// Application orchestration: bootstraps map, wires events, coordinates FeatureService, stateStore and UI.
// The event handlers live in toolbarHandlers.js, mapHandlers.js, savedFeatureHandlers.js and
// selectionHandlers.js; initialize() below is the ordered registration list.

import {
    subscribe,
    updateFeatures,
    updateSavedFeatures,
    updateCollections
} from './stateStore.js';

import { FeatureService } from './featureService.js';
import { CollectionService } from './collectionService.js';
import { MapManager } from './mapManager.js';
import { UI } from './uiManager.js';
import { registerToolbarHandlers } from './toolbarHandlers.js';
import { registerMapHandlers } from './mapHandlers.js';
import { registerSavedFeatureHandlers } from './savedFeatureHandlers.js';
import { registerSelectionHandlers } from './selectionHandlers.js';


function renderPanels() {

    UI.renderCollections();
    UI.renderSavedFeaturesList();
    UI.renderStats();
    UI.renderComparison();
    UI.renderCompareIndicator && UI.renderCompareIndicator();

}


function renderSelectedFeature(state) {

    const selectedId =
        state.selectedFeatureId;


    if (!selectedId) {

        UI.renderFeatureDetails(null);

        return;

    }


    const feature =
        state.features.find(
            f =>
                String(
                    f.featureId ||
                    f.FeatureId
                ) ===
                String(selectedId)
        );


    UI.renderFeatureDetails(
        feature || null
    );

}


// The map is optional for everything else on the page: if the ArcGIS API cannot load (network
// failure, the SRI pin blocking a changed file, or its modules not arriving within 15 s) say so in the
// map container and carry on, so the state search, saved states, comparison and collections still
// work (MapManager's zoom, hover and basemap helpers do nothing without a view).
async function initMapOrReport() {

    try {

        await MapManager.init();

    }
    catch (err) {

        console.error("Map failed to load", err);

        document.getElementById("basemapSelect")?.closest(".theme-toolbar")?.classList.add("d-none");

        window.PortfolioEsri.showLoadError(
            document.getElementById("mapView"),
            "State search, saved states, comparison and collections still work."
        );

    }

}


async function initialize() {

    UI.init();

    await initMapOrReport();

    // The server renders data-signed-in on the page root; only a signed-in visitor can use collections.
    // A missing attribute counts as signed in, so the 401 fallback in CollectionService still applies.
    const signedIn = document.querySelector(".state-explorer-page")?.dataset.signedIn !== "false";

    // Load data (includes collections from server)
    const [features, saved, collections] = await Promise.all([
        FeatureService.getFeatures(),
        FeatureService.getSavedFeatures(),
        CollectionService.getCollections({ signedIn })
    ]);

    updateFeatures(features);
    updateSavedFeatures(saved);
    updateCollections(collections);
    // Anonymous visitors stay on the page: collections are disabled with a sign-in hint.
    UI.setCollectionsSignedIn(!CollectionService.requiresSignIn);

    subscribe(renderPanels);

    registerToolbarHandlers();

    registerMapHandlers();

    registerSavedFeatureHandlers();

    registerSelectionHandlers();

    subscribe(renderSelectedFeature);

    renderPanels();

}



if (document.readyState === "loading")
    document.addEventListener(
        "DOMContentLoaded",
        initialize
    );
else
    initialize();