// uiManager.js
// All DOM rendering and UI behaviors. Uses delegation and single-time event wiring.
// UI methods do not perform network requests or mutate global variables directly.
//
// This file assembles the public `UI` object. The pieces live beside it:
//   metadata.js    extractMetadataPairs (pure)
//   lists.js       saved-states list, collections list, statistics counters
//   modals.js      collection / save / view dialogs
//   comparison.js  comparison panel and Compare Mode button
//   uiHelpers.js   query / el / icon helpers and shared constants
// Event wiring (init) and the feature-details panel stay here.

import { getState, updateCollections, setSelectedCollection } from './stateStore.js';
import { CollectionService } from './collectionService.js';
import { extractMetadataPairs } from './metadata.js';
import { renderSavedFeaturesList, setCollectionsSignedIn, isCollectionsSignedIn, renderCollections, renderStats } from './lists.js';
import { openCollectionModal, openSaveModal, closeSaveModal, openViewModal, closeViewModal, restoreCollectionFocus } from './modals.js';
import { renderComparison, renderCompareIndicator } from './comparison.js';
import { DEFAULT_COLLECTION_COLOR, query } from './uiHelpers.js';

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
                        const input = await UI.openCollectionModal({ title: "Edit Collection", name: col.name, color: col.color });
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

    renderSavedFeaturesList,
    setCollectionsSignedIn,
    isCollectionsSignedIn,
    renderCollections,
    openCollectionModal,
    renderStats,

    // Delegates to the shared toast (toasts.js); "primary" maps to the info variant.
    showToast(title, message, type = "primary") {
        const variant = type === "primary" ? "info" : type;
        window.showToast(message, variant);
    },

    openSaveModal,
    closeSaveModal,
    openViewModal,
    closeViewModal,
    renderComparison,
    renderCompareIndicator,

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