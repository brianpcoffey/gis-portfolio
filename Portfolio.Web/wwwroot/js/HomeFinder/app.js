// app.js — Smart Home Finder orchestration
const API_BASE = "/api/homefinder";

let mapView = null;
let graphicsLayer = null;
let currentResults = [];

// --- Slider binding ---
const sliders = {
    sliderMaxPrice:      { label: "lblMaxPrice",      key: "maxPrice" },
    sliderMonthlyBudget: { label: "lblMonthlyBudget", key: "maxMonthlyBudget" },
    sliderMinBed:        { label: "lblMinBed",        key: "minBedrooms" },
    sliderMinBath:       { label: "lblMinBath",        key: "minBathrooms" },
    sliderMinSqft:       { label: "lblMinSqft",       key: "minSqft" },
    sliderMaxCommute:    { label: "lblMaxCommute",     key: "maxCommuteMin" },
    sliderWAfford:       { label: "lblWAfford",        key: "weightAffordability", divisor: 100 },
    sliderWNeighbor:     { label: "lblWNeighbor",      key: "weightNeighborhood",  divisor: 100 },
    sliderWSize:         { label: "lblWSize",          key: "weightSize",          divisor: 100 },
    sliderWAppreciation: { label: "lblWAppreciation",  key: "weightAppreciation",  divisor: 100 },
    sliderWCondition:    { label: "lblWCondition",     key: "weightCondition",     divisor: 100 },
    sliderWCommute:      { label: "lblWCommute",       key: "weightCommute",       divisor: 100 },
    sliderWAmenities:    { label: "lblWAmenities",     key: "weightAmenities",     divisor: 100 },
    sliderWTax:          { label: "lblWTax",           key: "weightTaxUtilities",  divisor: 100 },
    sliderWResale:       { label: "lblWResale",        key: "weightResale",        divisor: 100 },
    sliderWEnv:          { label: "lblWEnv",           key: "weightEnvironment",   divisor: 100 }
};

function getPreferences() {
    const prefs = {};
    for (const [sliderId, cfg] of Object.entries(sliders)) {
        const el = document.getElementById(sliderId);
        if (!el) continue;
        const val = parseFloat(el.value);
        prefs[cfg.key] = cfg.divisor ? val / cfg.divisor : val;
    }
    return prefs;
}

function wireSliders() {
    for (const [sliderId, cfg] of Object.entries(sliders)) {
        const el = document.getElementById(sliderId);
        const lbl = document.getElementById(cfg.label);
        if (!el || !lbl) continue;
        el.addEventListener("input", () => { lbl.textContent = el.value; });
    }
}

// --- Map initialization ---
async function initMap() {
    await new Promise((resolve) => {
        require([
            "esri/Map",
            "esri/views/MapView",
            "esri/layers/GraphicsLayer"
        ], (Map, MapView, GraphicsLayer) => {
            const map = new Map({ basemap: "dark-gray-vector" });
            graphicsLayer = new GraphicsLayer();
            map.add(graphicsLayer);

            mapView = new MapView({
                container: "homeFinderMap",
                map: map,
                center: [-117.1825, 34.0556], // Redlands, CA
                zoom: 13
            });

            mapView.when(resolve);
        });
    });
}

