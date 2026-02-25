require([
    "esri/Map",
    "esri/views/MapView",
    "esri/Graphic",
    "esri/layers/GraphicsLayer",
    "esri/geometry/geometryEngine"
], function (EsriMap, MapView, Graphic, GraphicsLayer, geometryEngine) {

    /* ===================== STATE ===================== */
    const LAYER_ID = "3";
    let allFeatures = [];
    let savedFeaturesMap = new window.Map(); // Use window.Map to explicitly get JavaScript's Map
    let selectedFeatureId = null;
    let compareSet = new Set();
    let isCompareMode = false;
    let collections = [
        { id: 1, name: "Favorites", color: "#dc3545" },
        { id: 2, name: "Research", color: "#0d6efd" },
        { id: 3, name: "To Visit", color: "#198754" }
    ];

    /* ===================== MAP SETUP ===================== */
    const graphicsLayer = new GraphicsLayer();
    const highlightLayer = new GraphicsLayer();

    const map = new EsriMap({
        basemap: "streets-navigation-vector",
        layers: [graphicsLayer, highlightLayer]
    });

    let view; // Declare here, initialize after DOM ready

    /* ===================== SYMBOLS ===================== */
    const symbols = {
        default: {
            type: "simple-fill",
            color: [100, 100, 100, 0.1],
            outline: { color: [80, 80, 80], width: 1 }
        },
        saved: {
            type: "simple-fill",
            color: [40, 167, 69, 0.4],
            outline: { color: "#28a745", width: 2 }
        },
        selected: {
            type: "simple-fill",
            color: [13, 110, 253, 0.5],
            outline: { color: "#0d6efd", width: 3 }
        },
        comparing: {
            type: "simple-fill",
            color: [255, 193, 7, 0.4],
            outline: { color: "#ffc107", width: 3 }
        },
        hover: {
            type: "simple-fill",
            color: [108, 117, 125, 0.3],
            outline: { color: "#6c757d", width: 2 }
        }
    };

    /* ===================== INITIALIZATION ===================== */
    async function initialize() {
        // Create MapView only after DOM is ready
        view = new MapView({
            container: "mapView",
            map: map,
            center: [-98, 39],
            zoom: 4
        });

        await loadSavedFeatures();
        await fetchFeatures();
        renderCollections();
        setupEventListeners();
        updateStats();
    }

    /* ===================== API CALLS ===================== */
    async function fetchFeatures(filter = "") {
        try {
            const res = await fetch(`/api/features?layerId=${LAYER_ID}`);
            if (!res.ok) throw new Error("Failed to fetch features");
            const features = await res.json();
            allFeatures = features;

            let filtered = features;
            if (filter) {
                filtered = features.filter(f =>
                    f.name.toLowerCase().includes(filter.toLowerCase())
                );
            }
            renderMap(filtered);
            updateStats();
        } catch (err) {
            showToast("Error", "Failed to load features", "danger");
        }
    }

    async function loadSavedFeatures() {
        try {
            const res = await fetch("/api/savedfeatures");
            if (!res.ok) throw new Error("Failed to load saved features");
            const features = await res.json();
            savedFeaturesMap.clear();
            features.forEach(f => savedFeaturesMap.set(f.featureId, f));
            renderSavedFeaturesList();
        } catch (err) {
            console.error("Error loading saved features:", err);
        }
    }

    async function saveFeatureToApi(feature, description = "") {
        const payload = {
            layerId: LAYER_ID,
            featureId: feature.featureId,
            name: feature.name,
            geometryJson: feature.geometryJson,
            description: description
        };

        const res = await fetch("/api/savedfeatures", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (res.status === 409) {
            throw new Error("Feature already saved");
        }
        if (!res.ok) throw new Error("Failed to save feature");

        return await res.json();
    }

    async function deleteFeatureFromApi(id) {
        const res = await fetch(`/api/savedfeatures/${id}`, { method: "DELETE" });
        if (!res.ok && res.status !== 404) throw new Error("Failed to delete");
    }

    async function updateFeatureInApi(dto) {
        const res = await fetch(`/api/savedfeatures/${dto.id}`, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(dto)
        });
        if (!res.ok) throw new Error("Failed to update feature");
        return await res.json();
    }

    /* ===================== MAP RENDERING ===================== */
    function renderMap(features) {
        graphicsLayer.removeAll();

        features.forEach(f => {
            let geometry;
            try {
                geometry = JSON.parse(f.geometryJson);
            } catch {
                return;
            }
            geometry.type = "polygon";

            const isSaved = savedFeaturesMap.has(f.featureId);
            const isSelected = selectedFeatureId === f.featureId;
            const isComparing = compareSet.has(f.featureId);

            let symbol;
            if (isSelected) symbol = { ...symbols.selected };
            else if (isComparing) symbol = { ...symbols.comparing };
            else if (isSaved) symbol = { ...symbols.saved };
            else symbol = { ...symbols.default };

            const graphic = new Graphic({
                geometry: geometry,
                symbol: symbol,
                attributes: { ...f, isSaved, isSelected, isComparing }
            });
            graphicsLayer.add(graphic);
        });
    }

    /* ===================== EVENT HANDLERS ===================== */
    function setupEventListeners() {
        // Search
        document.getElementById("searchInput").addEventListener("input", e => {
            fetchFeatures(e.target.value);
        });

        // Basemap change
        document.getElementById("basemapSelect").addEventListener("change", e => {
            map.basemap = e.target.value;
        });

        // Compare mode toggle
        document.getElementById("btnCompareMode").addEventListener("click", () => {
            isCompareMode = !isCompareMode;
            const btn = document.getElementById("btnCompareMode");
            btn.classList.toggle("active", isCompareMode);
            btn.classList.toggle("btn-warning", isCompareMode);
            btn.classList.toggle("btn-outline-warning", !isCompareMode);
            showToast("Compare Mode", isCompareMode ? "Click features to add to comparison" : "Compare mode disabled");
            if (!isCompareMode) {
                compareSet.clear();
                document.getElementById("comparisonPanel").classList.add("d-none");
            }
            renderMap(allFeatures);
            updateStats();
        });

        // Clear selection
        document.getElementById("btnClearSelection").addEventListener("click", () => {
            selectedFeatureId = null;
            compareSet.clear();
            isCompareMode = false;
            document.getElementById("btnCompareMode").classList.remove("active", "btn-warning");
            document.getElementById("btnCompareMode").classList.add("btn-outline-warning");
            document.getElementById("comparisonPanel").classList.add("d-none");
            renderMap(allFeatures);
            renderFeatureDetails(null);
            updateStats();
        });

        // Export
        document.getElementById("btnExport").addEventListener("click", exportData);

        // New collection
        document.getElementById("btnNewCollection").addEventListener("click", () => {
            const name = prompt("Enter collection name:");
            if (name) {
                collections.push({
                    id: Date.now(),
                    name: name,
                    color: `#${Math.floor(Math.random() * 16777215).toString(16)}`
                });
                renderCollections();
                showToast("Success", `Collection "${name}" created`);
            }
        });

        // Confirm save
        document.getElementById("btnConfirmSave").addEventListener("click", async () => {
            const featureId = document.getElementById("modalFeatureId").value;
            const description = document.getElementById("modalDescription").value;
            const feature = allFeatures.find(f => f.featureId === featureId);

            if (!feature) return;

            try {
                const saved = await saveFeatureToApi(feature, description);
                savedFeaturesMap.set(saved.featureId, saved);
                renderMap(allFeatures);
                renderSavedFeaturesList();
                updateStats();
                bootstrap.Modal.getInstance(document.getElementById("saveFeatureModal")).hide();
                showToast("Saved", `"${feature.name}" has been saved`);
            } catch (err) {
                showToast("Error", err.message, "danger");
            }
        });

        // Close comparison
        document.getElementById("btnCloseComparison").addEventListener("click", () => {
            document.getElementById("comparisonPanel").classList.add("d-none");
        });

        // Map click
        view.on("click", handleMapClick);

        // Map hover
        view.on("pointer-move", handleMapHover);
    }

    async function handleMapClick(event) {
        const hit = await view.hitTest(event);
        const result = hit.results.find(r => r.graphic.layer === graphicsLayer);

        if (!result) {
            selectedFeatureId = null;
            renderFeatureDetails(null);
            renderMap(allFeatures);
            return;
        }

        const feature = result.graphic.attributes;

        if (isCompareMode) {
            if (compareSet.has(feature.featureId)) {
                compareSet.delete(feature.featureId);
            } else {
                compareSet.add(feature.featureId);
            }
            renderMap(allFeatures);
            updateStats();
            if (compareSet.size >= 2) {
                showComparison();
            }
            return;
        }

        selectedFeatureId = feature.featureId;
        renderMap(allFeatures);
        renderFeatureDetails(feature);

        // Zoom to feature
        const graphic = result.graphic;
        view.goTo({ target: graphic, zoom: 6 }, { duration: 500 });
    }

    let lastHoverFeatureId = null;
    async function handleMapHover(event) {
        const hit = await view.hitTest(event);
        const result = hit.results.find(r => r.graphic.layer === graphicsLayer);

        highlightLayer.removeAll();

        if (!result) {
            lastHoverFeatureId = null;
            view.container.style.cursor = "default";
            return;
        }

        view.container.style.cursor = "pointer";
        const feature = result.graphic.attributes;

        if (feature.featureId === selectedFeatureId || feature.featureId === lastHoverFeatureId) {
            return;
        }

        lastHoverFeatureId = feature.featureId;

        // Add hover highlight
        const hoverGraphic = new Graphic({
            geometry: result.graphic.geometry,
            symbol: symbols.hover
        });
        highlightLayer.add(hoverGraphic);
    }

    /* ===================== UI RENDERING ===================== */
    function renderFeatureDetails(feature) {
        const container = document.getElementById("featureDetails");

        if (!feature) {
            container.innerHTML = `<p class="text-muted text-center small">Click a feature on the map to view details</p>`;
            return;
        }

        const isSaved = savedFeaturesMap.has(feature.featureId);
        const savedData = savedFeaturesMap.get(feature.featureId);

        container.innerHTML = `
            <h5 class="mb-3">${feature.name}</h5>
            <div class="mb-2">
                <span class="badge ${isSaved ? 'bg-success' : 'bg-secondary'}">${isSaved ? '✓ Saved' : 'Not Saved'}</span>
            </div>
            <table class="table table-sm">
                <tr><th>Feature ID</th><td>${feature.featureId}</td></tr>
                <tr><th>Layer ID</th><td>${LAYER_ID}</td></tr>
                ${savedData?.dateSaved ? `<tr><th>Saved On</th><td>${new Date(savedData.dateSaved).toLocaleDateString()}</td></tr>` : ''}
            </table>
            ${savedData?.description ? `
                <div class="alert alert-light note-item">
                    <small class="text-muted">Notes:</small><br>
                    ${savedData.description}
                </div>
            ` : ''}
            <div class="d-grid gap-2">
                ${!isSaved ? `
                    <button class="btn btn-success btn-sm" onclick="window.gisApp.openSaveModal('${feature.featureId}')">
                        💾 Save Feature
                    </button>
                ` : `
                    <button class="btn btn-outline-danger btn-sm" onclick="window.gisApp.removeFeature('${feature.featureId}')">
                        🗑️ Remove from Saved
                    </button>
                `}
                <button class="btn btn-outline-primary btn-sm" onclick="window.gisApp.addToCompare('${feature.featureId}')">
                    📊 Add to Comparison
                </button>
            </div>
        `;
    }

    function renderSavedFeaturesList() {
        const container = document.getElementById("savedFeaturesList");
        const count = document.getElementById("savedCount");

        count.textContent = savedFeaturesMap.size;

        if (savedFeaturesMap.size === 0) {
            container.innerHTML = `<div class="list-group-item text-muted text-center small">No saved features yet</div>`;
            return;
        }

        container.innerHTML = Array.from(savedFeaturesMap.values()).map(f => `
            <div class="list-group-item feature-card ${selectedFeatureId === f.featureId ? 'selected' : ''}" 
                 onclick="window.gisApp.selectFeature('${f.featureId}')">
                <div class="d-flex justify-content-between align-items-start">
                    <div>
                        <strong>${f.name}</strong>
                        <br><small class="text-muted">${new Date(f.dateSaved).toLocaleDateString()}</small>
                    </div>
                    <button class="btn btn-sm btn-outline-danger" onclick="event.stopPropagation(); window.gisApp.removeFeature('${f.featureId}')">×</button>
                </div>
            </div>
        `).join("");
    }

    function renderCollections() {
        const container = document.getElementById("collectionsList");
        container.innerHTML = collections.map(c => `
            <div class="list-group-item d-flex justify-content-between align-items-center">
                <span>
                    <span class="badge" style="background-color: ${c.color}">●</span>
                    ${c.name}
                </span>
            </div>
        `).join("");

        // Update modal dropdown
        const select = document.getElementById("modalCollection");
        select.innerHTML = `<option value="">-- None --</option>` +
            collections.map(c => `<option value="${c.id}">${c.name}</option>`).join("");
    }

    function updateStats() {
        document.getElementById("statTotal").textContent = allFeatures.length;
        document.getElementById("statSaved").textContent = savedFeaturesMap.size;
        document.getElementById("statSelected").textContent = selectedFeatureId ? 1 : 0;
        document.getElementById("statComparing").textContent = compareSet.size;
    }

    /* ===================== COMPARISON ===================== */
    function showComparison() {
        const panel = document.getElementById("comparisonPanel");
        const content = document.getElementById("comparisonContent");

        const comparing = allFeatures.filter(f => compareSet.has(f.featureId));

        content.innerHTML = `
            <table class="table table-sm table-striped">
                <thead>
                    <tr>
                        <th>Feature</th>
                        <th>Status</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    ${comparing.map(f => `
                        <tr>
                            <td><strong>${f.name}</strong></td>
                            <td>${savedFeaturesMap.has(f.featureId) ? '<span class="badge bg-success">Saved</span>' : '<span class="badge bg-secondary">Not Saved</span>'}</td>
                            <td>
                                <button class="btn btn-xs btn-outline-primary" onclick="window.gisApp.zoomTo('${f.featureId}')">Zoom</button>
                            </td>
                        </tr>
                    `).join("")}
                </tbody>
            </table>
            <div class="text-muted small">Comparing ${comparing.length} features</div>
        `;

        panel.classList.remove("d-none");
    }

    /* ===================== EXPORT ===================== */
    function exportData() {
        const data = {
            exportDate: new Date().toISOString(),
            savedFeatures: Array.from(savedFeaturesMap.values()),
            collections: collections
        };

        const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `gis-export-${new Date().toISOString().split('T')[0]}.json`;
        a.click();
        URL.revokeObjectURL(url);

        showToast("Export", "Data exported successfully");
    }

    /* ===================== UTILITIES ===================== */
    function showToast(title, message, type = "primary") {
        const toast = document.getElementById("toast");
        document.getElementById("toastTitle").textContent = title;
        document.getElementById("toastMessage").textContent = message;
        toast.classList.remove("bg-danger", "bg-success", "bg-primary");
        if (type === "danger") toast.classList.add("text-bg-danger");
        new bootstrap.Toast(toast).show();
    }

    /* ===================== GLOBAL API ===================== */
    window.gisApp = {
        selectFeature: (featureId) => {
            selectedFeatureId = featureId;
            const feature = allFeatures.find(f => f.featureId === featureId);
            renderMap(allFeatures);
            renderSavedFeaturesList();
            renderFeatureDetails(feature || savedFeaturesMap.get(featureId));

            if (feature) {
                const graphic = graphicsLayer.graphics.find(g => g.attributes.featureId === featureId);
                if (graphic) {
                    view.goTo({ target: graphic, zoom: 6 }, { duration: 500 });
                }
            }
        },

        openSaveModal: (featureId) => {
            const feature = allFeatures.find(f => f.featureId === featureId);
            if (!feature) return;

            document.getElementById("modalFeatureId").value = featureId;
            document.getElementById("modalFeatureName").value = feature.name;
            document.getElementById("modalDescription").value = "";

            new bootstrap.Modal(document.getElementById("saveFeatureModal")).show();
        },

        removeFeature: async (featureId) => {
            const saved = savedFeaturesMap.get(featureId);
            if (!saved) return;

            if (!confirm(`Remove "${saved.name}" from saved features?`)) return;

            try {
                await deleteFeatureFromApi(saved.id);
                savedFeaturesMap.delete(featureId);
                renderMap(allFeatures);
                renderSavedFeaturesList();
                updateStats();
                if (selectedFeatureId === featureId) {
                    renderFeatureDetails(allFeatures.find(f => f.featureId === featureId));
                }
                showToast("Removed", `"${saved.name}" has been removed`);
            } catch (err) {
                showToast("Error", "Failed to remove feature", "danger");
            }
        },

        addToCompare: (featureId) => {
            compareSet.add(featureId);
            isCompareMode = true;
            document.getElementById("btnCompareMode").classList.add("active", "btn-warning");
            document.getElementById("btnCompareMode").classList.remove("btn-outline-warning");
            renderMap(allFeatures);
            updateStats();
            if (compareSet.size >= 2) {
                showComparison();
            }
            showToast("Compare", "Feature added to comparison");
        },

        zoomTo: (featureId) => {
            const graphic = graphicsLayer.graphics.find(g => g.attributes.featureId === featureId);
            if (graphic) {
                view.goTo({ target: graphic, zoom: 6 }, { duration: 500 });
            }
        }
    };

    /* ===================== START ===================== */
    // Wait for DOM to be ready before initializing
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize);
    } else {
        initialize();
    }
});