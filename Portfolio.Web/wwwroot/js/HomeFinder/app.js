// app.js — Smart Home Finder orchestration
const API_BASE = "/api/homefinder";

let mapView = null;
let mapInstance = null;
let graphicsLayer = null;
let currentResults = [];

// --- Weight slider IDs (for validation) ---
const weightSliderIds = [
    "sliderWAfford", "sliderWNeighbor", "sliderWSize", "sliderWAppreciation",
    "sliderWCondition", "sliderWCommute", "sliderWAmenities", "sliderWTax",
    "sliderWResale", "sliderWEnv"
];

// --- Mapping from slider ID to visual bar ID ---
const weightBarIds = {
    sliderWAfford: "barWAfford",
    sliderWNeighbor: "barWNeighbor",
    sliderWSize: "barWSize",
    sliderWAppreciation: "barWAppreciation",
    sliderWCondition: "barWCondition",
    sliderWCommute: "barWCommute",
    sliderWAmenities: "barWAmenities",
    sliderWTax: "barWTax",
    sliderWResale: "barWResale",
    sliderWEnv: "barWEnv"
};

// --- Slider binding ---
const sliders = {
    sliderMaxPrice: { label: "lblMaxPrice", key: "maxPrice" },
    sliderMonthlyBudget: { label: "lblMonthlyBudget", key: "maxMonthlyBudget" },
    sliderMinBed: { label: "lblMinBed", key: "minBedrooms" },
    sliderMinBath: { label: "lblMinBath", key: "minBathrooms" },
    sliderMinSqft: { label: "lblMinSqft", key: "minSqft" },
    sliderMaxCommute: { label: "lblMaxCommute", key: "maxCommuteMin" },
    sliderWAfford: { label: "lblWAfford", key: "weightAffordability", divisor: 100 },
    sliderWNeighbor: { label: "lblWNeighbor", key: "weightNeighborhood", divisor: 100 },
    sliderWSize: { label: "lblWSize", key: "weightSize", divisor: 100 },
    sliderWAppreciation: { label: "lblWAppreciation", key: "weightAppreciation", divisor: 100 },
    sliderWCondition: { label: "lblWCondition", key: "weightCondition", divisor: 100 },
    sliderWCommute: { label: "lblWCommute", key: "weightCommute", divisor: 100 },
    sliderWAmenities: { label: "lblWAmenities", key: "weightAmenities", divisor: 100 },
    sliderWTax: { label: "lblWTax", key: "weightTaxUtilities", divisor: 100 },
    sliderWResale: { label: "lblWResale", key: "weightResale", divisor: 100 },
    sliderWEnv: { label: "lblWEnv", key: "weightEnvironment", divisor: 100 }
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

// --- Weight validation ---
function getWeightSum() {
    let sum = 0;
    for (const id of weightSliderIds) {
        const el = document.getElementById(id);
        if (el) sum += parseInt(el.value, 10);
    }
    return sum;
}

/**
 * Updates the visual weight bar for a given slider.
 * Sets width as a percentage and applies color coding based on the value.
 */
function updateWeightBar(sliderId) {
    const barId = weightBarIds[sliderId];
    if (!barId) return;

    const slider = document.getElementById(sliderId);
    const bar = document.getElementById(barId);
    if (!slider || !bar) return;

    const value = parseInt(slider.value, 10);
    const pct = Math.min(Math.max(value, 0), 100);

    // Set the actual width — this drives the CSS transition
    bar.style.width = pct + "%";

    // Color code by severity
    if (pct >= 50) {
        bar.setAttribute("data-weight-level", "high");
    } else if (pct >= 30) {
        bar.setAttribute("data-weight-level", "medium");
    } else {
        bar.removeAttribute("data-weight-level");
    }
}

/** Refreshes all weight bars to match their slider values. */
function updateAllWeightBars() {
    for (const sliderId of weightSliderIds) {
        updateWeightBar(sliderId);
    }
}

function updateWeightStatus() {
    const total = getWeightSum();
    const statusEl = document.getElementById("weightStatus");
    const totalEl = document.getElementById("weightTotal");
    const btnSearch = document.getElementById("btnSearch");

    if (!statusEl || !totalEl) return;

    totalEl.textContent = total;

    if (total === 100) {
        statusEl.className = "alert alert-success small py-1 px-2 mb-2";
        statusEl.innerHTML = `<i class="fa-solid fa-circle-check me-1"></i>Total: <strong>${total}</strong>% <span class="text-muted">\u2014 valid</span>`;
        if (btnSearch) btnSearch.disabled = false;
    } else {
        statusEl.className = "alert alert-danger small py-1 px-2 mb-2";
        const diff = total - 100;
        const hint = diff > 0 ? `${diff}% over` : `${Math.abs(diff)}% under`;
        statusEl.innerHTML = `<i class="fa-solid fa-triangle-exclamation me-1"></i>Total: <strong>${total}</strong>% <span>(${hint} \u2014 must equal 100%)</span>`;
        if (btnSearch) btnSearch.disabled = true;
    }
}

function wireSliders() {
    for (const [sliderId, cfg] of Object.entries(sliders)) {
        const el = document.getElementById(sliderId);
        const lbl = document.getElementById(cfg.label);
        if (!el || !lbl) continue;
        el.addEventListener("input", () => {
            lbl.textContent = el.value;
            if (cfg.divisor) {
                updateWeightBar(sliderId);
                updateWeightStatus();
            }
        });
    }
    // Initial sync of all weight bars and validation
    updateAllWeightBars();
    updateWeightStatus();
}

// --- Map initialization ---
async function initMap() {
    await new Promise((resolve) => {
        require([
            "esri/Map",
            "esri/views/MapView",
            "esri/layers/GraphicsLayer"
        ], (Map, MapView, GraphicsLayer) => {
            mapInstance = new Map({ basemap: "dark-gray-vector" });
            graphicsLayer = new GraphicsLayer();
            mapInstance.add(graphicsLayer);

            mapView = new MapView({
                container: "homeFinderMap",
                map: mapInstance,
                center: [-117.1825, 34.0556], // Redlands, CA
                zoom: 13
            });

            mapView.when(resolve);
        });
    });
}

// --- Basemap switching ---
function wireBasemapSelect() {
    const select = document.getElementById("basemapSelect");
    if (!select || !mapInstance) return;
    select.addEventListener("change", () => {
        mapInstance.basemap = select.value;
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
                    title: `#${p.rank} \u2014 ${p.street}`,
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

        if (results.length > 0) {
            mapView.goTo(graphicsLayer.graphics.toArray(), { padding: 50 });
        }
    });
}

/**
 * Returns the CSS class for a score bar based on the composite score.
 * 70+  = excellent (green), 40-69 = good (yellow), <40 = fair (red)
 */
function getScoreBarClass(score) {
    if (score >= 70) return "score-excellent";
    if (score >= 40) return "score-good";
    return "score-fair";
}

// --- Results panel ---
function renderResults(results) {
    const panel = document.getElementById("resultsPanel");
    if (!panel) return;

    if (!results.length) {
        panel.innerHTML = '<p class="text-muted text-center small">No matching properties found. Try adjusting your filters.</p>';
        return;
    }

    const html = results.map(p => {
        const scorePct = Math.min(Math.max(p.compositeScore, 0), 100);
        const barClass = getScoreBarClass(p.compositeScore);

        return `
            <div class="card theme-card mb-2 p-2" style="cursor:pointer;" data-property-id="${p.propertyId}" tabindex="0" role="button" aria-label="View property #${p.rank} at ${escapeHtml(p.street)}">
                <div class="d-flex justify-content-between align-items-start">
                    <div class="flex-grow-1 me-2">
                        <strong class="small">#${p.rank}</strong>
                        <span class="small ms-1">${escapeHtml(p.street)}</span>
                        <div class="theme-text-muted" style="font-size:.75rem;">
                            $${p.price.toLocaleString()} \u2014 ${p.bedrooms}bd/${p.bathrooms}ba \u2014 ${p.lotSqft.toLocaleString()} sqft
                        </div>
                        <!-- Responsive score bar -->
                        <div class="score-bar-container" aria-label="Composite score: ${scorePct}%">
                            <div class="score-bar-fill ${barClass}" style="width: 0%;" data-target-width="${scorePct}"></div>
                        </div>
                    </div>
                    <span class="badge bg-success">${p.compositeScore}</span>
                </div>
            </div>`;
    }).join("");

    panel.innerHTML = html;

    // Animate score bars after DOM insertion (triggers CSS transition)
    requestAnimationFrame(() => {
        panel.querySelectorAll(".score-bar-fill[data-target-width]").forEach(bar => {
            bar.style.width = bar.dataset.targetWidth + "%";
        });
    });

    // Wire click + keyboard handlers
    panel.querySelectorAll("[data-property-id]").forEach(card => {
        const handler = () => {
            const id = parseInt(card.dataset.propertyId, 10);
            const match = currentResults.find(r => r.propertyId === id);
            if (match && mapView) {
                require(["esri/geometry/Point"], (Point) => {
                    const point = new Point({ longitude: match.longitude, latitude: match.latitude });

                    mapView.goTo({ target: point, zoom: 16 });

                    mapView.openPopup({
                        title: `#${match.rank} — ${match.street}`,
                        location: point
                    });
                });
            }
        };

        card.addEventListener("click", handler);
        card.addEventListener("keydown", (e) => {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                handler();
            }
        });
    });
}