// --- Plot results on map ---
function plotResults(results) {
    require([
        "esri/Graphic",
        "esri/geometry/Point",
        "esri/symbols/SimpleMarkerSymbol",
        "esri/PopupTemplate"
    ], (Graphic, Point, SimpleMarkerSymbol, PopupTemplate) => {
        graphicsLayer.removeAll();

        results.forEach((p) => {
            const point = new Point({ longitude: p.longitude, latitude: p.latitude });

            const color = p.rank <= 3 ? [76, 175, 80, 0.9]
                        : p.rank <= 7 ? [255, 193, 7, 0.9]
                        : [244, 67, 54, 0.8];

            const symbol = new SimpleMarkerSymbol({
                color: color,
                size: 14 - p.rank * 0.3,
                outline: { color: [255, 255, 255], width: 1 }
            });

            const graphic = new Graphic({
                geometry: point,
                symbol: symbol,
                attributes: p,
                popupTemplate: new PopupTemplate({
                    title: `#${p.rank} — ${p.street}`,
                    content: `
                        <b>Score:</b> ${p.compositeScore}/100<br>
                        <b>Price:</b> $${p.price.toLocaleString()}<br>
                        <b>Monthly:</b> $${p.estimatedMonthlyCost.toLocaleString()}<br>
                        <b>Bed/Bath:</b> ${p.bedrooms}/${p.bathrooms}<br>
                        <b>Sq Ft:</b> ${p.lotSqft.toLocaleString()}<br>
                        <b>Zip:</b> ${p.zipCode}
                    `
                })
            });

            graphicsLayer.add(graphic);
        });

        // Zoom to results extent
        if (results.length > 0) {
            mapView.goTo(graphicsLayer.graphics.toArray(), { padding: 50 });
        }
    });
}

// --- Results panel ---
function renderResults(results) {
    const panel = document.getElementById("resultsPanel");
    if (!panel) return;

    if (!results.length) {
        panel.innerHTML = '<p class="text-muted text-center small">No matching properties found. Try adjusting your filters.</p>';
        return;
    }

    const html = results.map(p => `
        <div class="card mb-2 p-2" style="cursor:pointer;" data-property-id="${p.propertyId}">
            <div class="d-flex justify-content-between align-items-start">
                <div>
                    <strong class="small">#${p.rank}</strong>
                    <span class="small ms-1">${escapeHtml(p.street)}</span>
                    <div class="text-muted" style="font-size:.75rem;">
                        $${p.price.toLocaleString()} · ${p.bedrooms}bd/${p.bathrooms}ba · ${p.lotSqft.toLocaleString()} sqft
                    </div>
                </div>
                <span class="badge bg-success">${p.compositeScore}</span>
            </div>
        </div>
    `).join("");

    panel.innerHTML = html;

    // Click to zoom
    panel.querySelectorAll("[data-property-id]").forEach(card => {
        card.addEventListener("click", () => {
            const id = parseInt(card.dataset.propertyId, 10);
            const match = currentResults.find(r => r.propertyId === id);
            if (match && mapView) {
                require(["esri/geometry/Point"], (Point) => {
                    mapView.goTo({ target: new Point({ longitude: match.longitude, latitude: match.latitude }), zoom: 16 });
                    mapView.popup.open({
                        title: `#${match.rank} — ${match.street}`,
                        location: new Point({ longitude: match.longitude, latitude: match.latitude })
                    });
                });
            }
        });
    });
}

function escapeHtml(s) {
    return String(s || "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

// --- Search ---
async function performSearch() {
    const prefs = getPreferences();
    const btn = document.getElementById("btnSearch");
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Searching...';

    try {
        const res = await fetch(`${API_BASE}/search`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(prefs)
        });

        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        currentResults = await res.json();

        plotResults(currentResults);
        renderResults(currentResults);

        document.getElementById("btnSaveSearch").disabled = currentResults.length === 0;
    } catch (err) {
        console.error("Search failed:", err);
        document.getElementById("resultsPanel").innerHTML =
            '<p class="text-danger text-center small">Search failed. Please try again.</p>';
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-magnifying-glass me-1"></i>Find Homes';
    }
}

// --- Init ---
document.addEventListener("DOMContentLoaded", async () => {
    wireSliders();
    await initMap();

    document.getElementById("btnSearch")?.addEventListener("click", performSearch);
    document.getElementById("btnSaveSearch")?.addEventListener("click", async () => {
        // Save search to profile (simplified — POST preferences + result IDs)
        const prefs = getPreferences();
        const name = prompt("Name this search:", `Search ${new Date().toLocaleDateString()}`);
        if (!name) return;

        try {
            const res = await fetch(`${API_BASE}/searches`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    name: name.trim(),
                    preferences: prefs,
                    propertyIds: currentResults.map(r => r.propertyId)
                })
            });
            if (res.ok) alert("Search saved to your profile!");
        } catch (err) {
            console.error("Save search failed:", err);
        }
    });
});