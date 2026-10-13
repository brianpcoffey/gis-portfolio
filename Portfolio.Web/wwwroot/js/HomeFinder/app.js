// app.js — Smart Home Finder orchestration
const HOME_FINDER_ROUTES = window.PortfolioApi.routes.homeFinder;


let mapView = null;
let mapInstance = null;
let graphicsLayer = null;
let currentResults = [];

// Esri widget theme (light/dark, follows the site toggle) before the map exists.
window.PortfolioEsri.loadTheme();

const announce = (msg) => window.PortfolioUi.announce(msg);

// ============================================================
// Slider / Weight Configuration
// ============================================================

const weightSliderIds = [
    "sliderWAfford", "sliderWNeighbor", "sliderWSize", "sliderWAppreciation",
    "sliderWCondition", "sliderWCommute", "sliderWAmenities", "sliderWTax",
    "sliderWResale", "sliderWEnv"
];

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

const sliders = {
    sliderMaxPrice: { label: "lblMaxPrice", key: "maxPrice", format: (v) => PortfolioFormat.money(v) },
    sliderMonthlyBudget: { label: "lblMonthlyBudget", key: "maxMonthlyBudget", format: (v) => PortfolioFormat.money(v) },
    sliderMinBed: { label: "lblMinBed", key: "minBedrooms" },
    sliderMinBath: { label: "lblMinBath", key: "minBathrooms" },
    sliderMinSqft: { label: "lblMinSqft", key: "minSqft", format: (v) => PortfolioFormat.number(v) },
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

/** Shows the slider's value, formatted, in its visible label and as aria-valuetext. */
function syncSliderLabel(sliderId) {
    const cfg = sliders[sliderId];
    const el = document.getElementById(sliderId);
    if (!cfg || !el) return;
    let text;
    if (cfg.format) text = cfg.format(el.value);
    else if (cfg.divisor) text = `${el.value}%`;
    else text = String(el.value);
    const lbl = document.getElementById(cfg.label);
    if (lbl) lbl.textContent = cfg.divisor ? el.value : text;
    el.setAttribute("aria-valuetext", text);
}

// ============================================================
// Preference Collection
// ============================================================

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

// ============================================================
// Weight Engine — Dynamic Clamping
// ============================================================

/** Returns the integer sum of all weight sliders. */
function getWeightSum() {
    let sum = 0;
    for (const id of weightSliderIds) {
        const el = document.getElementById(id);
        if (el) sum += parseInt(el.value, 10);
    }
    return sum;
}

/**
 * Dynamically adjusts every weight slider's `max` attribute so the user
 * can never push the total above 100%.  The active slider (the one
 * currently being dragged) is excluded from re-clamping so it feels
 * responsive; instead, every *other* slider has its max reduced.
 *
 * @param {string|null} activeSliderId  The slider currently being dragged.
 */
function enforceWeightBudget(activeSliderId = null) {
    const total = getWeightSum();
    const step = 5; // matches the HTML step attribute

    for (const id of weightSliderIds) {
        const el = document.getElementById(id);
        if (!el) continue;

        const currentVal = parseInt(el.value, 10);
        // remaining = how much room exists if we took this slider to 0
        const othersSum = total - currentVal;
        const maxAllowed = Math.max(0, 100 - othersSum);

        // Round down to nearest step so the slider thumb snaps correctly
        const steppedMax = Math.floor(maxAllowed / step) * step;
        el.max = steppedMax;

        // If the current value somehow exceeds the new max, clamp it
        if (currentVal > steppedMax) {
            el.value = steppedMax;
            syncSliderLabel(id);
        }
    }
}

/**
 * Updates the visual weight bar for a given slider.
 * Bar width is shown as percentage of 100, color-coded by severity.
 */
function updateWeightBar(sliderId) {
    const barId = weightBarIds[sliderId];
    if (!barId) return;

    const slider = document.getElementById(sliderId);
    const bar = document.getElementById(barId);
    if (!slider || !bar) return;

    const value = parseInt(slider.value, 10);
    const pct = Math.min(Math.max(value, 0), 100);

    bar.style.width = pct + "%";

    // ARIA: expose current value to screen readers
    bar.setAttribute("aria-valuenow", pct);

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

/**
 * Master status update — shows total, remaining, and gates the Search button.
 * Also updates the circular/arc remaining-budget indicator.
 */
function updateWeightStatus() {
    const total = getWeightSum();
    const remaining = 100 - total;
    const statusEl = document.getElementById("weightStatus");
    const totalEl = document.getElementById("weightTotal");
    const remainingEl = document.getElementById("weightRemaining");
    const btnSearch = document.getElementById("btnSearch");
    const budgetRing = document.getElementById("budgetRing");

    if (totalEl) totalEl.textContent = total;
    if (remainingEl) remainingEl.textContent = remaining;

    // Update the SVG ring indicator
    if (budgetRing) {
        const circumference = 2 * Math.PI * 36; // r=36
        const offset = circumference - (total / 100) * circumference;
        budgetRing.style.strokeDashoffset = offset;

        budgetRing.classList.remove("ring-valid", "ring-over", "ring-under");
        if (total === 100) budgetRing.classList.add("ring-valid");
        else if (total > 100) budgetRing.classList.add("ring-over");
        else budgetRing.classList.add("ring-under");
    }

    if (!statusEl) return;

    const textEl = document.getElementById("weightStatusText");
    const state = total === 100 ? "valid" : (remaining > 0 ? "under" : "over");
    const hint = remaining > 0 ? `${remaining}% remaining` : `${Math.abs(remaining)}% over`;

    statusEl.className = `alert ${state === "valid" ? "alert-success" : "alert-warning"} small py-1 px-2 mb-2`;
    if (textEl) {
        const icon = PortfolioDom.el("i", { class: `fa-solid ${state === "valid" ? "fa-circle-check" : "fa-scale-unbalanced"} me-1`, "aria-hidden": "true" });
        const tail = PortfolioDom.el("span", { text: state === "valid" ? "\u2014 ready to search" : `(${hint} \u2014 must equal 100%)` });
        textEl.replaceChildren(icon, "Total: ", PortfolioDom.el("strong", { id: "weightTotal", text: `${total}%` }), " ", tail);
    }
    if (btnSearch) btnSearch.disabled = state !== "valid";

    // The visible total (#weightStatusText) is read as plain text; only a validity flip is announced,
    // never every slider step, and not the first evaluation on page load.
    if (lastWeightState !== null && state !== lastWeightState) {
        announce(state === "valid"
            ? "Weights total 100%, ready to search."
            : `Weights total ${total}%: ${hint}, must equal 100%.`);
    }
    lastWeightState = state;
}

let lastWeightState = null;

// ============================================================
// Slider Wiring
// ============================================================

function wireSliders() {
    for (const [sliderId, cfg] of Object.entries(sliders)) {
        const el = document.getElementById(sliderId);
        const lbl = document.getElementById(cfg.label);
        if (!el || !lbl) continue;

        el.addEventListener("input", () => {
            syncSliderLabel(sliderId);

            if (cfg.divisor) {
                // Weight slider — enforce budget, then update visuals
                enforceWeightBudget(sliderId);
                updateWeightBar(sliderId);
                updateWeightStatus();
            }
        });

        // The visible <label for> names the slider; aria-valuetext carries the formatted value.
        syncSliderLabel(sliderId);
    }

    // Initial sync
    enforceWeightBudget();
    updateAllWeightBars();
    updateWeightStatus();
}

// ============================================================
// Persistence — Save/Load slider state via localStorage
// ============================================================

const PREFS_STORAGE_KEY = "homeFinderPrefs";

/** Persists the current slider values to localStorage. */
function persistPreferences() {
    try {
        const prefs = getPreferences();
        localStorage.setItem(PREFS_STORAGE_KEY, JSON.stringify(prefs));
    } catch { /* quota exceeded / private browsing — silently ignore */ }
}

/** Restores slider values from localStorage if present. Returns true if restored. */
function restorePreferences() {
    try {
        const raw = localStorage.getItem(PREFS_STORAGE_KEY);
        if (!raw) return false;

        const prefs = JSON.parse(raw);

        for (const [sliderId, cfg] of Object.entries(sliders)) {
            const el = document.getElementById(sliderId);
            if (!el || prefs[cfg.key] === undefined) continue;

            const raw = cfg.divisor
                ? Math.round(prefs[cfg.key] * cfg.divisor)
                : prefs[cfg.key];
            el.value = raw;
            syncSliderLabel(sliderId);
        }

        return true;
    } catch {
        return false;
    }
}

// ============================================================
// Map Initialization
// ============================================================

async function initMap() {
    await window.PortfolioEsri.load();
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
                center: [-117.1825, 34.0556],
                zoom: 13
            });

            mapView.when(() => {
                // ArcGIS nests its own role="application" surface inside the container; name that one.
                const surface = mapView.container.querySelector(".esri-view-surface");
                const label = document.getElementById("homeFinderMap")?.dataset.mapLabel;
                if (surface && label) surface.setAttribute("aria-label", label);
                resolve();
            });
        });
    });
}

