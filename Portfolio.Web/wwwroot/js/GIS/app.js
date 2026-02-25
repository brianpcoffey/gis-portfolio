// app.js
// Application orchestration: bootstraps map, wires events, coordinates FeatureService, stateStore and UI.

import {
    appState,
    setState,
    subscribe,
    updateFeatures,
    updateSavedFeatures,
    addSavedFeature,
    removeSavedFeatureById,
    setSelectedFeature,
    toggleCompareFeature,
    clearSelectionAndCompare,
    setCompareMode,
    updateCollections,
    getState,
    MAX_COMPARE
} from './stateStore.js';

import { FeatureService } from './featureService.js';
import { CollectionService } from './collectionService.js';
import { MapManager } from './mapManager.js';
import { UI } from './uiManager.js';


async function initialize() {

    UI.init();

    await MapManager.init();

    // Debug check (safe to leave in)
    console.log("MapManager.view =", MapManager.view);


    // Load data (includes collections from server)
    const [features, saved, collections] = await Promise.all([
        FeatureService.getFeatures(),
        FeatureService.getSavedFeatures(),
        CollectionService.getCollections()
    ]);

    updateFeatures(features);
    updateSavedFeatures(saved);
    updateCollections(collections);

    // State subscription
    subscribe(state => {

        UI.renderCollections();
        UI.renderSavedFeaturesList();
        UI.renderStats();
        UI.renderComparison();
        UI.renderCompareIndicator && UI.renderCompareIndicator();

    });


    // Toolbar Buttons

    document.getElementById("btnCompareMode")?.addEventListener("click", () => {

        const state = getState();
        setCompareMode(!state.compareMode);

    });


    document.getElementById("btnClearSelection")?.addEventListener("click", () => {

        clearSelectionAndCompare();

    });


    document.getElementById("btnExport")?.addEventListener("click", () => {

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

    });



    document.getElementById("btnNewCollection")?.addEventListener("click", async () => {

        const name = prompt("New collection name:");

        if (!name) return;

        try {

            const created = await CollectionService.createCollection(name.trim(), "#6c757d");

            // merge into state
            const state = getState();
            updateCollections([...(state.collections || []), created]);

            UI.showToast("Collection", "Collection created", "success");
        }
        catch (err) {
            console.error("create collection failed", err);
            UI.showToast("Collection", "Failed to create collection", "danger");
        }

    });



    document.getElementById("basemapSelect")?.addEventListener("change", (e) => {

        MapManager.setBasemap(e.target.value);

    });



    // MAP CLICK HANDLER (SAFE)

    MapManager.onMapClick(async (event) => {

        try {

            // Prevent hitTest crash
            if (!MapManager.view) return;

            if (!MapManager.view.hitTest) return;


            const hit = await MapManager.view.hitTest(event);

            let graphic = null;


            if (hit?.results?.length) {

                graphic = hit.results.find(r =>

                    r.graphic &&
                    r.graphic.attributes &&
                    r.graphic.attributes.featureId

                )?.graphic;

            }


            if (!graphic) return;


            const fid =
                String(graphic.attributes.featureId);


            const state = getState();


            if (state.compareMode) {

                // Enforce maximum compare limit when adding (but allow toggling off)
                const idStr = String(fid);
                const already = state.compareSet.has(idStr);
                if (!already && (state.compareSet.size >= MAX_COMPARE)) {
                    UI.showToast("Compare", `Cannot compare more than ${MAX_COMPARE} features.`, "danger");
                    return;
                }
                toggleCompareFeature(fid);

            }
            else {

                setSelectedFeature(fid);

            }

        }
        catch (e) {

            console.error(
                "Map click handler error",
                e
            );

        }

    });



    // MAP HOVER HANDLER (SAFE)

    MapManager.onMapHover((event) => {

        try {

            if (!MapManager.view) return;

            if (!MapManager.view.hitTest) return;


            MapManager.view.hitTest(event)

                .then(res => {

                    const graphic =
                        res.results?.find(r =>

                            r.graphic &&
                            r.graphic.attributes &&
                            r.graphic.attributes.featureId

                        )?.graphic;


                    if (graphic) {

                        MapManager.highlightFeature(

                            String(
                                graphic.attributes.featureId
                            )

                        );

                    }
                    else {

                        MapManager.highlightFeature(null);

                    }

                })

                .catch(e =>
                    console.error(
                        "hover hitTest",
                        e
                    )
                );

        }
        catch (e) {

            console.error(
                "Hover handler error",
                e
            );

        }

    });



    // UI Events

    document.addEventListener(
        "ui:zoomSaved",
        e => {

            MapManager.zoomToFeature(
                e.detail.featureId
            );

        }
    );



    document.addEventListener(
        "ui:removeSaved",
        async e => {

            const id =
                String(e.detail.featureId);

            try {

                await FeatureService.deleteFeature(id);

                removeSavedFeatureById(id);

                UI.showToast(
                    "Remove",
                    "Saved feature removed",
                    "success"
                );

            }
            catch { }

        }
    );



    document.addEventListener(
        "ui:selectSaved",
        e => {

            setSelectedFeature(
                String(e.detail.featureId)
            );

        }
    );



    document.addEventListener(
        "ui:selectCollection",
        e => {

            setState({

                selectedCollectionId:
                    e.detail.collectionId
                        ? String(e.detail.collectionId)
                        : null

            });

        }
    );



    document.addEventListener(
        "ui:openSaveModal",
        e => {

            const fid =
                String(e.detail.featureId);

            const feature =
                e.detail.feature ||
                getState().features.find(
                    f =>
                        String(
                            f.featureId ||
                            f.FeatureId
                        ) === fid
                );


            const name =
                feature?.displayName ||
                feature?.Name ||
                feature?.name ||
                `State ${fid}`;


            UI.openSaveModal(fid, name);

        }
    );



    document.addEventListener(
        "ui:confirmSave",
        async e => {

            const {
                featureId,
                description,
                collectionId
            } = e.detail;


            const state =
                getState();


            const feature =
                state.features.find(
                    f =>
                        String(
                            f.featureId ||
                            f.FeatureId
                        ) ===
                        String(featureId)
                );


            if (!feature) {

                UI.showToast(
                    "Error",
                    "Feature not found",
                    "danger"
                );

                return;

            }


            const btn =
                document.getElementById(
                    "btnConfirmSave"
                );


            if (btn)
                btn.disabled = true;


            try {

                const saved =
                    await FeatureService.saveFeature(
                        feature,
                        description,
                        collectionId
                    );


                const normalized = {

                    ...saved,

                    featureId: String(

                        saved.featureId ||
                        saved.FeatureId ||
                        feature.featureId ||
                        feature.FeatureId

                    )

                };


                addSavedFeature(normalized);


                UI.showToast(
                    "Saved",
                    "Feature saved successfully",
                    "success"
                );


                UI.closeSaveModal();

            }
            catch (err) {

                console.error(
                    "save failed",
                    err
                );

            }
            finally {

                if (btn)
                    btn.disabled = false;

            }

        }
    );

    // New UI -> App event handlers (compare panel)
    document.addEventListener("ui:zoomCompare", e => {
        MapManager.zoomToFeature(e.detail.featureId);
    });

    document.addEventListener("ui:toggleCompare", e => {
        // enforce max when toggled from the UI controls as well
        const state = getState();
        const idStr = String(e.detail.featureId);
        const already = state.compareSet.has(idStr);
        if (!already && (state.compareSet.size >= MAX_COMPARE)) {
            UI.showToast("Compare", `Cannot compare more than ${MAX_COMPARE} features.`, "danger");
            return;
        }
        toggleCompareFeature(e.detail.featureId);
    });

    document.addEventListener("ui:closeComparison", () => {
        // Clear compare mode and set compare set empty
        clearSelectionAndCompare();
    });



    document.addEventListener(
        "ui:search",
        e => {

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
    );



    subscribe(state => {

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

    });



    // Initial render

    UI.renderCollections();
    UI.renderSavedFeaturesList();
    UI.renderStats();
    UI.renderComparison();
    UI.renderCompareIndicator && UI.renderCompareIndicator();

}



if (document.readyState === "loading")
    document.addEventListener(
        "DOMContentLoaded",
        initialize
    );
else
    initialize();