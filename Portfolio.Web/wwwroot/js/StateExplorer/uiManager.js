// uiManager.js
// All DOM rendering and UI behaviors. Uses delegation and single-time event wiring.
// UI methods do not perform network requests or mutate global variables directly.

import { getState, updateCollections, setSelectedCollection } from './stateStore.js';
import { CollectionService } from './collectionService.js';

let saveModalInstance = null;
let viewModalInstance = null;
let collectionModalInstance = null;

// Default swatch for a new collection (also what the collection dialog opens with).
const DEFAULT_COLLECTION_COLOR = "#6c757d";
const HEX_COLOR = /^#[0-9a-fA-F]{6}$/;

// Whether the visitor can use the collections API (false after a 401 for an anonymous visitor).
let collectionsSignedIn = true;

const el = (tag, attrs, ...kids) => window.PortfolioDom.el(tag, attrs, ...kids);
const icon = (cls) => el("i", { class: `fa-solid ${cls}`, "aria-hidden": "true" });

function query(id) { return document.getElementById(id); }

// Marks the collection-name field invalid and links it to its error text, or clears both.
// aria-describedby is only present while invalid, so the hidden message is not read otherwise.
function setNameInvalid(input, invalid) {
    input.classList.toggle("is-invalid", invalid);
    if (invalid) {
        input.setAttribute("aria-invalid", "true");
        input.setAttribute("aria-describedby", "collectionNameError");
    } else {
        input.removeAttribute("aria-invalid");
        input.removeAttribute("aria-describedby");
    }
}

// Puts focus back after the collection dialog closes: on the opener while it is still in the
// page, else on the row's name button (a saved edit re-renders the row), else on "+ New".
function restoreCollectionFocus(opener, rowId) {
    if (opener?.isConnected) { opener.focus(); return; }
    const row = rowId ? document.querySelector(`[data-collection-id="${CSS.escape(String(rowId))}"] .se-collection-select`) : null;
    (row || query("btnNewCollection"))?.focus();
}

// Extract useful metadata pairs from a feature object.
// Prioritizes a small set of friendly fields (Name, Feature ID, Region, Capital, Population (est.), Area, Admission)
// Omits any ObjectID/OBJECTID/ObjectId keys and maps common GIS field names to user-friendly labels.
function extractMetadataPairs(feature) {
    const pairs = [];
    if (!feature) return pairs;

    const pushIf = (label, val) => {
        if (val !== undefined && val !== null && String(val).trim() !== "") {
            pairs.push([label, val]);
        }
    };

    // Friendly primary fields
    pushIf("Name", feature.displayName || feature.Name || feature.name);
    pushIf("Feature ID", feature.featureId || feature.FeatureId);

    // Common sources to search
    const sources = [feature || {}, feature.properties || {}, feature.attributes || {}];

    const lookup = (keys) => {
        for (const key of keys) {
            for (const s of sources) {
                if (!s) continue;
                // direct match
                if (key in s) return s[key];
                // case-insensitive match
                const found = Object.keys(s).find(k => k.toLowerCase() === key.toLowerCase());
                if (found) return s[found];
            }
        }
        return undefined;
    };

    // Mapping of known property keys to friendly labels
    const keyLabelMap = {
        "sub_region": "Region",
        "region": "Region",
        "state_name": "State",
        "STATE_NAME": "State",
        "capital": "Capital",
        "population": "Population (est.)",
        "pop": "Population (est.)",
        "shape_area": "Area",
        "shape_length": "Perimeter",
        "area": "Area",
        "admission_date": "Admission",
        "state_fips": "FIPS",
        "state_code": "State Code",
        "abbr": "Abbreviation"
    };

    // Preferred keys (order matters)
    const preferredKeyGroups = [
        ["sub_region", "region", "SUB_REGION", "REGION"],
        ["capital", "CAPITAL"],
        ["population", "POP", "pop"],
        ["shape_area", "area", "SHAPE_AREA", "AREA"],
        ["admission_date", "ADMISSION_DATE"]
    ];

    // Push mapped preferred fields first
    for (const group of preferredKeyGroups) {
        const val = lookup(group);
        const exampleKey = group[0];
        const label = keyLabelMap[exampleKey.toLowerCase()] || (group[0] === "population" ? "Population (est.)" : undefined);
        pushIf(label || group[0], val);
    }

    // Short description if present
    pushIf("Short Description", feature.shortDescription || feature.description || feature.Description);

    // Fallback: include a few additional primitive properties (max 6),
    // but exclude object id fields and already displayed fields.
    const seenLabels = new Set(pairs.map(p => p[0].toLowerCase()));
    const extras = [];
    const propertySources = [feature.properties || {}, feature.attributes || {}, feature];

    const excludedKeysLower = new Set([
        "displayname", "name", "featureid", "feature_id", "description", "shortdescription",
        "objectid", "object_id"
    ]);

    const toFriendlyLabel = (k) => {
        const lower = k.toLowerCase();
        if (keyLabelMap[lower]) return keyLabelMap[lower];
        // convert snake/upper to Title Case: STATE_FIPS -> State Fips -> FIPS override handled above
        return k.replace(/_/g, " ").replace(/\b\w+/g, s => s.charAt(0).toUpperCase() + s.slice(1).toLowerCase());
    };

    for (const src of propertySources) {
        for (const k of Object.keys(src || {})) {
            const kLower = String(k).toLowerCase();
            if (excludedKeysLower.has(kLower)) continue;
            const label = toFriendlyLabel(k);
            if (seenLabels.has(label.toLowerCase())) continue;
            const v = src[k];
            if (v === undefined || v === null) continue;
            // skip large objects, stringify small objects
            if (typeof v === "object") {
                try {
                    const s = JSON.stringify(v);
                    if (s.length > 200) continue;
                    extras.push([label, s]);
                } catch {
                    continue;
                }
            } else {
                extras.push([label, v]);
            }
            seenLabels.add(label.toLowerCase());
            if (extras.length >= 6) break;
        }
        if (extras.length >= 6) break;
    }

    extras.forEach(p => pairs.push(p));

    return pairs;
}

