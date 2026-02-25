// uiManager.js
// All DOM rendering and UI behaviors. Uses delegation and single-time event wiring.
// UI methods do not perform network requests or mutate global variables directly.

import { getState, updateCollections, setSelectedCollection } from './stateStore.js';
import { CollectionService } from './collectionService.js';

let saveModalInstance = null;

function query(id) { return document.getElementById(id); }

function escapeHtml(s) {
    if (s === null || s === undefined) return "";
    return String(s)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
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
                if (action === "zoom") document.dispatchEvent(new CustomEvent("ui:zoomSaved", { detail: { featureId: fid } }));
                if (action === "remove") document.dispatchEvent(new CustomEvent("ui:removeSaved", { detail: { savedId, featureId: fid } }));
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

                // If the click was on a small action button, handle edit/delete.
                if (btn) {
                    const action = btn.dataset.action;
                    if (action === "edit") {
                        // Prompt for new values (simple UX)
                        const state = getState();
                        const col = state.collections.find(c => String(c.id) === String(id));
                        if (!col) return;
                        const newName = prompt("Collection name:", col.name);
                        if (!newName || !newName.trim()) return;
                        const newColor = prompt("Collection color (hex):", col.color || "#6c757d") || col.color;
                        try {
                            await CollectionService.updateCollection(id, { name: newName.trim(), color: newColor.trim() });
                            // merge update locally
                            const updated = { ...col, name: newName.trim(), color: newColor.trim(), lastModified: new Date().toISOString() };
                            const newList = state.collections.map(c => String(c.id) === String(id) ? updated : c);
                            updateCollections(newList);
                            UI.showToast("Collection", "Collection updated", "success");
                            // keep selection on updated collection
                            setSelectedCollection(id);
                        } catch (err) {
                            console.error("update collection failed", err);
                            UI.showToast("Collection", "Failed to update collection", "danger");
                        }
                    } else if (action === "delete") {
                        if (!confirm("Delete this collection? This cannot be undone.")) return;
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

        // Search debounce wiring - emit events
        const searchInput = query("searchInput");
        if (searchInput && !searchInput._attached) {
            let timeout;
            searchInput.addEventListener("input", (e) => {
                clearTimeout(timeout);
                const q = e.target.value;
                timeout = setTimeout(() => {
                    document.dispatchEvent(new CustomEvent("ui:search", { detail: { query: q } }));
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
        container.innerHTML = "";
        const state = getState();
        const entries = Array.from(state.savedFeatures.values());
        if (!entries.length) {
            container.innerHTML = '<div class="list-group-item text-muted text-center small">No saved states yet</div>';
            if (countEl) countEl.textContent = "0";
            return;
        }

        const frag = document.createDocumentFragment();
        entries.forEach(saved => {
            // saved: may contain saved.Id (db id) and featureId (external)
            const featureKey = String(saved.featureId || saved.FeatureId);
            const savedDbId = String(saved.id || saved.Id || "");
            const item = document.createElement("div");
            item.className = "list-group-item d-flex justify-content-between align-items-center";
            // store both for click handling
            item.dataset.featureId = featureKey;
            if (savedDbId) item.dataset.savedId = savedDbId;

            const left = document.createElement("div");
            left.style.cursor = "pointer";
            left.textContent = saved.displayName || saved.Name || saved.name || `State ${featureKey}`;
            left.className = "flex-grow-1";
            item.appendChild(left);

            const btnGroup = document.createElement("div");
            btnGroup.className = "btn-group btn-group-sm";

            const zoomBtn = document.createElement("button");
            zoomBtn.type = "button";
            zoomBtn.className = "btn btn-outline-secondary";
            zoomBtn.title = "Zoom";
            zoomBtn.innerHTML = '<i class="fa-solid fa-magnifying-glass-plus"></i>';
            zoomBtn.dataset.action = "zoom";
            zoomBtn.dataset.featureId = featureKey;
            // also include saved id on the button to make it available
            if (savedDbId) zoomBtn.dataset.savedId = savedDbId;
            btnGroup.appendChild(zoomBtn);

            const removeBtn = document.createElement("button");
            removeBtn.type = "button";
            removeBtn.className = "btn btn-outline-danger";
            removeBtn.title = "Remove";
            removeBtn.innerHTML = '<i class="fa-solid fa-trash"></i>';
            removeBtn.dataset.action = "remove";
            // remove needs saved DB id for API and feature id for client removal
            removeBtn.dataset.featureId = featureKey;
            if (savedDbId) removeBtn.dataset.savedId = savedDbId;
            btnGroup.appendChild(removeBtn);

            item.appendChild(btnGroup);
            frag.appendChild(item);
        });
        container.appendChild(frag);
        if (countEl) countEl.textContent = String(entries.length);
    },

    renderCollections() {
        const list = query("collectionsList");
        const select = query("modalCollection");
        if (list) list.innerHTML = "";
        if (select) {
            select.innerHTML = '<option value="">-- None --</option>';
        }
        const state = getState();
        state.collections.forEach(col => {
            if (list) {
                const item = document.createElement("button");
                const isActive = String(state.selectedCollectionId) === String(col.id);
                item.type = "button";
                item.className = "list-group-item list-group-item-action d-flex justify-content-between align-items-center" + (isActive ? " active" : "");
                item.dataset.collectionId = String(col.id);

                // left: badge + name, right: small action buttons
                item.innerHTML = `<span><span class="badge me-2" style="background:${escapeHtml(col.color)};width:10px;height:10px;border-radius:50%;display:inline-block;"></span>${escapeHtml(col.name)}</span>
                                  <div class="btn-group btn-group-sm">
                                    <button type="button" class="btn btn-outline-light btn-sm" data-action="edit" title="Edit"><i class="fa-solid fa-pen"></i></button>
                                    <button type="button" class="btn btn-outline-danger btn-sm ms-1" data-action="delete" title="Delete"><i class="fa-solid fa-trash"></i></button>
                                  </div>`;
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

    showToast(title, message, type = "primary") {
        const toastEl = query("toast");
        if (!toastEl) return;
        const titleEl = query("toastTitle");
        const msgEl = query("toastMessage");
        if (titleEl) titleEl.textContent = title;
        if (msgEl) msgEl.textContent = message;
        toastEl.classList.remove("text-bg-danger", "bg-success", "bg-primary");
        if (type === "danger") toastEl.classList.add("text-bg-danger");
        else if (type === "success") toastEl.classList.add("bg-success");
        else toastEl.classList.add("bg-primary");
        try {
            const bs = bootstrap.Toast.getOrCreateInstance(toastEl);
            bs.show();
        } catch (e) {
            console.warn("Toast show failed", e);
        }
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
            const metaHtml = metaPairs.length ? `<div class="mt-2"><small class="text-muted">${metaPairs.map(p => `<div>${escapeHtml(p[0])}: <strong>${escapeHtml(p[1])}</strong></div>`).join("")}</small></div>` : "";

            const card = document.createElement("div");
            card.className = "card mb-2 p-2";
            card.innerHTML = `
                <div class="d-flex justify-content-between align-items-start">
                    <div style="min-width:0;">
                        <strong>${escapeHtml(title)}</strong>
                        <div class="small text-muted">ID: ${escapeHtml(id)}</div>
                        <div class="small text-truncate" style="max-width:360px;">${escapeHtml(desc)}</div>
                        ${metaHtml}
                    </div>
                    <div class="btn-group-vertical btn-group-sm ms-2">
                        <button type="button" class="btn btn-outline-secondary" data-action="zoom" data-feature-id="${escapeHtml(id)}" title="Zoom to feature">🔎</button>
                        <button type="button" class="btn btn-outline-danger" data-action="toggle" data-feature-id="${escapeHtml(id)}" title="Remove from comparison">✖</button>
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
        if (state.compareMode) {
            // active style
            btn.classList.remove("btn-outline-warning");
            btn.classList.add("btn-warning", "active");
            btn.textContent = `📊 Compare Mode (${state.compareSet.size || 0})`;
        } else {
            btn.classList.remove("btn-warning", "active");
            btn.classList.add("btn-outline-warning");
            btn.textContent = "📊 Compare Mode";
        }
    },

    renderFeatureDetails(feature) {
        const target = query("featureDetails");
        if (!target) return;
        if (!feature) {
            target.innerHTML = '<p class="text-muted text-center small">Click a state on the map to view details</p>';
            return;
        }

        const title = escapeHtml(feature.displayName || feature.Name || feature.name || "");
        const fid = escapeHtml(feature.featureId || feature.FeatureId || "");
        const description = escapeHtml(feature.Description || feature.description || "");

        // Build metadata table
        const metaPairs = extractMetadataPairs(feature);
        let metaHtml = "";
        if (metaPairs.length) {
            metaHtml = `<dl class="row small mb-2">` +
                metaPairs.map(([k, v]) => `<dt class="col-5 text-muted">${escapeHtml(k)}</dt><dd class="col-7"><strong>${escapeHtml(v)}</strong></dd>`).join("") +
                `</dl>`;
        }

        const html = `
            <h5>${title}</h5>
            <p class="small text-muted mb-1">Feature ID: ${fid}</p>
            ${metaHtml}
            <p>${description}</p>
            <div class="d-flex gap-2">
                <button id="btnSaveFromDetail" class="btn btn-sm btn-primary">Save</button>
                <button id="btnZoomFromDetail" class="btn btn-sm btn-outline-secondary">Zoom</button>
            </div>
        `;
        target.innerHTML = html;

        // Wire the two buttons once (replace handlers to avoid duplicates)
        const saveBtn = query("btnSaveFromDetail");
        if (saveBtn) {
            saveBtn.onclick = () => {
                document.dispatchEvent(new CustomEvent("ui:openSaveModal", { detail: { featureId: feature.featureId || feature.FeatureId, feature } }));
            };
        }
        const zoomBtn = query("btnZoomFromDetail");
        if (zoomBtn) {
            zoomBtn.onclick = () => document.dispatchEvent(new CustomEvent("ui:zoomSaved", { detail: { featureId: feature.featureId || feature.FeatureId } }));
        }
    }
};