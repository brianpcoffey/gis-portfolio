// gis.js
require([
    "esri/Map",
    "esri/views/MapView",
    "esri/Graphic",
    "esri/layers/GraphicsLayer",
    "esri/geometry/geometryEngine"
], function (Map, MapView, Graphic, GraphicsLayer, geometryEngine) {

    /* ===================== STATE ===================== */
    const LAYER_ID = "3";
    let allFeatures = [];
    let savedFeaturesMap = new window.Map(); // JS Map for saved features
    let selectedFeatureId = null;
    let compareSet = new Set();
    let isCompareMode = false;
    let collections = [
        { id: 1, name: "Favorites", color: "#dc3545" },
        { id: 2, name: "Research", color: "#0d6efd" },
        { id: 3, name: "To Visit", color: "#198754" }
    ];
    let selectedCollectionId = null;

    /* ===================== MAP SETUP ===================== */
    const graphicsLayer = new GraphicsLayer();
    const highlightLayer = new GraphicsLayer();

    const map = new Map({
        basemap: "streets-navigation-vector",
        layers: [graphicsLayer, highlightLayer]
    });

    let view; // ArcGIS MapView

    /* ===================== SYMBOLS ===================== */
    const symbols = {
        default: { type: "simple-fill", color: [100, 100, 100, 0.1], outline: { color: [80, 80, 80], width: 1 } },
        saved: { type: "simple-fill", color: [40, 167, 69, 0.4], outline: { color: "#28a745", width: 2 } },
        selected: { type: "simple-fill", color: [13, 110, 253, 0.5], outline: { color: "#0d6efd", width: 3 } },
        comparing: { type: "simple-fill", color: [255, 193, 7, 0.4], outline: { color: "#ffc107", width: 3 } },
        hover: { type: "simple-fill", color: [108, 117, 125, 0.3], outline: { color: "#6c757d", width: 2 } }
    };

    /* ===================== DISPLAY NAME UTILITIES ===================== */
    function getDisplayName(feature) {
        if (!feature) return "Unknown State";
        const attrs = feature.attributes || {};
        if (attrs.STATE_NAME?.trim()) return attrs.STATE_NAME.trim();
        if (attrs.NAME?.trim()) return attrs.NAME.trim();
        if (attrs.STATE?.trim()) return attrs.STATE.trim();
        if (attrs.STATE_ABBR?.trim()) return attrs.STATE_ABBR.trim();
        if (feature.name?.trim()) return feature.name.trim();
        return feature.featureId ? `State ${feature.featureId}` : "Unknown State";
    }

    function cacheDisplayNames(features) {
        features.forEach(f => { if (!f.displayName) f.displayName = getDisplayName(f); });
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function renderMetadataTable(feature) {
        if (!feature || !feature.attributes) return '<p class="text-muted small">No metadata available</p>';
        const keys = Object.keys(feature.attributes).filter(k =>
            !['isSaved', 'isSelected', 'isComparing', 'displayName'].includes(k)
            && feature.attributes[k] !== null && feature.attributes[k] !== undefined
        );
        if (!keys.length) return '<p class="text-muted small">No metadata available</p>';
        const rows = keys.map(k => `<tr><td class="text-muted small">${escapeHtml(k)}</td><td class="small">${escapeHtml(typeof feature.attributes[k] === 'object' ? JSON.stringify(feature.attributes[k]) : String(feature.attributes[k]))}</td></tr>`).join('');
        return `<details class="mb-3"><summary class="text-muted small" style="cursor:pointer;">View Metadata</summary><table class="table table-sm table-bordered mt-2" style="font-size:0.75rem;"><thead class="table-light"><tr><th>Field</th><th>Value</th></tr></thead><tbody>${rows}</tbody></table></details>`;
    }

    /* ===================== UI RENDER HELPERS (ADDED) ===================== */
    function renderSavedFeaturesList() {
        const container = document.getElementById("savedFeaturesList");
        const countEl = document.getElementById("savedCount");
        if (!container) return;
        container.innerHTML = "";

        const entries = Array.from(savedFeaturesMap.values());
        if (!entries.length) {
            container.innerHTML = '<div class="list-group-item text-muted text-center small">No saved states yet</div>';
            if (countEl) countEl.textContent = "0";
            return;
        }

        entries.forEach(saved => {
            const item = document.createElement("div");
            item.className = "list-group-item d-flex justify-content-between align-items-center";
            const left = document.createElement("div");
            left.style.cursor = "pointer";
            left.textContent = saved.displayName || saved.name || `State ${saved.featureId}`;
            left.addEventListener("click", () => window.gisApp.zoomTo(saved.featureId));
            item.appendChild(left);

            const btnGroup = document.createElement("div");
            btnGroup.className = "btn-group btn-group-sm";

            const zoomBtn = document.createElement("button");
            zoomBtn.className = "btn btn-outline-secondary";
            zoomBtn.title = "Zoom";
            zoomBtn.innerHTML = '<i class="fa-solid fa-search-plus"></i>';
            zoomBtn.addEventListener("click", () => window.gisApp.zoomTo(saved.featureId));
            btnGroup.appendChild(zoomBtn);

            const removeBtn = document.createElement("button");
            removeBtn.className = "btn btn-outline-danger";
            removeBtn.title = "Remove";
            removeBtn.innerHTML = '<i class="fa-solid fa-trash"></i>';
            removeBtn.addEventListener("click", async () => {
                if (!confirm(`Remove "${saved.displayName || saved.name}" from saved states?`)) return;
                try {
                    await deleteFeatureFromApi(saved.id);
                    savedFeaturesMap.delete(saved.featureId);
                    renderSavedFeaturesList();
                    renderMap(allFeatures);
                    updateStats();
                    showToast("Removed", `"${saved.displayName || saved.name}" has been removed`);
                } catch (err) {
                    console.error(err);
                    showToast("Error", "Failed to remove state", "danger");
                }
            });
            btnGroup.appendChild(removeBtn);

            item.appendChild(btnGroup);
            container.appendChild(item);
        });

        if (countEl) countEl.textContent = String(entries.length);
    }

    function renderCollections() {
        const list = document.getElementById("collectionsList");
        const select = document.getElementById("modalCollection");
        if (list) list.innerHTML = "";
        if (select) {
            select.innerHTML = '<option value="">-- None --</option>';
        }

        collections.forEach(col => {
            if (list) {
                const item = document.createElement("button");
                item.type = "button";
                item.className = "list-group-item list-group-item-action d-flex justify-content-between align-items-center";
                item.innerHTML = `<span><span class="badge me-2" style="background:${col.color};width:10px;height:10px;border-radius:50%;display:inline-block;"></span>${escapeHtml(col.name)}</span>`;
                item.addEventListener("click", () => {
                    selectedCollectionId = col.id;
                    // simple visual selection
                    Array.from(list.children).forEach(ch => ch.classList.remove("active"));
                    item.classList.add("active");
                });
                list.appendChild(item);
            }

            if (select) {
                const opt = document.createElement("option");
                opt.value = String(col.id);
                opt.textContent = col.name;
                select.appendChild(opt);
            }
        });

        const btnNew = document.getElementById("btnNewCollection");
        if (btnNew) {
            btnNew.removeEventListener("click", handleNewCollectionClick);
            btnNew.addEventListener("click", handleNewCollectionClick);
        }
    }

    function handleNewCollectionClick() {
        const name = prompt("Collection name:");
        if (!name) return;
        const id = collections.length ? Math.max(...collections.map(c => c.id)) + 1 : 1;
        const color = randomColor();
        collections.push({ id, name, color });
        renderCollections();
        showToast("Collection", `Created "${name}"`);
    }

    function randomColor() {
        const colors = ["#dc3545", "#0d6efd", "#198754", "#ffc107", "#6f42c1", "#fd7e14"];
        return colors[Math.floor(Math.random() * colors.length)];
    }

    function updateStats() {
        const totalEl = document.getElementById("statTotal");
        const savedEl = document.getElementById("statSaved");
        const selectedEl = document.getElementById("statSelected");
        const comparingEl = document.getElementById("statComparing");
        if (totalEl) totalEl.textContent = String(allFeatures.length || 0);
        if (savedEl) savedEl.textContent = String(savedFeaturesMap.size || 0);
        if (selectedEl) selectedEl.textContent = selectedFeatureId ? "1" : "0";
        if (comparingEl) comparingEl.textContent = String(compareSet.size || 0);
    }

    function renderFeatureDetails(feature) {
        const container = document.getElementById("featureDetails");
        if (!container) return;
        if (!feature) {
            container.innerHTML = '<p class="text-muted text-center small">Click a state on the map to view details</p>';
            return;
        }
        const displayName = feature.displayName || getDisplayName(feature);
        const isSaved = savedFeaturesMap.has(feature.featureId);
        const html = [];
        html.push(`<h5 class="mb-2">${escapeHtml(displayName)}</h5>`);
        html.push(`<div class="mb-2"><button class="btn btn-sm btn-outline-primary me-1" onclick="window.gisApp.zoomTo(${feature.featureId})"><i class="fa-solid fa-search-plus"></i> Zoom</button>`);
        if (!isSaved) {
            html.push(` <button class="btn btn-sm btn-success me-1" onclick="window.gisApp.openSaveModal(${feature.featureId})"><i class="fa-solid fa-bookmark"></i> Save</button>`);
        } else {
            html.push(` <button class="btn btn-sm btn-outline-danger me-1" onclick="window.gisApp.removeFeature(${feature.featureId})"><i class="fa-solid fa-trash"></i> Remove</button>`);
        }
        html.push(` <button class="btn btn-sm btn-outline-warning" onclick="window.gisApp.addToCompare(${feature.featureId})"><i class="fa-solid fa-scale-balanced"></i> Compare</button></div>`);
        html.push(renderMetadataTable(feature));
        container.innerHTML = html.join("");
    }

    function showComparison() {
        const panel = document.getElementById("comparisonPanel");
        const content = document.getElementById("comparisonContent");
        if (!panel || !content) return;
        const ids = Array.from(compareSet);
        if (ids.length < 2) {
            panel.classList.add("d-none");
            return;
        }
        panel.classList.remove("d-none");
        const rows = ids.map(id => {
            const f = allFeatures.find(x => x.featureId === id) || savedFeaturesMap.get(id);
            return `<div class="mb-2"><strong>${escapeHtml(f?.displayName || f?.name || `State ${id}`)}</strong>${renderMetadataTable(f)}</div>`;
        }).join("");
        content.innerHTML = rows;
    }

    /* ===================== INITIALIZATION ===================== */
    async function initialize() {
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
        initializeBloodhound();
    }

    /* ===================== API CALLS ===================== */
    async function fetchFeatures(filter = "") {
        try {
            const res = await fetch(`/api/features?layerId=${LAYER_ID}`, { credentials: 'same-origin' });
            if (!res.ok) throw new Error("Failed to fetch features");
            const features = await res.json();
            cacheDisplayNames(features);
            allFeatures = features;
            featureBloodhound ? updateBloodhoundData() : initializeBloodhound();
            renderMap(allFeatures);
            updateStats();
        } catch (err) { showToast("Error", "Failed to load features", "danger"); }
    }

    async function loadSavedFeatures() {
        try {
            const res = await fetch("/api/savedfeatures", { credentials: 'same-origin' });
            if (!res.ok) throw new Error("Failed to load saved features");
            const features = await res.json();
            cacheDisplayNames(features);
            savedFeaturesMap.clear();
            features.forEach(f => savedFeaturesMap.set(f.featureId, f));
            renderSavedFeaturesList();
        } catch (err) { console.error(err); }
    }

    async function saveFeatureToApi(feature, description = "") {
        // Build payload using PascalCase keys and ensure FeatureId is a string to match server checks
        const payload = {
            LayerId: LAYER_ID,
            FeatureId: String(feature.featureId),
            Name: feature.displayName || feature.name || `State ${feature.featureId}`,
            GeometryJson: feature.geometryJson,
            Description: description
        };
        const res = await fetch("/api/savedfeatures", {
            method: "POST",
            credentials: 'same-origin',
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });
        if (res.status === 409) throw new Error("Feature already saved");
        if (!res.ok) {
            // try to parse server error message
            try {
                const errObj = await res.json();
                throw new Error(errObj?.error || "Failed to save feature");
            } catch {
                throw new Error("Failed to save feature");
            }
        }
        const saved = await res.json(); saved.displayName = feature.displayName; return saved;
    }

    async function deleteFeatureFromApi(id) {
        const res = await fetch(`/api/savedfeatures/${id}`, { method: "DELETE", credentials: 'same-origin' });
        if (!res.ok && res.status !== 404) throw new Error("Failed to delete");
    }

    /* ===================== MAP RENDERING ===================== */
    function renderMap(features) {
        graphicsLayer.removeAll();
        features.forEach(f => {
            let geometry;
            try { geometry = JSON.parse(f.geometryJson); geometry.type = "polygon"; } catch { return; }
            const isSaved = savedFeaturesMap.has(f.featureId);
            const isSelected = selectedFeatureId === f.featureId;
            const isComparing = compareSet.has(f.featureId);
            let symbol = isSelected ? { ...symbols.selected } : isComparing ? { ...symbols.comparing } : isSaved ? { ...symbols.saved } : { ...symbols.default };
            const graphic = new Graphic({ geometry, symbol, attributes: { ...f, displayName: f.displayName, isSaved, isSelected, isComparing } });
            graphicsLayer.add(graphic);
        });
    }

    /* ===================== EVENT HANDLERS ===================== */
    function setupEventListeners() {
        const btnCompare = document.getElementById("btnCompareMode");
        if (btnCompare) {
            btnCompare.addEventListener("click", () => {
                isCompareMode = !isCompareMode;
                const btn = document.getElementById("btnCompareMode");
                btn.classList.toggle("active", isCompareMode);
                btn.classList.toggle("btn-warning", isCompareMode);
                btn.classList.toggle("btn-outline-warning", !isCompareMode);
                showToast("Compare Mode", isCompareMode ? "Click states to add to comparison" : "Compare mode disabled");
                if (!isCompareMode) { compareSet.clear(); document.getElementById("comparisonPanel").classList.add("d-none"); }
                renderMap(allFeatures);
                updateStats();
            });
        }

        const btnClear = document.getElementById("btnClearSelection");
        if (btnClear) {
            btnClear.addEventListener("click", () => {
                selectedFeatureId = null; compareSet.clear(); isCompareMode = false;
                const btn = document.getElementById("btnCompareMode");
                if (btn) { btn.classList.remove("active", "btn-warning"); btn.classList.add("btn-outline-warning"); }
                const cmpPanel = document.getElementById("comparisonPanel");
                if (cmpPanel) cmpPanel.classList.add("d-none");
                renderMap(allFeatures); renderFeatureDetails(null); updateStats();
            });
        }

        view.on("click", handleMapClick);
        view.on("pointer-move", handleMapHover);

        const btnCloseComparison = document.getElementById("btnCloseComparison");
        if (btnCloseComparison) btnCloseComparison.addEventListener("click", () => {
            compareSet.clear();
            document.getElementById("comparisonPanel")?.classList.add("d-none");
            renderMap(allFeatures); updateStats();
        });

        // Save modal confirm
        const btnConfirmSave = document.getElementById("btnConfirmSave");
        if (btnConfirmSave) {
            btnConfirmSave.addEventListener("click", async () => {
                const fid = Number(document.getElementById("modalFeatureId").value);
                const description = document.getElementById("modalDescription").value || "";
                const feature = allFeatures.find(f => f.featureId === fid);
                if (!feature) return;
                try {
                    const saved = await saveFeatureToApi(feature, description);
                    saved.displayName = feature.displayName || getDisplayName(feature);
                    savedFeaturesMap.set(feature.featureId, saved);
                    renderSavedFeaturesList();
                    renderMap(allFeatures);
                    updateStats();
                    new bootstrap.Modal(document.getElementById("saveFeatureModal")).hide();
                    showToast("Saved", `"${saved.displayName}" has been saved`);
                } catch (err) {
                    console.error(err);
                    showToast("Error", err.message || "Failed to save state", "danger");
                }
            });
        }
    }

    async function handleMapClick(event) {
        const hit = await view.hitTest(event);
        const result = hit.results.find(r => r.graphic.layer === graphicsLayer);
        if (!result) { selectedFeatureId = null; renderFeatureDetails(null); renderMap(allFeatures); return; }
        const feature = result.graphic.attributes;
        if (isCompareMode) { compareSet.has(feature.featureId) ? compareSet.delete(feature.featureId) : compareSet.add(feature.featureId); renderMap(allFeatures); updateStats(); if (compareSet.size >= 2) showComparison(); return; }
        selectedFeatureId = feature.featureId;
        renderMap(allFeatures); renderFeatureDetails(feature);
        view.goTo({ target: result.graphic, zoom: 6 }, { duration: 500 });
    }

    let lastHoverFeatureId = null;
    async function handleMapHover(event) {
        const hit = await view.hitTest(event);
        const result = hit.results.find(r => r.graphic.layer === graphicsLayer);
        highlightLayer.removeAll();
        if (!result) { lastHoverFeatureId = null; view.container.style.cursor = "default"; return; }
        view.container.style.cursor = "pointer";
        const feature = result.graphic.attributes;
        if (feature.featureId === selectedFeatureId || feature.featureId === lastHoverFeatureId) return;
        lastHoverFeatureId = feature.featureId;
        highlightLayer.add(new Graphic({ geometry: result.graphic.geometry, symbol: symbols.hover }));
    }

    /* ===================== BLOODHOUND / TYPEAHEAD (basic) ===================== */
    let featureBloodhound = null;
    function initializeBloodhound() {
        try {
            if (typeof Bloodhound === "undefined" || !window.jQuery) return;
            const names = allFeatures.map(f => ({ name: f.displayName || getDisplayName(f), id: f.featureId }));
            featureBloodhound = new Bloodhound({
                datumTokenizer: Bloodhound.tokenizers.obj.whitespace('name'),
                queryTokenizer: Bloodhound.tokenizers.whitespace,
                local: names
            });
            $('#searchInput').typeahead('destroy');
            $('#searchInput').typeahead({
                highlight: true,
                minLength: 1
            }, {
                name: 'features',
                display: 'name',
                source: featureBloodhound
            }).bind('typeahead:select', function (ev, suggestion) {
                const id = suggestion.id;
                const feature = allFeatures.find(f => f.featureId === id);
                if (feature) {
                    window.gisApp.selectFeature(id);
                }
            });
        } catch (e) {
            // silently ignore if typeahead not available
            console.warn("Typeahead init failed", e);
        }
    }

    function updateBloodhoundData() {
        try {
            if (!featureBloodhound) return initializeBloodhound();
            const names = allFeatures.map(f => ({ name: f.displayName || getDisplayName(f), id: f.featureId }));
            featureBloodhound.clear();
            featureBloodhound.add(names);
        } catch (e) {
            console.warn("updateBloodhoundData failed", e);
        }
    }

    /* ===================== INITIALIZE ===================== */
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize);
    else initialize();

    /* ===================== EXPORT & TOAST ===================== */
    function showToast(title, message, type = "primary") {
        const toast = document.getElementById("toast");
        document.getElementById("toastTitle").textContent = title;
        document.getElementById("toastMessage").textContent = message;
        toast.classList.remove("bg-danger", "bg-success", "bg-primary");
        if (type === "danger") toast.classList.add("text-bg-danger");
        new bootstrap.Toast(toast).show();
    }

    /* ===================== GLOBAL APP API ===================== */
    window.gisApp = {
        selectFeature: (featureId) => {
            selectedFeatureId = featureId;
            const feature = allFeatures.find(f => f.featureId === featureId);
            renderMap(allFeatures); renderSavedFeaturesList(); renderFeatureDetails(feature || savedFeaturesMap.get(featureId));
            if (feature) { const g = graphicsLayer.graphics.find(g => g.attributes.featureId === featureId); if (g) view.goTo({ target: g, zoom: 6 }, { duration: 500 }); }
        },
        openSaveModal: (featureId) => {
            const feature = allFeatures.find(f => f.featureId === featureId); if (!feature) return;
            const displayName = feature.displayName || getDisplayName(feature);
            document.getElementById("modalFeatureId").value = featureId;
            document.getElementById("modalFeatureName").value = displayName;
            document.getElementById("modalDescription").value = "";
            // populate collections select if present
            const modalCollection = document.getElementById("modalCollection");
            if (modalCollection) {
                // keep current selection if set
                modalCollection.value = selectedCollectionId ? String(selectedCollectionId) : "";
            }
            new bootstrap.Modal(document.getElementById("saveFeatureModal")).show();
        },
        removeFeature: async (featureId) => {
            const saved = savedFeaturesMap.get(featureId); if (!saved) return;
            if (!confirm(`Remove "${saved.displayName}" from saved states?`)) return;
            try { await deleteFeatureFromApi(saved.id); savedFeaturesMap.delete(featureId); renderMap(allFeatures); renderSavedFeaturesList(); updateStats(); if (selectedFeatureId === featureId) renderFeatureDetails(allFeatures.find(f => f.featureId === featureId)); showToast("Removed", `"${saved.displayName}" has been removed`); } catch (err) { showToast("Error", "Failed to remove state", "danger"); }
        },
        addToCompare: (featureId) => {
            compareSet.add(featureId); isCompareMode = true;
            const btn = document.getElementById("btnCompareMode"); if (btn) { btn.classList.add("active", "btn-warning"); btn.classList.remove("btn-outline-warning"); }
            renderMap(allFeatures); updateStats(); if (compareSet.size >= 2) showComparison(); showToast("Compare", "State added to comparison");
        },
        zoomTo: (featureId) => {
            const g = graphicsLayer.graphics.find(g => g.attributes.featureId === featureId);
            if (g) view.goTo({ target: g, zoom: 6 }, { duration: 500 });
        }
    };
});