export const UI = {
    DEFAULT_COLLECTION_COLOR,

    init() {
        // Single-time event wiring for delegated lists and static buttons
        const savedList = query("savedFeaturesList");
        if (savedList && !savedList._delegationAttached) {
            savedList.addEventListener("click", (e) => {
                const btn = e.target.closest("button");
                if (!btn) {
                    const item = e.target.closest(".list-group-item");
                    if (item && (item.dataset.featureId || item.dataset.savedId)) {
                        // select by external FeatureId (for viewing/zoom)
                        const fid = item.dataset.featureId;
                        document.dispatchEvent(new CustomEvent("ui:selectSaved", { detail: { featureId: fid } }));
                    }
                    return;
                }
                const action = btn.dataset.action;
                // Buttons now carry both saved-db id and external feature id
                const savedId = btn.dataset.savedId;
                const fid = btn.dataset.featureId;
                if (action === "select") document.dispatchEvent(new CustomEvent("ui:selectSaved", { detail: { featureId: fid } }));
                if (action === "zoom") document.dispatchEvent(new CustomEvent("ui:zoomSaved", { detail: { featureId: fid } }));
                if (action === "remove") document.dispatchEvent(new CustomEvent("ui:removeSaved", { detail: { savedId, featureId: fid } }));
                if (action === "view") document.dispatchEvent(new CustomEvent("ui:viewSaved", { detail: { savedId, featureId: fid } }));
            });
            savedList._delegationAttached = true;
        }

        const collectionsList = query("collectionsList");
        if (collectionsList && !collectionsList._delegationAttached) {
            collectionsList.addEventListener("click", async (e) => {
                const btn = e.target.closest("button");
                const item = e.target.closest(".list-group-item");
                if (!item) return;
                const id = item.dataset.collectionId;
                if (!id) return; // e.g. the sign-in hint row

                // If the click was on a small action button, handle edit/delete.
                if (btn && (btn.dataset.action === "edit" || btn.dataset.action === "delete")) {
                    const action = btn.dataset.action;
                    if (action === "edit") {
                        const state = getState();
                        const col = state.collections.find(c => String(c.id) === String(id));
                        if (!col) return;
                        const input = await UI.openCollectionModal({ title: "Edit collection", name: col.name, color: col.color });
                        if (!input) return;
                        const newName = input.name;
                        const newColor = input.color;
                        try {
                            await CollectionService.updateCollection(id, { name: newName, color: newColor });
                            // merge update locally
                            const updated = { ...col, name: newName, color: newColor, lastModified: new Date().toISOString() };
                            const newList = state.collections.map(c => String(c.id) === String(id) ? updated : c);
                            updateCollections(newList);
                            UI.showToast("Collection", "Collection updated", "success");
                            // keep selection on updated collection
                            setSelectedCollection(id);
                            // The re-render replaced the edit button that held focus.
                            if (!document.activeElement || document.activeElement === document.body) restoreCollectionFocus(null, id);
                        } catch (err) {
                            console.error("update collection failed", err);
                            UI.showToast("Collection", "Failed to update collection", "danger");
                        }
                    } else if (action === "delete") {
                        if (!await confirmDialog("Delete this collection? This cannot be undone.")) return;
                        try {
                            await CollectionService.deleteCollection(id);
                            const state = getState();
                            const newList = state.collections.filter(c => String(c.id) !== String(id));
                            updateCollections(newList);
                            UI.showToast("Collection", "Collection deleted", "success");
                            // clear selection if it was the deleted one
                            if (String(state.selectedCollectionId) === String(id)) {
                                setSelectedCollection(null);
                            }
                        } catch (err) {
                            console.error("delete collection failed", err);
                            UI.showToast("Collection", "Failed to delete collection", "danger");
                        }
                    }
                    return;
                }

                // Otherwise treat as select
                document.dispatchEvent(new CustomEvent("ui:selectCollection", { detail: { collectionId: id } }));
            });
            collectionsList._delegationAttached = true;
        }

        // Modal confirm handled once
        const confirmBtn = query("btnConfirmSave");
        if (confirmBtn && !confirmBtn._attached) {
            confirmBtn.addEventListener("click", () => {
                const modal = query("saveFeatureModal");
                const fid = query("modalFeatureId").value;
                const description = query("modalDescription").value || "";
                const collectionId = query("modalCollection").value || "";
                document.dispatchEvent(new CustomEvent("ui:confirmSave", { detail: { featureId: fid, description, collectionId } }));
            });
            confirmBtn._attached = true;
        }

        // Listen for view saved events and open modal
        if (!document._ui_view_handler_attached) {
            document.addEventListener("ui:viewSaved", (e) => {
                const d = e.detail || {};
                UI.openViewModal(d);
            });
            document._ui_view_handler_attached = true;
        }

        // Search debounce wiring - emit events
        const searchInput = query("searchInput");
        if (searchInput && !searchInput._attached) {
            let timeout;
            searchInput.addEventListener("input", (e) => {
                clearTimeout(timeout);
                const q = e.target.value;
                timeout = setTimeout(() => {
                    document.dispatchEvent(new CustomEvent("ui:search", { detail: { query: q } } ));
                }, 250);
            });
            searchInput._attached = true;
        }

        // Comparison panel wiring
        const closeBtn = query("btnCloseComparison");
        if (closeBtn && !closeBtn._attached) {
            closeBtn.addEventListener("click", () => {
                document.dispatchEvent(new CustomEvent("ui:closeComparison"));
            });
            closeBtn._attached = true;
        }

        const comparisonContent = query("comparisonContent");
        if (comparisonContent && !comparisonContent._delegationAttached) {
            comparisonContent.addEventListener("click", (e) => {
                const btn = e.target.closest("button");
                if (!btn) return;
                const action = btn.dataset.action;
                const fid = btn.dataset.featureId;
                if (action === "zoom") document.dispatchEvent(new CustomEvent("ui:zoomCompare", { detail: { featureId: fid } }));
                if (action === "toggle") document.dispatchEvent(new CustomEvent("ui:toggleCompare", { detail: { featureId: fid } }));
            });
            comparisonContent._delegationAttached = true;
        }
    },

    // Render helpers - idempotent and quick
    renderSavedFeaturesList() {
        const container = query("savedFeaturesList");
        const countEl = query("savedCount");
        if (!container) return;
        container.replaceChildren();
        const state = getState();
        const entries = Array.from(state.savedFeatures.values());
        if (!entries.length) {
            container.appendChild(el("div", { class: "list-group-item theme-text-muted text-center small", text: "No saved states yet" }));
            if (countEl) countEl.textContent = "0";
            return;
        }

        const frag = document.createDocumentFragment();
        entries.forEach(saved => {
            // saved: may contain saved.Id (db id) and featureId (external)
            const featureKey = String(saved.featureId || saved.FeatureId);
            const savedDbId = String(saved.id || saved.Id || "");
            const item = el("div", { class: "list-group-item d-flex justify-content-between align-items-center" });
            // store both for click handling
            item.dataset.featureId = featureKey;
            if (savedDbId) item.dataset.savedId = savedDbId;

            // Build display title with optional collection
            const titleText = saved.displayName || saved.Name || saved.name || `State ${featureKey}`;
            const collectionName = saved.CollectionName || saved.collectionName;
            const left = el("button", { type: "button", class: "btn btn-link p-0 text-start flex-grow-1 se-saved-select", "data-action": "select", "data-feature-id": featureKey },
                titleText);
            if (collectionName) {
                left.append(" ", el("small", { class: "theme-text-muted ms-2", text: `(${collectionName})` }));
            }
            item.appendChild(left);

            const btnGroup = el("div", { class: "btn-group btn-group-sm" });
            const makeBtn = (cls, label, iconCls, action) => {
                const b = el("button", { type: "button", class: `btn ${cls}`, title: label, "aria-label": `${label}: ${titleText}`, "data-action": action },
                    icon(iconCls));
                b.dataset.featureId = featureKey;
                // also include saved id on the button to make it available (remove needs it for the API)
                if (savedDbId) b.dataset.savedId = savedDbId;
                return b;
            };
            btnGroup.appendChild(makeBtn("btn-outline-accent", "View details", "fa-circle-info", "view"));
            btnGroup.appendChild(makeBtn("btn-outline-secondary", "Zoom", "fa-magnifying-glass-plus", "zoom"));
            btnGroup.appendChild(makeBtn("btn-outline-danger", "Remove", "fa-trash", "remove"));

            item.appendChild(btnGroup);
            frag.appendChild(item);
        });
        container.appendChild(frag);
        if (countEl) countEl.textContent = String(entries.length);
    },

    // Anonymous visitors cannot use the collections API: show a sign-in hint and disable "+ New".
    setCollectionsSignedIn(signedIn) {
        collectionsSignedIn = !!signedIn;
        const btn = query("btnNewCollection");
        if (btn) {
            btn.setAttribute("aria-disabled", String(!collectionsSignedIn));
            btn.title = collectionsSignedIn ? "" : "Sign in to create collections";
        }
        this.renderCollections();
    },

    isCollectionsSignedIn() { return collectionsSignedIn; },

    renderCollections() {
        const list = query("collectionsList");
        const select = query("modalCollection");
        if (list) list.replaceChildren();
        if (select) {
            select.innerHTML = '<option value="">-- None --</option>';
        }

        if (!collectionsSignedIn) {
            if (list) {
                const returnUrl = encodeURIComponent(window.location.pathname);
                list.appendChild(el("div", { class: "list-group-item theme-text-muted small", id: "collectionsSignInHint" },
                    "Sign in to use collections. ",
                    el("a", { href: `/Login?returnUrl=${returnUrl}`, text: "Sign in" })));
            }
            return;
        }

        const state = getState();
        if (list && !state.collections.length) {
            list.appendChild(el("div", { class: "list-group-item theme-text-muted text-center small", text: "No collections yet" }));
        }
        state.collections.forEach(col => {
            if (list) {
                const isActive = String(state.selectedCollectionId) === String(col.id);
                const item = el("div", {
                    class: "list-group-item d-flex justify-content-between align-items-center gap-2" + (isActive ? " active" : ""),
                    "data-collection-id": String(col.id)
                });

                // Colour badge: built as a node; the colour reaches CSS only after a hex check.
                const colorBadge = el("span", { class: "se-color-dot me-2", "aria-hidden": "true" });
                if (HEX_COLOR.test(col.color)) colorBadge.style.backgroundColor = col.color;

                // The name is its own button; edit and delete are siblings, never nested inside it.
                const nameBtn = el("button", {
                    type: "button",
                    class: "btn btn-link p-0 text-start flex-grow-1 se-collection-select",
                    "data-action": "select",
                    "aria-pressed": String(isActive)
                }, colorBadge, el("span", { text: col.name }));

                const group = el("div", { class: "btn-group btn-group-sm" },
                    el("button", { type: "button", class: "btn btn-outline-secondary btn-sm", "data-action": "edit", title: "Edit", "aria-label": `Edit collection ${col.name}` }, icon("fa-pen")),
                    el("button", { type: "button", class: "btn btn-outline-danger btn-sm ms-1", "data-action": "delete", title: "Delete", "aria-label": `Delete collection ${col.name}` }, icon("fa-trash")));

                item.append(nameBtn, group);
                list.appendChild(item);
            }
            if (select) {
                const opt = document.createElement("option");
                opt.value = String(col.id);
                opt.textContent = col.name;
                select.appendChild(opt);
            }
        });
    },

    // Replaces window.prompt: a Bootstrap modal with a name and a colour. Resolves to
    // { name, color } on Save, or null when the dialog is dismissed.
    openCollectionModal({ title = "New collection", name = "", color = DEFAULT_COLLECTION_COLOR } = {}) {
        const modalEl = query("collectionModal");
        const form = query("collectionForm");
        const nameInput = query("collectionName");
        const colorInput = query("collectionColor");
        if (!modalEl || !form || !nameInput || !colorInput) return Promise.resolve(null);

        // Bootstrap only restores focus to data-api triggers, so remember who opened the dialog
        // (the "+ New" button, or the edit button of a collection row).
        const opener = document.activeElement;
        const openerRowId = opener?.closest?.("[data-collection-id]")?.dataset.collectionId;

        query("collectionModalTitle").textContent = title;
        nameInput.value = name || "";
        setNameInvalid(nameInput, false);
        colorInput.value = HEX_COLOR.test(color || "") ? color : DEFAULT_COLLECTION_COLOR;
        collectionModalInstance = bootstrap.Modal.getOrCreateInstance(modalEl);

        return new Promise(resolve => {
            let result = null;
            const onSubmit = (e) => {
                e.preventDefault();
                const trimmed = nameInput.value.trim();
                if (!trimmed) {
                    setNameInvalid(nameInput, true);
                    nameInput.focus();
                    return;
                }
                result = { name: trimmed, color: colorInput.value };
                collectionModalInstance.hide();
            };
            const onInput = () => setNameInvalid(nameInput, false);
            const onShown = () => nameInput.focus();
            const onHidden = () => {
                form.removeEventListener("submit", onSubmit);
                nameInput.removeEventListener("input", onInput);
                modalEl.removeEventListener("shown.bs.modal", onShown);
                setNameInvalid(nameInput, false);
                resolve(result);
                setTimeout(() => restoreCollectionFocus(opener, openerRowId), 0);
            };
            form.addEventListener("submit", onSubmit);
            nameInput.addEventListener("input", onInput);
            modalEl.addEventListener("shown.bs.modal", onShown, { once: true });
            modalEl.addEventListener("hidden.bs.modal", onHidden, { once: true });
            collectionModalInstance.show();
        });
    },

    renderStats() {
        const state = getState();
        const total = state.features.length || 0;
        const saved = state.savedFeatures.size || 0;
        const selected = state.selectedFeatureId ? 1 : 0;
        const comparing = state.compareSet.size || 0;
        const set = (id, val) => { const el = query(id); if (el) el.textContent = String(val); };
        set("statTotal", total);
        set("statSaved", saved);
        set("statSelected", selected);
        set("statComparing", comparing);
    },

    // Delegates to the shared toast (toasts.js); "primary" maps to the info variant.
    showToast(title, message, type = "primary") {
        const variant = type === "primary" ? "info" : type;
        window.showToast(message, variant);
    },

    // Modal helpers
    openSaveModal(featureId, featureName = "") {
        const modalEl = query("saveFeatureModal");
        if (!modalEl) return;
        // Use single Modal instance
        saveModalInstance = bootstrap.Modal.getOrCreateInstance(modalEl);
        query("modalFeatureId").value = String(featureId || "");
        query("modalFeatureName").value = featureName || "";
        query("modalDescription").value = "";
        saveModalInstance.show();
    },

    closeSaveModal() {
        if (!saveModalInstance) return;
        saveModalInstance.hide();
    },

    // New: view saved modal helpers
    openViewModal(detail = {}) {
        const modalEl = query("viewSavedModal");
        if (!modalEl) return;
        viewModalInstance = bootstrap.Modal.getOrCreateInstance(modalEl);

        // Find saved entry in state by savedId or featureId
        const state = getState();
        let saved;
        if (detail.savedId) {
            saved = Array.from(state.savedFeatures.values()).find(s => String(s.id || s.Id || "") === String(detail.savedId));
        }
        if (!saved && detail.featureId) {
            saved = state.savedFeatures.get(String(detail.featureId));
        }
        if (!saved) {
            // fallback: show minimal info
            query("viewSavedTitle").textContent = `Saved State`;
            query("viewSavedMeta").innerHTML = "";
        } else {
            const title = saved.Name || saved.displayName || saved.name || `State ${saved.FeatureId || saved.featureId || ""}`;
            const desc = (saved.Description || saved.description || "").toString();
            query("viewSavedTitle").textContent = title;

            // metadata
            const metaPairs = extractMetadataPairs(saved);
            if (metaPairs.length) {
                const html = metaPairs.map(p => `<div><strong>${PortfolioDom.escapeHtml(p[0])}:</strong> ${PortfolioDom.escapeHtml(String(p[1]))}</div>`).join("");
                query("viewSavedMeta").innerHTML = html;
            } else {
                query("viewSavedMeta").innerHTML = "";
            }

            // attach zoom data attributes
            const zoomBtn = query("btnViewZoom");
            if (zoomBtn) {
                zoomBtn.dataset.featureId = String(saved.FeatureId || saved.featureId || "");
            }

            // attach remove/unsave
            const removeBtn = query("btnViewRemove");
            if (removeBtn) {
                removeBtn.dataset.savedId = String(saved.id || saved.Id || "");
                removeBtn.dataset.featureId = String(saved.FeatureId || saved.featureId || "");
            }
        }

        // wire buttons (idempotent)
        const zoomBtn = query("btnViewZoom");
        if (zoomBtn) {
            zoomBtn.onclick = () => {
                const fid = zoomBtn.dataset.featureId;
                document.dispatchEvent(new CustomEvent("ui:zoomSaved", { detail: { featureId: fid } }));
            };
        }
        const removeBtn = query("btnViewRemove");
        if (removeBtn) {
            removeBtn.onclick = async () => {
                const savedId = removeBtn.dataset.savedId || "";
                const featureId = removeBtn.dataset.featureId || "";
                if (!await confirmDialog("Remove saved state?", { confirmText: "Remove" })) return;
                document.dispatchEvent(new CustomEvent("ui:removeSaved", { detail: { savedId, featureId } }));
                // close modal after action
                if (viewModalInstance) viewModalInstance.hide();
            };
        }
        viewModalInstance.show();
    },

    closeViewModal() {
        if (!viewModalInstance) return;
        viewModalInstance.hide();
    },

    // Comparison panel rendering
    renderComparison() {
        const panel = query("comparisonPanel");
        const content = query("comparisonContent");
        if (!panel || !content) return;

        const state = getState();
        const ids = Array.from(state.compareSet || []);
        // If compareMode is not enabled or there are no ids, hide panel
        if (!state.compareMode || ids.length === 0) {
            panel.classList.add("d-none");
            content.innerHTML = "";
            this.renderCompareIndicator();
            return;
        }

        // Build content
        const frag = document.createDocumentFragment();
        ids.forEach(id => {
            // Try to locate feature in features list first, then savedFeatures
            let feature = state.features.find(f => String(f.featureId || f.FeatureId) === String(id));
            if (!feature) feature = state.savedFeatures.get(String(id));
            const title = feature?.displayName || feature?.Name || feature?.name || `State ${id}`;
            const desc = (feature?.Description || feature?.description || "").toString();

            // Build metadata snippet
            const metaPairs = extractMetadataPairs(feature);
            const metaHtml = metaPairs.length ? `<div class="mt-2"><small class="theme-text-muted">${metaPairs.map(p => `<div>${PortfolioDom.escapeHtml(p[0])}: <strong>${PortfolioDom.escapeHtml(p[1])}</strong></div>`).join("")}</small></div>` : "";

            const card = document.createElement("div");
            card.className = "card theme-card mb-2 p-2";
            card.innerHTML = `
                <div class="d-flex justify-content-between align-items-start">
                    <div style="min-width:0;">
                        <strong>${PortfolioDom.escapeHtml(title)}</strong>
                        <div class="small theme-text-muted">ID: ${PortfolioDom.escapeHtml(id)}</div>
                        <div class="small text-truncate se-compare-desc">${PortfolioDom.escapeHtml(desc)}</div>
                        ${metaHtml}
                    </div>
                    <div class="btn-group-vertical btn-group-sm ms-2">
                        <button type="button" class="btn btn-outline-secondary" data-action="zoom" data-feature-id="${PortfolioDom.escapeHtml(id)}" title="Zoom to feature" aria-label="Zoom to ${PortfolioDom.escapeHtml(title)}"><i class="fa-solid fa-magnifying-glass-plus" aria-hidden="true"></i></button>
                        <button type="button" class="btn btn-outline-danger" data-action="toggle" data-feature-id="${PortfolioDom.escapeHtml(id)}" title="Remove from comparison" aria-label="Remove ${PortfolioDom.escapeHtml(title)} from comparison"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button>
                    </div>
                </div>
            `;
            frag.appendChild(card);
        });

        content.innerHTML = "";
        content.appendChild(frag);
        panel.classList.remove("d-none");
        this.renderCompareIndicator();
    },

    // Visual indicator for compare mode (toggles button style/text)
    renderCompareIndicator() {
        const btn = query("btnCompareMode");
        if (!btn) return;
        const state = getState();
        btn.classList.toggle("btn-accent", !!state.compareMode);
        btn.classList.toggle("btn-outline-accent", !state.compareMode);
        btn.classList.toggle("active", !!state.compareMode);
        btn.setAttribute("aria-pressed", String(!!state.compareMode));
        btn.replaceChildren(
            el("i", { class: "fa-solid fa-chart-column me-1", "aria-hidden": "true" }),
            state.compareMode ? `Compare Mode (${state.compareSet.size || 0})` : "Compare Mode");
    },

    renderFeatureDetails(feature) {
        const target = query("featureDetails");
        if (!target) return;
        if (!feature) {
            target.innerHTML = '<p class="theme-text-muted text-center small">Click a state on the map to view details</p>';
            return;
        }

        const state = getState();
        const fid = String(feature.featureId || feature.FeatureId || "");
        // authoritative saved state is in savedFeatures map
        const savedEntry = state.savedFeatures.get(fid);

        const title = PortfolioDom.escapeHtml(feature.displayName || feature.Name || feature.name || "");
        const description = PortfolioDom.escapeHtml(feature.Description || feature.description || "");

        // Build metadata table
        const metaPairs = extractMetadataPairs(feature);
        let metaHtml = "";
        if (metaPairs.length) {
            metaHtml = `<dl class="row small mb-2">` +
                metaPairs.map(([k, v]) => `<dt class="col-5 theme-text-muted">${PortfolioDom.escapeHtml(k)}</dt><dd class="col-7"><strong>${PortfolioDom.escapeHtml(v)}</strong></dd>`).join("") +
                `</dl>`;
        }

        // If savedEntry exists show Saved state and Unsave action, otherwise show Save button
        let actionHtml = "";
        if (savedEntry) {
            actionHtml = `
                <button id="btnSavedIndicator" type="button" class="btn btn-sm btn-outline-accent" disabled>Saved</button>
                <button id="btnUnsaveFromDetail" type="button" class="btn btn-sm btn-outline-danger">Unsave</button>
                <button id="btnZoomFromDetail" type="button" class="btn btn-sm btn-outline-secondary">Zoom</button>
            `;
        } else {
            actionHtml = `
                <button id="btnSaveFromDetail" type="button" class="btn btn-sm btn-accent">Save</button>
                <button id="btnZoomFromDetail" type="button" class="btn btn-sm btn-outline-secondary">Zoom</button>
            `;
        }

        const html = `
            <h3 class="h5">${title}</h3>
            <p class="small theme-text-muted mb-1">Feature ID: ${PortfolioDom.escapeHtml(fid)}</p>
            ${metaHtml}
            <p>${description}</p>
            <div class="d-flex gap-2">
                ${actionHtml}
            </div>
        `;
        target.innerHTML = html;

        // Wire buttons
        const saveBtn = query("btnSaveFromDetail");
        if (saveBtn) {
            saveBtn.onclick = () => {
                document.dispatchEvent(new CustomEvent("ui:openSaveModal", { detail: { featureId: feature.featureId || feature.FeatureId, feature } }));
            };
        }

        const unsaveBtn = query("btnUnsaveFromDetail");
        if (unsaveBtn) {
            unsaveBtn.onclick = () => {
                // Use saved DB id if available, else send feature id
                const savedId = String(savedEntry?.id || savedEntry?.Id || "");
                const payload = savedId ? { savedId, featureId: fid } : { featureId: fid };
                document.dispatchEvent(new CustomEvent("ui:removeSaved", { detail: payload }));
            };
        }

        const zoomBtn = query("btnZoomFromDetail");
        if (zoomBtn) {
            zoomBtn.onclick = () => document.dispatchEvent(new CustomEvent("ui:zoomSaved", { detail: { featureId: feature.featureId || feature.FeatureId } }));
        }
    }
};