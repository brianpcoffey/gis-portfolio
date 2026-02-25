// uiManager.js
// All DOM rendering and UI behaviors. Uses delegation and single-time event wiring.
// UI methods do not perform network requests or mutate global variables directly.

import { getState } from './stateStore.js';

let saveModalInstance = null;

function query(id) { return document.getElementById(id); }

export const UI = {
    init() {
        // Single-time event wiring for delegated lists and static buttons
        const savedList = query("savedFeaturesList");
        if (savedList && !savedList._delegationAttached) {
            savedList.addEventListener("click", (e) => {
                const btn = e.target.closest("button");
                if (!btn) {
                    const item = e.target.closest(".list-group-item");
                    if (item && item.dataset.featureId) {
                        const fid = item.dataset.featureId;
                        // emit an app-level custom event
                        document.dispatchEvent(new CustomEvent("ui:selectSaved", { detail: { featureId: fid } }));
                    }
                    return;
                }
                const action = btn.dataset.action;
                const fid = btn.dataset.featureId;
                if (action === "zoom") document.dispatchEvent(new CustomEvent("ui:zoomSaved", { detail: { featureId: fid } }));
                if (action === "remove") document.dispatchEvent(new CustomEvent("ui:removeSaved", { detail: { featureId: fid } }));
            });
            savedList._delegationAttached = true;
        }

        const collectionsList = query("collectionsList");
        if (collectionsList && !collectionsList._delegationAttached) {
            collectionsList.addEventListener("click", (e) => {
                const btn = e.target.closest(".list-group-item");
                if (!btn) return;
                const id = btn.dataset.collectionId;
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
            const id = String(saved.featureId || saved.FeatureId);
            const item = document.createElement("div");
            item.className = "list-group-item d-flex justify-content-between align-items-center";
            item.dataset.featureId = id;

            const left = document.createElement("div");
            left.style.cursor = "pointer";
            left.textContent = saved.displayName || saved.Name || saved.name || `State ${id}`;
            left.className = "flex-grow-1";
            item.appendChild(left);

            const btnGroup = document.createElement("div");
            btnGroup.className = "btn-group btn-group-sm";

            const zoomBtn = document.createElement("button");
            zoomBtn.type = "button";
            zoomBtn.className = "btn btn-outline-secondary";
            zoomBtn.title = "Zoom";
            zoomBtn.innerHTML = '<i class="fa-solid fa-search-plus"></i>';
            zoomBtn.dataset.action = "zoom";
            zoomBtn.dataset.featureId = id;
            btnGroup.appendChild(zoomBtn);

            const removeBtn = document.createElement("button");
            removeBtn.type = "button";
            removeBtn.className = "btn btn-outline-danger";
            removeBtn.title = "Remove";
            removeBtn.innerHTML = '<i class="fa-solid fa-trash"></i>';
            removeBtn.dataset.action = "remove";
            removeBtn.dataset.featureId = id;
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
                item.type = "button";
                item.className = "list-group-item list-group-item-action d-flex justify-content-between align-items-center";
                item.dataset.collectionId = String(col.id);
                item.innerHTML = `<span><span class="badge me-2" style="background:${col.color};width:10px;height:10px;border-radius:50%;display:inline-block;"></span>${col.name}</span>`;
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

    renderFeatureDetails(feature) {
        const target = query("featureDetails");
        if (!target) return;
        if (!feature) {
            target.innerHTML = '<p class="text-muted text-center small">Click a state on the map to view details</p>';
            return;
        }
        const html = `
            <h5>${feature.displayName || feature.Name || feature.name}</h5>
            <p class="small text-muted mb-1">Feature ID: ${feature.featureId || feature.FeatureId}</p>
            <p>${feature.Description || feature.description || ""}</p>
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