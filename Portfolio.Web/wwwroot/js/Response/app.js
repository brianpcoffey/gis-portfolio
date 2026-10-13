// Emergency Response Coverage Optimizer — Redlands, CA | Leaflet + /api/v1/response
// Page orchestration: loads the scenario, runs optimize / reset / isochrone requests and wires the
// buttons. Classic script, no ES module syntax; it loads last and starts the page at the end.
// The rest of the page lives in sibling scripts that share the window.ResponsePage namespace:
// context.js (loads first), layers.js, histogram.js, results.js.
// Depends on the deferred shared modules: PortfolioUi, PortfolioFormat, PortfolioApi.
(function () {
    "use strict";

    var R = window.ResponsePage;
    var fmt = window.PortfolioFormat;
    var ui = window.PortfolioUi;

    var map = R.map;
    var state = R.state;
    var ISOCHRONE_BANDS = R.ISOCHRONE_BANDS;

    var alertBox = R.dom.alertBox;
    var stationsInput = R.dom.stationsInput;
    var stationsValue = R.dom.stationsValue;
    var speedInput = R.dom.speedInput;
    var firstInput = R.dom.firstInput;
    var secondInput = R.dom.secondInput;
    var optimizeBtn = R.dom.optimizeBtn;
    var isochroneBtn = R.dom.isochroneBtn;
    var resetBtn = R.dom.resetBtn;
    var summary = R.dom.summary;
    var comparison = R.dom.comparison;
    var resultsPanel = R.dom.resultsPanel;

    // Runs `opts` through PortfolioUi.runAction on `trigger`, holding the other action
    // buttons busy as well. The pending flag also stops a map click starting a second request.
    function runExclusive(trigger, opts) {
        if (state.pending) { return Promise.resolve(); }
        state.pending = true;
        var others = [optimizeBtn, isochroneBtn, resetBtn].filter(function (b) { return b !== trigger; });
        var release = ui.busy({ buttons: others });
        opts.trigger = trigger;
        opts.alert = alertBox;
        opts.region = resultsPanel;
        return ui.runAction(opts).then(function (r) { release(); state.pending = false; return r; });
    }

    function loadScenario() {
        runExclusive(resetBtn, {
            request: function () { return apiGet(window.PortfolioApi.routes.spatialCompute.response.scenario); },
            render: function (result) {
                state.scenario = result;
                indexScenario();
                R.drawDemand(null);
                R.drawStations([]);
                R.fitToScenario();
                R.drawHistogram(null);
                R.buildExistingList();
                summary.textContent = JSON.stringify({
                    scenario: state.scenario.name,
                    demandPoints: state.scenario.demandPoints.length,
                    candidateSites: state.scenario.candidates.length,
                    existingStations: state.scenario.existingStationIds.length,
                    totalCallVolume: state.scenario.totalCallVolume
                }, null, 2);
            }
            // No summary: this runs once on page load, not because the visitor asked for it.
        });
    }

    function indexScenario() {
        state.candidateById = {};
        state.demandById = {};
        state.scenario.candidates.forEach(function (c) { state.candidateById[c.id] = c; });
        state.scenario.demandPoints.forEach(function (d) { state.demandById[d.id] = d; });
    }

    function optimize() {
        if (!state.scenario) {
            alertBox.show("The scenario has not loaded yet.");
            return;
        }

        runExclusive(optimizeBtn, {
            request: function () {
                return apiPost(window.PortfolioApi.routes.spatialCompute.response.optimize, {
                    demandPoints: state.scenario.demandPoints,
                    candidates: state.scenario.candidates,
                    existingStationIds: state.scenario.existingStationIds,
                    stationCount: parseInt(stationsInput.value, 10),
                    objectiveMode: parseInt(selectedObjective(), 10),
                    avgSpeedKmh: parseFloat(speedInput.value),
                    firstThresholdMinutes: parseFloat(firstInput.value),
                    secondThresholdMinutes: parseFloat(secondInput.value),
                    maxIterations: 50
                });
            },
            render: function (result) {
                state.lastResult = result;
                R.render(result);
            },
            summary: function (result) {
                var n = result.chosenCandidateIds.length;
                return "Optimization complete: " + n + (n === 1 ? " station" : " stations") + " selected. " +
                    "p90 response " + fmt.unit(result.optimized.p90Minutes, "min") + ", " +
                    fmt.percent(result.optimized.percentWithinFirstThreshold) + " within " +
                    parseFloat(firstInput.value) + " minutes. NFPA 1710 " + (result.meetsNfpa1710 ? "met." : "not met.");
            }
        });
    }

    function selectedObjective() {
        var checked = document.querySelector("input[name='responseObjective']:checked");
        return checked ? checked.value : "1";
    }

    function resetToExisting() {
        R.clearIsochrone();
        if (state.assignmentLayer) { map.removeLayer(state.assignmentLayer); state.assignmentLayer = null; }
        R.drawDemand(state.lastResult ? state.lastResult.baselineAssignments : null);
        R.drawStations([]);
        R.buildExistingList();
        if (state.lastResult) {
            R.updateKpis(state.lastResult.baseline, state.lastResult.baseline.percentWithinFirstThreshold >= 90);
            R.drawHistogram(state.lastResult);
            var text = "Showing today's stations: p90 " + fmt.unit(state.lastResult.baseline.p90Minutes, "min") + ", " +
                fmt.percent(state.lastResult.baseline.percentWithinFirstThreshold) + " within " +
                parseFloat(firstInput.value) + " minutes.";
            R.setComparison(text);
            ui.announce(text);
        } else {
            comparison.classList.add("d-none");
        }
    }

    function showIsochroneForFirstStation() {
        if (!state.scenario) { return; }
        var stationId = state.lastResult && state.lastResult.chosenCandidateIds.length > 0
            ? state.lastResult.chosenCandidateIds[0]
            : state.scenario.existingStationIds[0];
        if (stationId == null) { return; }
        requestIsochrone(state.candidateById[stationId], isochroneBtn);
    }

    // `trigger` is the control that started the request (a station button or the toolbar button);
    // map-marker clicks pass the toolbar button. Focus stays on it while the request runs.
    function requestIsochrone(candidate, trigger) {
        if (!candidate) { return; }

        runExclusive(trigger || isochroneBtn, {
            request: function () {
                return apiPost(window.PortfolioApi.routes.spatialCompute.response.isochrone, {
                    originNodeId: candidate.nodeId,
                    avgSpeedKmh: parseFloat(speedInput.value),
                    bandMinutes: ISOCHRONE_BANDS
                });
            },
            render: function (result) { R.drawIsochrone(result, candidate); },
            summary: function () { return comparison.textContent; }
        });
    }

    // layers.js (map-marker clicks) and results.js (station buttons) request isochrones through here.
    R.requestIsochrone = requestIsochrone;

    optimizeBtn.addEventListener("click", optimize);
    isochroneBtn.addEventListener("click", showIsochroneForFirstStation);
    resetBtn.addEventListener("click", resetToExisting);
    stationsInput.addEventListener("input", function () {
        stationsValue.textContent = stationsInput.value;
    });

    loadScenario();
}());