function wireBasemapSelect() {
    const select = document.getElementById("basemapSelect");
    if (!select || !mapInstance) return;
    select.addEventListener("change", () => {
        mapInstance.basemap = select.value;
    });
}

// ============================================================
// Map — Plot Results
// ============================================================

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
                    title: `#${p.rank} \u2014 ${PortfolioDom.escapeHtml(p.street)}`,
                    content: `
                        <b>Score:</b> ${p.compositeScore}/100<br>
                        <b>Price:</b> $${p.price.toLocaleString()} (${pricePerSqft(p)}/sqft)<br>
                        <b>Monthly:</b> $${p.estimatedMonthlyCost.toLocaleString()}<br>
                        <b>Type:</b> ${PortfolioDom.escapeHtml(p.propertyType)} \u2014 ${p.stories}-story<br>
                        <b>Bed/Bath:</b> ${p.bedrooms}/${p.bathrooms}<br>
                        <b>Sq Ft:</b> ${p.lotSqft.toLocaleString()}<br>
                        <b>Built:</b> ${p.yearBuilt}<br>
                        <b>Garage:</b> ${p.garageSpaces} car${p.garageSpaces === 1 ? "" : "s"}${p.hasPool ? " \u2014 Pool" : ""}<br>
                        <b>School rating:</b> ${p.schoolRating}/100<br>
                        <b>HOA:</b> ${p.hoaFee > 0 ? "$" + p.hoaFee.toLocaleString() + "/mo" : "None"}<br>
                        <b>Zip:</b> ${PortfolioDom.escapeHtml(p.zipCode)} \u2014 ${p.daysOnMarket} days on market<br>
                        <span class="theme-text-muted">Listed by ${PortfolioDom.escapeHtml(p.brokeredBy || "")}</span>
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

// ============================================================
// Results Panel
// ============================================================

function getScoreBarClass(score) {
    if (score >= 70) return "score-excellent";
    if (score >= 40) return "score-good";
    return "score-fair";
}

/** Formats price-per-square-foot as a compact dollar string. */
function pricePerSqft(p) {
    if (!p.lotSqft) return "$—";
    return "$" + Math.round(p.price / p.lotSqft).toLocaleString();
}

function renderResults(results) {
    const panel = document.getElementById("resultsPanel");
    if (!panel) return;

    if (!results.length) {
        panel.innerHTML =
            '<p class="theme-text-muted text-center small">No matching properties found. Try adjusting your filters.</p>';
        announce("No matching properties found. Try adjusting your filters.");
        return;
    }

    const html = results.map(p => {
        const scorePct = Math.min(Math.max(p.compositeScore, 0), 100);
        const barClass = getScoreBarClass(p.compositeScore);

        return `
            <div class="card theme-card mb-2 p-2" style="cursor:pointer;"
                 data-property-id="${p.propertyId}" tabindex="0" role="button"
                 aria-label="View property #${p.rank} at ${PortfolioDom.escapeHtml(p.street)}">
                <div class="d-flex justify-content-between align-items-start">
                    <div class="flex-grow-1 me-2">
                        <strong class="small">#${p.rank}</strong>
                        <span class="small ms-1">${PortfolioDom.escapeHtml(p.street)}</span>
                        <div class="theme-text-muted" style="font-size:.75rem;">
                            $${p.price.toLocaleString()} \u2014 ${p.bedrooms}bd/${p.bathrooms}ba \u2014 ${p.lotSqft.toLocaleString()} sqft \u2014 ${pricePerSqft(p)}/sqft
                        </div>
                        <div class="theme-text-muted" style="font-size:.7rem;">
                            ${PortfolioDom.escapeHtml(p.propertyType)} \u00b7 Built ${p.yearBuilt} \u00b7 ${p.garageSpaces}-car garage${p.hasPool ? " \u00b7 Pool" : ""} \u00b7 School ${p.schoolRating}
                        </div>
                        <div class="score-bar-container" aria-label="Composite score: ${scorePct}%">
                            <div class="score-bar-fill ${barClass}" style="width: 0%;" data-target-width="${scorePct}"></div>
                        </div>
                    </div>
                    <span class="badge bg-success">${p.compositeScore}</span>
                </div>
            </div>`;
    }).join("");

    panel.innerHTML = html;
    announce(`${results.length} ${results.length === 1 ? "property" : "properties"} found`);

    requestAnimationFrame(() => {
        panel.querySelectorAll(".score-bar-fill[data-target-width]").forEach(bar => {
            bar.style.width = bar.dataset.targetWidth + "%";
        });
    });

    panel.querySelectorAll("[data-property-id]").forEach(card => {
        const handler = () => {
            const id = parseInt(card.dataset.propertyId, 10);
            const match = currentResults.find(r => r.propertyId === id);
            if (match && mapView) {
                require(["esri/geometry/Point"], (Point) => {
                    const point = new Point({ longitude: match.longitude, latitude: match.latitude });
                    mapView.goTo({ target: point, zoom: 16 });
                    mapView.openPopup({ title: `#${match.rank} \u2014 ${PortfolioDom.escapeHtml(match.street)}`, location: point });
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

