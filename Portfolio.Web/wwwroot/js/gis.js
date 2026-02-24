// ArcGIS JS API and feature management for GIS Explorer

window.require([
    "esri/Map",
    "esri/views/MapView",
    "esri/Graphic",
    "esri/layers/GraphicsLayer"
], function (Map, MapView, Graphic, GraphicsLayer) {
    const layerId = "3"; // Example layer, adjust as needed
    const graphicsLayer = new GraphicsLayer();
    const map = new Map({ basemap: "streets-navigation-vector", layers: [graphicsLayer] });
    const view = new MapView({
        container: "mapView",
        map: map,
        center: [-98, 39], // USA center
        zoom: 4
    });

    let allFeatures = [];

    // Fetch features from backend API
    async function fetchFeatures(filter = "") {
        const res = await fetch(`/api/features?layerId=${layerId}`);
        const features = await res.json();
        allFeatures = features;
        let filtered = features;
        if (filter) {
            filtered = features.filter(f => f.name && f.name.toLowerCase().includes(filter.toLowerCase()));
        }
        renderFeatureList(filtered);
        renderMapFeatures(filtered);
    }

    // Render feature list with Save buttons
    function renderFeatureList(features) {
        const list = document.getElementById("featureList");
        if (!features.length) {
            list.innerHTML = "<div class='alert alert-info'>No features found.</div>";
            return;
        }
        list.innerHTML = features.map(f => `
            <div class="card mb-2">
                <div class="card-body d-flex justify-content-between align-items-center">
                    <span>${f.name}</span>
                    <button class="btn btn-primary btn-sm" onclick='window.saveFeature(${JSON.stringify(f)})'>Save Feature</button>
                </div>
            </div>
        `).join("");
    }

    // Render features on the map
    function renderMapFeatures(features) {
        graphicsLayer.removeAll();
        features.forEach(f => {
            let geometry;
            try {
                geometry = JSON.parse(f.geometryJson);
            } catch {
                return;
            }
            let symbol;
            if (geometry.x !== undefined && geometry.y !== undefined) {
                // Point
                symbol = {
                    type: "simple-marker",
                    color: "blue",
                    size: 8
                };
            } else if (geometry.rings) {
                // Polygon
                symbol = {
                    type: "simple-fill",
                    color: [51, 153, 255, 0.3],
                    outline: { color: "blue", width: 2 }
                };
            } else {
                return;
            }
            const graphic = new Graphic({
                geometry: geometry,
                symbol: symbol,
                attributes: { name: f.name, featureId: f.featureId }
            });
            graphicsLayer.add(graphic);
        });
    }

    // Map click popup
    view.on("click", function (event) {
        view.hitTest(event).then(function (response) {
            const graphic = response.results.find(r => r.graphic && r.graphic.layer === graphicsLayer)?.graphic;
            if (graphic) {
                view.popup.open({
                    title: graphic.attributes.name,
                    content: `<button class="btn btn-primary btn-sm" onclick="window.saveFeatureFromPopup('${graphic.attributes.featureId}')">Save Feature</button>`,
                    location: event.mapPoint
                });
            }
        });
    });

    // Save feature via API
    window.saveFeature = async function (feature) {
        const res = await fetch("/api/savedfeatures", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(feature)
        });
        if (res.ok) {
            alert("Feature saved!");
        } else {
            alert("Failed to save feature.");
        }
    };

    // Save feature from popup
    window.saveFeatureFromPopup = function (featureId) {
        const feature = allFeatures.find(f => f.featureId === featureId);
        if (feature) window.saveFeature(feature);
    };

    // Filter input
    document.getElementById("featureFilter").addEventListener("input", e => {
        fetchFeatures(e.target.value);
    });

    // Initial load
    fetchFeatures();
});