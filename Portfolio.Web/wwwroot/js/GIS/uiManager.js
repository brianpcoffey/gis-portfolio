// uiManager.js
import { appState, setState } from './stateStore.js';
import { MapManager } from './mapManager.js';

export const UI = {
    renderSavedFeaturesList() {
        const container = document.getElementById("savedFeaturesList");
        const countEl = document.getElementById("savedCount");
        if (!container) return;
        container.innerHTML = "";
        const entries = Array.from(appState.savedFeatures.values());
        if (!entries.length) {
            container.innerHTML = '<div class="list-group-item text-muted text-center small">No saved states yet</div>';
            if (countEl) countEl.textContent = "0";
            return;
        }
        entries.forEach(saved => {
            const item = document.createElement("div");
            item.className = "list-group-item d-flex justify-content-between align-items-center";
            const left = document.createElement("div");
            left.style.cursor = "pointer";
            left.textContent = saved.displayName || saved.name || `State ${saved.featureId}`;
            left.addEventListener("click", () => UI.selectFeature(saved.featureId));
            item.appendChild(left);

            const btnGroup = document.createElement("div");
            btnGroup.className = "btn-group btn-group-sm";

            const zoomBtn = document.createElement("button");
            zoomBtn.className = "btn btn-outline-secondary";
            zoomBtn.title = "Zoom";
            zoomBtn.innerHTML = '<i class="fa-solid fa-search-plus"></i>';
            zoomBtn.addEventListener("click", () => MapManager.zoomToFeature(saved.featureId));
            btnGroup.appendChild(zoomBtn);

            const removeBtn = document.createElement("button");
            removeBtn.className = "btn btn-outline-danger";
            removeBtn.title = "Remove";
            removeBtn.innerHTML = '<i class="fa-solid fa-trash"></i>';
            removeBtn.addEventListener("click", () => UI.removeFeature(saved.featureId));
            btnGroup.appendChild(removeBtn);

            item.appendChild(btnGroup);
            container.appendChild(item);
        });
        if (countEl) countEl.textContent = String(entries.length);
    },
    renderCollections() {
        const list = document.getElementById("collectionsList");
        const select = document.getElementById("modalCollection");
        if (list) list.innerHTML = "";
        if (select) select.innerHTML = '<option value="">-- None --</option>';
        appState.collections.forEach(col => {
            if (list) {
                const item = document.createElement("button");
                item.type = "button";
                item.className = "list-group-item list-group-item-action d-flex justify-content-between align-items-center";
                item.innerHTML = `<span><span class="badge me-2" style="background:${col.color};width:10px;height:10px;border-radius:50%;display:inline-block;"></span>${col.name}</span>`;
                item.addEventListener("click", () => {
                    setState({ selectedCollectionId: col.id });
                    Array.from(list.children).forEach(ch => ch.classList.remove("active"));
                    item.classList.add("active");
                });
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
        document.getElementById("statTotal").textContent = String(appState.features.length || 0);
        document.getElementById("statSaved").textContent = String(appState.savedFeatures.size || 0);
        document.getElementById("statSelected").textContent = appState.selectedFeatureId ? "1" : "0";
        document.getElementById("statComparing").textContent = String(appState.compareSet.size || 0);
    },
    showToast(title, message, type = "primary") {
        const toast = document.getElementById("toast");
        document.getElementById("toastTitle").textContent = title;
        document.getElementById("toastMessage").textContent = message;
        toast.classList.remove("bg-danger", "bg-success", "bg-primary", "text-bg-danger");
        if (type === "danger") toast.classList.add("text-bg-danger");
        new bootstrap.Toast(toast).show();
    },
    // ... More UI helpers: modals, details, comparison, etc.
    // For brevity, implement as in your original, but always read/write via appState.
    // All event handlers should be registered here, not inline.
    // Debounce search input, etc.
    // ...
    selectFeature(featureId) {
        setState({ selectedFeatureId: featureId });
        MapManager.zoomToFeature(featureId);
    },
    removeFeature(featureId) {
        // This will call FeatureService.deleteFeature and update state accordingly
        // Implementation in app.js for orchestration
    }
};