// ============================================================
// Search
// ============================================================

async function performSearch() {
    const total = getWeightSum();
    if (total !== 100) {
        updateWeightStatus();
        const diff = 100 - total;
        showToast(
            diff > 0
                ? `Weights are ${diff}% under \u2014 adjust sliders to total 100%.`
                : `Weights are ${Math.abs(diff)}% over \u2014 reduce sliders to total 100%.`,
            "warning"
        );
        return;
    }

    const prefs = getPreferences();
    const btn = document.getElementById("btnSearch");
    const done = PortfolioUi.busy({ buttons: [btn], region: document.getElementById("resultsPanel") });
    btn.replaceChildren(PortfolioDom.el("span", { class: "spinner-border spinner-border-sm me-1", "aria-hidden": "true" }), "Searching\u2026");

    try {
        // Use apiFetch instead of raw fetch so that a 401 response
        // triggers a full-page redirect to Google OAuth login.
        // This avoids the CORS error that would occur if the browser
        // tried to follow a 302 redirect to accounts.google.com
        // from within a fetch() call.
        const res = await apiFetch(HOME_FINDER_ROUTES.search, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(prefs)
        });

        if (!res.ok) {
            // Surface server-side validation errors
            if (res.status === 400) {
                const body = await res.json().catch(() => null);
                const msg = body?.errors
                    ? Object.values(body.errors).flat().join(" ")
                    : "Invalid preferences. Check your inputs.";
                showToast(msg, "danger");
                return;
            }
            throw new Error(`HTTP ${res.status}`);
        }

        currentResults = await res.json();
        plotResults(currentResults);
        renderResults(currentResults);
        persistPreferences();
        showToast(`Found ${currentResults.length} matching properties.`, "success");

        document.getElementById("btnSaveSearch").disabled = currentResults.length === 0;
    } catch (err) {
        console.error("Search failed:", err);
        document.getElementById("resultsPanel").innerHTML =
            '<p class="text-danger text-center small">Search failed. Please try again.</p>';
        showToast("Search failed. Please try again.", "danger");
        announce("Search failed. Please try again.");
    } finally {
        done();
        btn.replaceChildren(PortfolioDom.el("i", { class: "fa-solid fa-magnifying-glass me-1", "aria-hidden": "true" }), "Find Homes");
        updateWeightStatus();
    }
}

