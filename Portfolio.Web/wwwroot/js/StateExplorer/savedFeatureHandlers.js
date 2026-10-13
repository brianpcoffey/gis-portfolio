// App-side handlers for the ui:* events about saved states: zoom, remove, select, open the save
// dialog and confirm the save (the only one that calls the API to create a record).
// registerSavedFeatureHandlers() attaches them to document (called from app.js).

import { getState, addSavedFeature, removeSavedFeatureById, setSelectedFeature } from './stateStore.js';
import { FeatureService } from './featureService.js';
import { MapManager } from './mapManager.js';
import { UI } from './uiManager.js';

function onZoomSaved(e) {

    MapManager.zoomToFeature(
        e.detail.featureId
    );

}


async function onRemoveSaved(e) {

    // Expects both savedId (db id) and featureId (external)
    const savedId = String(e.detail.savedId || "");
    const featureKey = String(e.detail.featureId || "");

    try {

        if (savedId) {
            // delete by saved DB id (API expects int id)
            await FeatureService.deleteFeature(savedId);
        } else {
            // fallback: if no savedId available, try deleting by feature key (may fail)
            await FeatureService.deleteFeature(featureKey);
        }

        // remove from client-state using the external feature id key
        if (featureKey) removeSavedFeatureById(featureKey);

        UI.showToast(
            "Remove",
            "Saved feature removed",
            "success"
        );

    }
    catch { }

}


function onSelectSaved(e) {

    setSelectedFeature(
        String(e.detail.featureId)
    );

}


function onOpenSaveModal(e) {

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


async function onConfirmSave(e) {

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


    const done = PortfolioUi.busy({ buttons: [btn] });


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

        done();

    }

}


export function registerSavedFeatureHandlers() {

    document.addEventListener("ui:zoomSaved", onZoomSaved);

    document.addEventListener("ui:removeSaved", onRemoveSaved);

    document.addEventListener("ui:selectSaved", onSelectSaved);

    document.addEventListener("ui:openSaveModal", onOpenSaveModal);

    document.addEventListener("ui:confirmSave", onConfirmSave);

}
