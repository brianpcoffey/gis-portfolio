// Toolbar controls: Compare Mode, Clear selection, Export, "+ New" collection and the basemap select.
// Each handler is a named function; registerToolbarHandlers() attaches them (called from app.js).

import { getState, setCompareMode, clearSelectionAndCompare, updateCollections } from './stateStore.js';
import { CollectionService } from './collectionService.js';
import { MapManager } from './mapManager.js';
import { UI } from './uiManager.js';

function onCompareModeClick() {

    const state = getState();
    setCompareMode(!state.compareMode);

}


function onClearSelectionClick() {

    clearSelectionAndCompare();

}


function onExportClick() {

    const state = getState();

    const data = Array.from(state.savedFeatures.values());

    const blob = new Blob(
        [JSON.stringify(data, null, 2)],
        { type: 'application/json' }
    );

    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');

    a.href = url;
    a.download = 'saved-features.json';

    document.body.appendChild(a);
    a.click();
    a.remove();

    URL.revokeObjectURL(url);

    UI.showToast("Export", "Saved features exported", "success");

}


async function onNewCollectionClick() {

    if (!UI.isCollectionsSignedIn()) {
        UI.showToast("Collection", "Sign in to create collections.", "info");
        return;
    }

    const input = await UI.openCollectionModal({ title: "New Collection", color: UI.DEFAULT_COLLECTION_COLOR });

    if (!input) return;

    try {

        const created = await CollectionService.createCollection(input.name, input.color);

        const state = getState();
        updateCollections([...(state.collections || []), created]);

        UI.showToast("Collection", "Collection created", "success");
    }
    catch (err) {
        console.error("create collection failed", err);
        UI.showToast("Collection", "Failed to create collection", "danger");
    }

}


function onBasemapChange(e) {

    MapManager.setBasemap(e.target.value);

}


export function registerToolbarHandlers() {

    document.getElementById("btnCompareMode")?.addEventListener("click", onCompareModeClick);

    document.getElementById("btnClearSelection")?.addEventListener("click", onClearSelectionClick);

    document.getElementById("btnExport")?.addEventListener("click", onExportClick);

    document.getElementById("btnNewCollection")?.addEventListener("click", onNewCollectionClick);

    document.getElementById("basemapSelect")?.addEventListener("change", onBasemapChange);

}
