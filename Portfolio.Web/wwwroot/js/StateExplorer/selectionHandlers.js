// App-side handlers for the ui:* events about what is selected: choosing a collection, the
// comparison panel's zoom / toggle / close, and the debounced state search.
// registerSelectionHandlers() attaches them to document (called from app.js).

import { getState, setState, setSelectedFeature, toggleCompareFeature, clearSelectionAndCompare, MAX_COMPARE } from './stateStore.js';
import { MapManager } from './mapManager.js';
import { UI } from './uiManager.js';

function onSelectCollection(e) {

    setState({

        selectedCollectionId:
            e.detail.collectionId
                ? String(e.detail.collectionId)
                : null

    });

}


function onZoomCompare(e) {
    MapManager.zoomToFeature(e.detail.featureId);
}

function onToggleCompare(e) {
    // enforce max when toggled from the UI controls as well
    const state = getState();
    const idStr = String(e.detail.featureId);
    const already = state.compareSet.has(idStr);
    if (!already && (state.compareSet.size >= MAX_COMPARE)) {
        UI.showToast("Compare", `Cannot compare more than ${MAX_COMPARE} features.`, "danger");
        return;
    }
    toggleCompareFeature(e.detail.featureId);
}

function onCloseComparison() {
    clearSelectionAndCompare();
}


function onSearch(e) {

    const q =
        (e.detail.query || "")
            .trim()
            .toLowerCase();


    const state =
        getState();


    if (!q) {

        setSelectedFeature(null);

        return;

    }


    const match =
        state.features.find(f =>

            (
                f.displayName ||
                f.Name ||
                f.name ||
                ""
            )
                .toLowerCase()
                .includes(q)

        );


    if (match) {

        const id =
            String(
                match.featureId ||
                match.FeatureId
            );


        setSelectedFeature(id);

        MapManager.zoomToFeature(id);

    }
    else {

        UI.showToast(
            "Search",
            "No matching state found",
            "primary"
        );

    }

}


export function registerSelectionHandlers() {

    document.addEventListener("ui:selectCollection", onSelectCollection);

    document.addEventListener("ui:zoomCompare", onZoomCompare);

    document.addEventListener("ui:toggleCompare", onToggleCompare);

    document.addEventListener("ui:closeComparison", onCloseComparison);

    document.addEventListener("ui:search", onSearch);

}
