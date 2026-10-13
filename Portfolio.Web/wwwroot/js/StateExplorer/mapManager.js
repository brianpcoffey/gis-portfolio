// Responsible for map initialization and rendering. Reads state but does not touch DOM.

import { getState, subscribe } from './stateStore.js';

let EsriMapClass, MapView, Graphic, GraphicsLayer;
let map, view, graphicsLayer, highlightLayer;

const graphicsById = new Map(); // featureId => Graphic

let clickHandle = null;
let hoverHandle = null;

// The Esri widget theme (light/dark) is loaded by esri-theme.js; start it before the map exists.
window.PortfolioEsri.loadTheme();

// Reads a CSS token as [r, g, b]. Handles "#rrggbb" and the "r, g, b" channel tokens (--status-*-rgb).
function tokenRgb(name) {
    const raw = getComputedStyle(document.body).getPropertyValue("--" + name).trim();
    const hex = /^#([0-9a-f]{6})$/i.exec(raw);
    if (hex) {
        const n = parseInt(hex[1], 16);
        return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
    }
    const parts = raw.split(",").map(s => parseInt(s, 10));
    return parts.length === 3 && parts.every(Number.isFinite) ? parts : [0, 0, 0];
}

// Symbols used for different states, drawn from the brand and status tokens (D-09c) so the
// map matches the rest of the page: saved = success, selected = info, comparing = warning.
// The polygons sit on the Esri basemap, which stays light in both themes, so the neutral comes
// from the theme-independent --map-muted (the status *-rgb tokens are theme-independent too).
function buildSymbols() {
    const muted = tokenRgb("map-muted");
    const fill = (rgb, alpha, width) => ({
        type: "simple-fill",
        color: [...rgb, alpha],
        outline: { color: rgb, width }
    });
    return {
        default: { type: "simple-fill", color: [...muted, 0.1], outline: { color: muted, width: 1 } },
        saved: fill(tokenRgb("status-success-rgb"), 0.4, 2),
        selected: fill(tokenRgb("status-info-rgb"), 0.5, 3),
        comparing: fill(tokenRgb("status-warning-rgb"), 0.4, 3),
        hover: fill(muted, 0.3, 2)
    };
}

// None of the symbol colours depend on the theme, so they are read once (five getComputedStyle
// reads) and reused for every state change and hover.
let symbolCache = null;
function mapSymbols() {
    return symbolCache || (symbolCache = buildSymbols());
}

function symbolFor(featureId, state, symbols) {
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

        // Load the ArcGIS API (and its theme) through the shared loader.
        await window.PortfolioEsri.load();

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

                    this.view = view;
                    this.map = map;

                    // ArcGIS nests its own role="application" surface in the container; name that one.
                    view.when(() => {
                        const surface = view.container.querySelector(".esri-view-surface");
                        const label = document.getElementById("mapView")?.dataset.mapLabel;
                        if (surface && label) surface.setAttribute("aria-label", label);
                    });

                    subscribe(state => {
                        this.renderFeatures(state);
                    });

                    resolve();

                }, reject);
        });
    },


    renderFeatures(stateSnapshot) {

        const state = stateSnapshot || getState();
        const symbols = mapSymbols();

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


            const sym = symbolFor(id, state, symbols);


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

        if (!highlightLayer) return; // no map (the ArcGIS API failed to load)

        highlightLayer.removeAll();

        if (!featureId) return;

        const g = graphicsById.get(String(featureId));

        if (!g) return;


        const hoverGraphic = new Graphic({

            geometry: g.geometry,

            symbol: mapSymbols().hover

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