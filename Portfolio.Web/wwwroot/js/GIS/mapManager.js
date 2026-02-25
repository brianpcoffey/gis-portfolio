// mapManager.js
import { appState } from './stateStore.js';

let map, view, graphicsLayer, highlightLayer;

const symbols = {
    default: { type: "simple-fill", color: [100, 100, 100, 0.1], outline: { color: [80, 80, 80], width: 1 } },
    saved: { type: "simple-fill", color: [40, 167, 69, 0.4], outline: { color: "#28a745", width: 2 } },
    selected: { type: "simple-fill", color: [13, 110, 253, 0.5], outline: { color: "#0d6efd", width: 3 } },
    comparing: { type: "simple-fill", color: [255, 193, 7, 0.4], outline: { color: "#ffc107", width: 3 } },
    hover: { type: "simple-fill", color: [108, 117, 125, 0.3], outline: { color: "#6c757d", width: 2 } }
};

export const MapManager = {
    async init() {
        return new Promise((resolve, reject) => {
            window.require([
                "esri/Map", "esri/views/MapView", "esri/Graphic", "esri/layers/GraphicsLayer"
            ], (Map, MapView, Graphic, GraphicsLayer) => {
                graphicsLayer = new GraphicsLayer();
                highlightLayer = new GraphicsLayer();
                map = new Map({
                    basemap: "streets-navigation-vector",
                    layers: [graphicsLayer, highlightLayer]
                });
                view = new MapView({
                    container: "mapView",
                    map: map,
                    center: [-98, 39],
                    zoom: 4
                });
                this.Graphic = Graphic;
                this.view = view;
                this.graphicsLayer = graphicsLayer;
                this.highlightLayer = highlightLayer;
                resolve();
            });
        });
    },
    renderFeatures() {
        graphicsLayer.removeAll();
        appState.features.forEach(f => {
            let geometry;
            try { geometry = JSON.parse(f.geometryJson || f.GeometryJson); geometry.type = "polygon"; } catch { return; }
            const isSaved = appState.savedFeatures.has(f.featureId);
            const isSelected = appState.selectedFeatureId === f.featureId;
            const isComparing = appState.compareSet.has(f.featureId);
            let symbol = isSelected ? symbols.selected : isComparing ? symbols.comparing : isSaved ? symbols.saved : symbols.default;
            const graphic = new this.Graphic({
                geometry,
                symbol,
                attributes: { ...f, isSaved, isSelected, isComparing }
            });
            graphicsLayer.add(graphic);
        });
    },
    highlightFeature(featureId) {
        highlightLayer.removeAll();
        const f = appState.features.find(f => f.featureId === featureId);
        if (!f) return;
        let geometry;
        try { geometry = JSON.parse(f.geometryJson || f.GeometryJson); geometry.type = "polygon"; } catch { return; }
        highlightLayer.add(new this.Graphic({ geometry, symbol: symbols.hover }));
    },
    zoomToFeature(featureId) {
        const g = graphicsLayer.graphics.find(g => g.attributes.featureId === featureId);
        if (g) view.goTo({ target: g, zoom: 6 }, { duration: 500 });
    },
    onMapClick(handler) {
        view.on("click", handler);
    },
    onMapHover(handler) {
        view.on("pointer-move", handler);
    },
    setBasemap(basemap) {
        map.basemap = basemap;
    }
};