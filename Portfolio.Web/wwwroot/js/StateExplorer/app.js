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


async function initialize() {

    UI.init();

    await MapManager.init();

    // Load data (includes collections from server)
    const [features, saved, collections] = await Promise.all([
        FeatureService.getFeatures(),
        FeatureService.getSavedFeatures(),
        CollectionService.getCollections()
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