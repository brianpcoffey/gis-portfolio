// app.js
import { appState, setState, subscribe } from './stateStore.js';
import { FeatureService } from './featureService.js';
import { MapManager } from './mapManager.js';
import { UI } from './uiManager.js';

async function initialize() {
    await MapManager.init();

    // Load features and saved features
    const [features, savedFeatures] = await Promise.all([
        FeatureService.getFeatures(),
        FeatureService.getSavedFeatures()
    ]);
    setState({ features });

    // Initial render
    UI.renderCollections();
    UI.renderSavedFeaturesList();
    UI.renderStats();
    MapManager.renderFeatures();

    // Subscribe to state changes for re-render
    subscribe(() => {
        UI.renderSavedFeaturesList();
        UI.renderCollections();
        UI.renderStats();
        MapManager.renderFeatures();
    });

    // Event wiring
    document.getElementById("btnCompareMode").addEventListener("click", () => {
        setState({ compareMode: !appState.compareMode });
    });
    document.getElementById("btnClearSelection").addEventListener("click", () => {
        setState({ selectedFeatureId: null, compareSet: new Set(), compareMode: false });
    });
    document.getElementById("btnExport").addEventListener("click", () => {
        // Implement export logic
    });
    document.getElementById("btnNewCollection").addEventListener("click", () => {
        // Implement collection creation logic
    });
    document.getElementById("basemapSelect").addEventListener("change", (e) => {
        MapManager.setBasemap(e.target.value);
    });

    // Map events
    MapManager.onMapClick(async (event) => {
        // Implement map click logic: selection, compare, etc.
    });
    MapManager.onMapHover(async (event) => {
        // Implement map hover logic: highlight, etc.
    });

    // Modal save confirm
    document.getElementById("btnConfirmSave").addEventListener("click", async () => {
        // Implement save feature logic
    });

    // Debounced search input
    let searchTimeout;
    document.getElementById("searchInput").addEventListener("input", (e) => {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => {
            // Implement search logic
        }, 250);
    });
}

if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize);
else initialize();