// ============================================================
// Save / Load Searches
// ============================================================

async function saveSearch() {
    const prefs = getPreferences();
    const name = prompt("Name this search:", `Search ${new Date().toLocaleDateString()}`);
    if (!name) return;

    try {
        const res = await apiFetch(HOME_FINDER_ROUTES.searches, {
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

async function loadSavedSearches() {
    const body = document.getElementById("savedSearchesBody");
    if (!body) return;

    body.innerHTML = '<p class="theme-text-muted text-center small">Loading\u2026</p>';

    try {
        const res = await apiFetch(HOME_FINDER_ROUTES.searches);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const searches = await res.json();

        if (!searches.length) {
            body.innerHTML = '<p class="theme-text-muted text-center small">No saved searches found.</p>';
            return;
        }

        body.innerHTML = searches.map(s => `
            <div class="card theme-card mb-2 p-2">
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <strong class="small">${PortfolioDom.escapeHtml(s.name)}</strong>
                        <div class="theme-text-muted" style="font-size:.75rem;">
                            Saved ${new Date(s.createdAt).toLocaleDateString()}
                        </div>
                    </div>
                    <div class="d-flex gap-1">
                        <button class="btn btn-sm btn-outline-accent btn-load-search" data-search-id="${s.id}">
                            <i class="fa-solid fa-upload me-1" aria-hidden="true"></i>Load
                        </button>
                        <button class="btn btn-sm btn-outline-danger btn-delete-search" data-search-id="${s.id}" aria-label="Delete saved search ${PortfolioDom.escapeHtml(s.name)}">
                            <i class="fa-solid fa-trash" aria-hidden="true"></i>
                        </button>
                    </div>
                </div>
            </div>
        `).join("");

        body.querySelectorAll(".btn-load-search").forEach(btn => {
            btn.addEventListener("click", async () => {
                const id = btn.dataset.searchId;
                await applySavedSearch(id);
                const modal = bootstrap.Modal.getInstance(document.getElementById("savedSearchesModal"));
                modal?.hide();
            });
        });

        body.querySelectorAll(".btn-delete-search").forEach(btn => {
            btn.addEventListener("click", async () => {
                const id = btn.dataset.searchId;
                if (!await confirmDialog("Delete this saved search?")) return;
                try {
                    const res = await apiFetch(`${HOME_FINDER_ROUTES.searches}/${id}`, { method: "DELETE" });
                    if (res.ok) {
                        btn.closest(".card").remove();
                        showToast("Search deleted.", "success");
                    }
                } catch (err) {
                    console.error("Delete failed:", err);
                    showToast("Delete failed.", "danger");
                }
            });
        });
    } catch (err) {
        console.error("Load searches failed:", err);
        body.innerHTML = '<p class="text-danger text-center small">Failed to load saved searches.</p>';
    }
}

async function applySavedSearch(searchId) {
    try {
        const res = await apiFetch(`${HOME_FINDER_ROUTES.searches}/${searchId}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const saved = await res.json();

        const prefs = saved.preferences || JSON.parse(saved.preferencesJson || "{}");

        for (const [sliderId, cfg] of Object.entries(sliders)) {
            const el = document.getElementById(sliderId);
            if (!el || prefs[cfg.key] === undefined) continue;

            const val = cfg.divisor ? Math.round(prefs[cfg.key] * cfg.divisor) : prefs[cfg.key];
            el.value = val;
            syncSliderLabel(sliderId);
        }

        enforceWeightBudget();
        updateAllWeightBars();
        updateWeightStatus();
        await performSearch();
        showToast(`Loaded "${saved.name}"`, "success");
    } catch (err) {
        console.error("Apply saved search failed:", err);
        showToast("Failed to load search.", "danger");
    }
}

// ============================================================
// Init
// ============================================================

document.addEventListener("DOMContentLoaded", async () => {
    // Restore previous session preferences before wiring sliders
    restorePreferences();

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