function escapeHtml(s) {
    return String(s || "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

// --- Search ---
async function performSearch() {
    // Client-side weight validation
    if (getWeightSum() !== 100) {
        updateWeightStatus();
        return;
    }

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
        updateWeightStatus(); // Re-enable/disable based on current weight state
    }
}

// --- Save Search ---
async function saveSearch() {
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
        if (res.ok) {
            showToast("Search saved to your profile!", "success");
        } else {
            showToast("Failed to save search.", "danger");
        }
    } catch (err) {
        console.error("Save search failed:", err);
        showToast("Failed to save search.", "danger");
    }
}

// --- Load Saved Searches ---
async function loadSavedSearches() {
    const body = document.getElementById("savedSearchesBody");
    if (!body) return;

    body.innerHTML = '<p class="text-muted text-center small">Loading...</p>';

    try {
        const res = await fetch(`${API_BASE}/searches`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const searches = await res.json();

        if (!searches.length) {
            body.innerHTML = '<p class="text-muted text-center small">No saved searches found.</p>';
            return;
        }

        body.innerHTML = searches.map(s => `
            <div class="card theme-card mb-2 p-2">
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <strong class="small">${escapeHtml(s.name)}</strong>
                        <div class="theme-text-muted" style="font-size:.75rem;">
                            Saved ${new Date(s.createdAt).toLocaleDateString()}
                        </div>
                    </div>
                    <div class="d-flex gap-1">
                        <button class="btn btn-sm btn-outline-accent btn-load-search" data-search-id="${s.id}">
                            <i class="fa-solid fa-upload me-1"></i>Load
                        </button>
                        <button class="btn btn-sm btn-outline-danger btn-delete-search" data-search-id="${s.id}">
                            <i class="fa-solid fa-trash"></i>
                        </button>
                    </div>
                </div>
            </div>
        `).join("");

        // Wire load buttons
        body.querySelectorAll(".btn-load-search").forEach(btn => {
            btn.addEventListener("click", async () => {
                const id = btn.dataset.searchId;
                await applySavedSearch(id);
                const modal = bootstrap.Modal.getInstance(document.getElementById("savedSearchesModal"));
                modal?.hide();
            });
        });

        // Wire delete buttons
        body.querySelectorAll(".btn-delete-search").forEach(btn => {
            btn.addEventListener("click", async () => {
                const id = btn.dataset.searchId;
                if (!confirm("Delete this saved search?")) return;
                try {
                    const res = await fetch(`${API_BASE}/searches/${id}`, { method: "DELETE" });
                    if (res.ok) {
                        btn.closest(".card").remove();
                        showToast("Search deleted.", "success");
                    }
                } catch (err) {
                    console.error("Delete failed:", err);
                }
            });
        });
    } catch (err) {
        console.error("Load searches failed:", err);
        body.innerHTML = '<p class="text-danger text-center small">Failed to load saved searches.</p>';
    }
}

// --- Apply a saved search (restore sliders and re-search) ---
async function applySavedSearch(searchId) {
    try {
        const res = await fetch(`${API_BASE}/searches/${searchId}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const saved = await res.json();

        const prefs = saved.preferences || JSON.parse(saved.preferencesJson || "{}");

        // Restore slider values
        for (const [sliderId, cfg] of Object.entries(sliders)) {
            const el = document.getElementById(sliderId);
            const lbl = document.getElementById(cfg.label);
            if (!el || prefs[cfg.key] === undefined) continue;

            const raw = cfg.divisor ? Math.round(prefs[cfg.key] * cfg.divisor) : prefs[cfg.key];
            el.value = raw;
            if (lbl) lbl.textContent = raw;
        }

        // Sync all weight bars after restoring slider values
        updateAllWeightBars();
        updateWeightStatus();
        await performSearch();
        showToast(`Loaded "${escapeHtml(saved.name)}"`, "success");
    } catch (err) {
        console.error("Apply saved search failed:", err);
        showToast("Failed to load search.", "danger");
    }
}

// --- Toast helper ---
function showToast(message, type = "success") {
    const container = document.getElementById("toastContainer") || createToastContainer();
    const toast = document.createElement("div");
    toast.className = `toast align-items-center text-bg-${type} border-0 show`;
    toast.setAttribute("role", "alert");
    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">${message}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>`;
    container.appendChild(toast);
    setTimeout(() => toast.remove(), 4000);
}

function createToastContainer() {
    const c = document.createElement("div");
    c.id = "toastContainer";
    c.className = "toast-container position-fixed bottom-0 end-0 p-3";
    c.style.zIndex = "1090";
    document.body.appendChild(c);
    return c;
}

// --- Init ---
document.addEventListener("DOMContentLoaded", async () => {
    wireSliders();
    await initMap();
    wireBasemapSelect();

    document.getElementById("btnSearch")?.addEventListener("click", performSearch);
    document.getElementById("btnSaveSearch")?.addEventListener("click", saveSearch);
    document.getElementById("btnLoadSearches")?.addEventListener("click", () => {
        const modal = new bootstrap.Modal(document.getElementById("savedSearchesModal"));
        modal.show();
        loadSavedSearches();
    });
});