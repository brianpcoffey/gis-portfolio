// context.js — Emergency Response Coverage Optimizer: the shared window.ResponsePage namespace.
// Redlands, CA | Leaflet + /api/v1/response. Classic deferred script, no ES module syntax.
//
// The page script is split into scripts that share this one object. Load order (set in
// Pages/Projects/Response/Index.cshtml, after leaflet.js and leaflet-setup.js):
//   context.js, layers.js, histogram.js, results.js, app.js
// This file loads first and owns everything the others share: the isochrone bands, the DOM
// references, the mutable scenario / layer state and the Leaflet map. The others read ResponsePage.*
// when they load and call each other through it at event time, after every script has run.
// (The namespace is not called "Response": that is the Fetch API class.)
//
// Depends on the deferred shared modules: PortfolioUi, PortfolioDom, PortfolioMap (leaflet-setup.js,
// loaded after leaflet.js).
(function () {
    "use strict";

    var ui = window.PortfolioUi;

    // Isochrone bands in minutes. The first two straddle the NFPA 1710 four-minute
    // first-due threshold; the Redlands network is small enough that a plain 4/8/12
    // split would paint almost everything a single colour.
    var ISOCHRONE_BANDS = [2, 4, 8, 12];

    // ── DOM refs ──────────────────────────────────────────────────────────
    var histogramSvg = document.getElementById("responseHistogramSvg");
    var dom = {
        alertBox: ui.inlineAlert(document.getElementById("responseAlert")),
        stationsInput: document.getElementById("responseStations"),
        stationsValue: document.getElementById("responseStationsValue"),
        speedInput: document.getElementById("responseSpeed"),
        firstInput: document.getElementById("responseFirstThreshold"),
        secondInput: document.getElementById("responseSecondThreshold"),
        showIsochrone: document.getElementById("responseShowIsochrone"),
        optimizeBtn: document.getElementById("responseOptimizeBtn"),
        isochroneBtn: document.getElementById("responseIsochroneBtn"),
        resetBtn: document.getElementById("responseResetBtn"),
        stationList: document.getElementById("responseStationList"),
        stationHeading: document.getElementById("responseStationHeading"),
        summary: document.getElementById("responseSummary"),
        comparison: document.getElementById("responseComparison"),
        histogramSvg: histogramSvg,
        resultsPanel: histogramSvg.closest(".card"),
        kpiP90: document.getElementById("responseKpiP90"),
        kpiFirst: document.getElementById("responseKpiFirst"),
        kpiSecond: document.getElementById("responseKpiSecond"),
        kpiNfpa: document.getElementById("responseKpiNfpa")
    };

    // ── State ─────────────────────────────────────────────────────────────
    var state = {
        scenario: null,
        candidateById: {},
        demandById: {},
        lastResult: null,
        demandLayer: null,
        assignmentLayer: null,
        isochroneLayer: null,
        stationMarkers: {},
        pending: false
    };

    // ── Leaflet map ───────────────────────────────────────────────────────
    // PortfolioMap supplies the OSM tile layer, attribution and canvas rendering
    // (preferCanvas keeps hundreds of demand circles and assignment lines smooth).
    var map = window.PortfolioMap.create("responseMap", { label: "Redlands response coverage map" });

    map.createPane("isochrone");
    map.getPane("isochrone").style.zIndex = 380;

    window.ResponsePage = {
        ISOCHRONE_BANDS: ISOCHRONE_BANDS,
        dom: dom,
        state: state,
        map: map
    };
}());
