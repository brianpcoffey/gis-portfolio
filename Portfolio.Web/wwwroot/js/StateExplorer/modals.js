// The three Bootstrap dialogs: new/edit collection, save a state, and view a saved state.
// Each opener builds on a single Modal instance; the confirm buttons dispatch ui:* events (see uiManager.js).

import { getState } from './stateStore.js';
import { extractMetadataPairs } from './metadata.js';
import { DEFAULT_COLLECTION_COLOR, HEX_COLOR, query } from './uiHelpers.js';

let saveModalInstance = null;
let viewModalInstance = null;
let collectionModalInstance = null;

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
export function restoreCollectionFocus(opener, rowId) {
    if (opener?.isConnected) { opener.focus(); return; }
    const row = rowId ? document.querySelector(`[data-collection-id="${CSS.escape(String(rowId))}"] .se-collection-select`) : null;
    (row || query("btnNewCollection"))?.focus();
}

// Replaces window.prompt: a Bootstrap modal with a name and a color. Resolves to
// { name, color } on Save, or null when the dialog is dismissed.
export function openCollectionModal({ title = "New Collection", name = "", color = DEFAULT_COLLECTION_COLOR } = {}) {
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
}

export function openSaveModal(featureId, featureName = "") {
    const modalEl = query("saveFeatureModal");
    if (!modalEl) return;
    saveModalInstance = bootstrap.Modal.getOrCreateInstance(modalEl);
    query("modalFeatureId").value = String(featureId || "");
    query("modalFeatureName").value = featureName || "";
    query("modalDescription").value = "";
    saveModalInstance.show();
}

export function closeSaveModal() {
    if (!saveModalInstance) return;
    saveModalInstance.hide();
}

export function openViewModal(detail = {}) {
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

        const metaPairs = extractMetadataPairs(saved);
        if (metaPairs.length) {
            const html = metaPairs.map(p => `<div><strong>${PortfolioDom.escapeHtml(p[0])}:</strong> ${PortfolioDom.escapeHtml(String(p[1]))}</div>`).join("");
            query("viewSavedMeta").innerHTML = html;
        } else {
            query("viewSavedMeta").innerHTML = "";
        }

        const zoomBtn = query("btnViewZoom");
        if (zoomBtn) {
            zoomBtn.dataset.featureId = String(saved.FeatureId || saved.featureId || "");
        }

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
            if (viewModalInstance) viewModalInstance.hide();
        };
    }
    viewModalInstance.show();
}

export function closeViewModal() {
    if (!viewModalInstance) return;
    viewModalInstance.hide();
}
