// results.js — Emergency Response Coverage Optimizer: the results panel.
// Renders an optimization result: KPI tiles, the today-versus-optimized comparison, the JSON
// summary and the station list (each station a real button that requests its isochrone).
// Publishes its entry points on window.ResponsePage (see context.js).
(function () {
    "use strict";

    var R = window.ResponsePage;
    var fmt = window.PortfolioFormat;
    var el = window.PortfolioDom.el;

    var state = R.state;
    var firstInput = R.dom.firstInput;
    var stationList = R.dom.stationList;
    var stationHeading = R.dom.stationHeading;
    var summary = R.dom.summary;
    var comparison = R.dom.comparison;
    var kpiP90 = R.dom.kpiP90;
    var kpiFirst = R.dom.kpiFirst;
    var kpiSecond = R.dom.kpiSecond;
    var kpiNfpa = R.dom.kpiNfpa;

    function render(result) {
        updateKpis(result.optimized, result.meetsNfpa1710);
        R.drawDemand(result.assignments);
        R.drawStations(result.chosenCandidateIds);
        R.drawAssignments(result.assignments);
        R.drawHistogram(result);
        buildStationList(result);

        var deltaP90 = result.baseline.p90Minutes - result.optimized.p90Minutes;
        if (result.baseline.p90Minutes > 0) {
            var within = parseFloat(firstInput.value);
            setComparison([
                el("strong", { text: "Today:" }),
                " p90 " + fmt.unit(result.baseline.p90Minutes, "min") + ", " +
                    fmt.percent(result.baseline.percentWithinFirstThreshold) + " within " + within + " min.  ",
                el("strong", { text: "Optimized:" }),
                " p90 " + fmt.unit(result.optimized.p90Minutes, "min") + ", " +
                    fmt.percent(result.optimized.percentWithinFirstThreshold) + " within " + within + " min — " +
                    (deltaP90 >= 0
                        ? "a " + fmt.number(deltaP90, { decimals: 1 }) + " minute improvement."
                        : "a " + fmt.number(Math.abs(deltaP90), { decimals: 1 }) + " minute regression.")
            ]);
        } else {
            comparison.classList.add("d-none");
        }

        summary.textContent = JSON.stringify({
            nativeAccelerated: result.nativeAccelerated,
            chosenCandidateIds: result.chosenCandidateIds,
            optimized: result.optimized,
            baseline: result.baseline,
            meetsNfpa1710: result.meetsNfpa1710,
            matrixBuildMs: result.matrixBuildMs,
            solveMs: result.solveMs,
            iterationObjectives: result.iterationObjectives
        }, null, 2);
    }

    // Replaces the comparison box content with a string or an array of strings / nodes.
    function setComparison(content) {
        var nodes = Array.isArray(content) ? content : [content];
        comparison.replaceChildren.apply(comparison, nodes);
        comparison.classList.remove("d-none");
    }

    function updateKpis(stats, meetsNfpa) {
        kpiP90.textContent = fmt.unit(stats.p90Minutes, "min");
        kpiFirst.textContent = fmt.percent(stats.percentWithinFirstThreshold);
        kpiSecond.textContent = fmt.percent(stats.percentWithinSecondThreshold);
        kpiNfpa.textContent = meetsNfpa ? "PASS" : "FAIL";
        kpiNfpa.style.color = meetsNfpa ? "var(--status-success-text)" : "var(--status-danger-text)";
    }

    // Each station is a real <button>: activating it paints that station's drive-time bands,
    // the keyboard equivalent of clicking its marker on the map (which stays mouse-only).

    function stationButton(candidate, primary, meta) {
        var button = el("button", {
            type: "button",
            class: "geo-cell-item w-100 text-start d-block",
            title: "Show drive-time bands from " + candidate.label
        },
            el("span", { class: "d-flex align-items-center justify-content-between" },
                el("span", { text: candidate.label }),
                el("strong", { text: primary })),
            el("span", { class: "geo-cell-meta d-block", text: meta }));
        button.addEventListener("click", function () { R.requestIsochrone(candidate, button); });
        return button;
    }

    function setStationList(heading, buttons) {
        stationHeading.textContent = heading;
        stationList.replaceChildren.apply(stationList, buttons);
    }

    // Before an optimization (and after Reset) the list is today's stations in service.
    function buildExistingList() {
        if (!state.scenario) { return; }
        var buttons = [];
        state.scenario.existingStationIds.forEach(function (id) {
            var candidate = state.candidateById[id];
            if (!candidate) { return; }
            buttons.push(stationButton(candidate, "In service", "in service today"));
        });
        setStationList("Stations in service today", buttons);
    }

    function buildStationList(result) {
        var loadById = {};
        var worstById = {};
        result.assignments.forEach(function (a) {
            var demand = state.demandById[a.demandPointId];
            if (!demand) { return; }
            loadById[a.assignedCandidateId] = (loadById[a.assignedCandidateId] || 0) + demand.callVolume;
            if (a.responseMinutes > (worstById[a.assignedCandidateId] || 0)) {
                worstById[a.assignedCandidateId] = a.responseMinutes;
            }
        });

        var buttons = [];
        result.chosenCandidateIds.forEach(function (id) {
            var candidate = state.candidateById[id];
            if (!candidate) { return; }
            buttons.push(stationButton(candidate, Math.round(loadById[id] || 0) + " calls",
                (candidate.isExisting ? "in service today · " : "new build · ") +
                "worst response " + fmt.unit(worstById[id] || 0, "min")));
        });
        setStationList("Chosen stations", buttons);
    }

    R.render = render;
    R.setComparison = setComparison;
    R.updateKpis = updateKpis;
    R.buildExistingList = buildExistingList;
}());
