// Map pointer handlers: click selects (or toggles compare), hover highlights.
// registerMapHandlers() attaches them to the MapView (called from app.js after MapManager.init()).

import { getState, setSelectedFeature, toggleCompareFeature, MAX_COMPARE } from './stateStore.js';
import { MapManager } from './mapManager.js';
import { UI } from './uiManager.js';

async function handleMapClick(event) {

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

}


function handleMapHover(event) {

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

}


export function registerMapHandlers() {

    MapManager.onMapClick(handleMapClick);

    MapManager.onMapHover(handleMapHover);

}
