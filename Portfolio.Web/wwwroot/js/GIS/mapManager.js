// mapManager.js
// Responsible for map initialization and rendering. Reads state but does not touch DOM.

import { getState, subscribe } from './stateStore.js';

let EsriMapClass, MapView, Graphic, GraphicsLayer;
let map, view, graphicsLayer, highlightLayer;

const graphicsById = new Map(); // featureId => Graphic

let clickHandle = null;
let hoverHandle = null;

// Symbols used for different states
const symbols = {
    default: { type: "simple-fill", color: [100, 100, 100, 0.1], outline: { color: [80, 80, 80], width: 1 } },
    saved: { type: "simple-fill", color: [40, 167, 69, 0.4], outline: { color: "#28a745", width: 2 } },
    selected: { type: "simple-fill", color: [13, 110, 253, 0.5], outline: { color: "#0d6efd", width: 3 } },
    comparing: { type: "simple-fill", color: [255, 193, 7, 0.4], outline: { color: "#ffc107", width: 3 } },
    hover: { type: "simple-fill", color: [108, 117, 125, 0.3], outline: { color: "#6c757d", width: 2 } }
};

function symbolFor(featureId, state) {
    if (state.selectedFeatureId === featureId) return symbols.selected;
    if (state.compareSet.has(featureId)) return symbols.comparing;
    if (state.savedFeatures.has(featureId)) return symbols.saved;
    return symbols.default;
}

export const MapManager = {

    // exported placeholders — will be assigned during init
    view: null,
    map: null,

    async init() {

        return new Promise((resolve, reject) => {

            window.require([
                "esri/Map",
                "esri/views/MapView",
                "esri/Graphic",
                "esri/layers/GraphicsLayer"
            ],

                (EsriMap, EsriMapView, EsriGraphic, EsriGraphicsLayer) => {

                    EsriMapClass = EsriMap;
                    MapView = EsriMapView;
                    Graphic = EsriGraphic;
                    GraphicsLayer = EsriGraphicsLayer;

                    graphicsLayer = new GraphicsLayer();
                    highlightLayer = new GraphicsLayer();

                    map = new EsriMapClass({
                        basemap: "streets-navigation-vector",
                        layers: [graphicsLayer, highlightLayer]
                    });

                    view = new MapView({
                        container: "mapView",
                        map: map,
                        center: [-98, 39],
                        zoom: 4
                    });

                    // expose to callers
                    this.view = view;
                    this.map = map;

                    // Subscribe to state changes
                    subscribe(state => {
                        this.renderFeatures(state);
                    });

                    resolve();

                }, reject);
        });
    },


    renderFeatures(stateSnapshot) {

        const state = stateSnapshot || getState();

        const currentIds = new Set(
            state.features.map(f => String(f.featureId || f.FeatureId))
        );


        // Remove missing graphics
        for (const id of Array.from(graphicsById.keys())) {

            if (!currentIds.has(id)) {

                const g = graphicsById.get(id);

                graphicsLayer.remove(g);

                graphicsById.delete(id);
            }
        }


        // Add or update graphics
        state.features.forEach(f => {

            const id = String(f.featureId || f.FeatureId);

            let geometry;

            try {

                geometry = JSON.parse(
                    f.geometryJson || f.GeometryJson
                );

            } catch {

                return;
            }

            geometry.type = geometry.type || "polygon";


            const sym = symbolFor(id, state);


            if (graphicsById.has(id)) {

                const g = graphicsById.get(id);

                g.geometry = geometry;

                g.symbol = sym;

                g.attributes = { ...f };

            }
            else {

                const g = new Graphic({

                    geometry: geometry,

                    symbol: sym,

                    attributes: { ...f, featureId: id }

                });

                graphicsLayer.add(g);

                graphicsById.set(id, g);
            }

        });

    },


    highlightFeature(featureId) {

        highlightLayer.removeAll();

        if (!featureId) return;

        const g = graphicsById.get(String(featureId));

        if (!g) return;


        const hoverGraphic = new Graphic({

            geometry: g.geometry,

            symbol: symbols.hover

        });


        highlightLayer.add(hoverGraphic);

    },


    zoomToFeature(featureId, options = { zoom: 6, duration: 500 }) {

        const g = graphicsById.get(String(featureId));

        if (g && this.view) {

            this.view.goTo(

                {
                    target: g,
                    zoom: options.zoom
                },

                {
                    duration: options.duration
                }

            ).catch(e => console.error("goTo failed", e));
        }
    },


    onMapClick(handler) {

        if (!this.view) return;

        if (clickHandle)
            clickHandle.remove();

        clickHandle = this.view.on("click", handler);

    },


    onMapHover(handler) {

        if (!this.view) return;

        if (hoverHandle)
            hoverHandle.remove();

        hoverHandle = this.view.on("pointer-move", handler);

    },


    setBasemap(basemap) {

        if (this.map)
            this.map.basemap = basemap;
    }

};