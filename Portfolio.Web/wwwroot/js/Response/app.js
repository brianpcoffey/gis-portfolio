// Emergency Response Coverage Optimizer — Redlands, CA | Leaflet + /api/v1/response
// Self-contained IIFE, no ES module syntax.
// Depends on the deferred shared modules: PortfolioUi, PortfolioFormat, PortfolioCharts, PortfolioDom,
// PortfolioMap (leaflet-setup.js, loaded after leaflet.js).
(function () {
    "use strict";

    var charts = window.PortfolioCharts;
    var fmt = window.PortfolioFormat;
    var ui = window.PortfolioUi;
    var el = window.PortfolioDom.el;

    // Isochrone bands in minutes. The first two straddle the NFPA 1710 four-minute
    // first-due threshold; the Redlands network is small enough that a plain 4/8/12
    // split would paint almost everything a single colour.
    var ISOCHRONE_BANDS = [2, 4, 8, 12];

    // Response-time ramp used for the demand circles, in minutes.
    var RAMP_MAX_MINUTES = 12;

    // The OpenStreetMap raster tiles stay light in both themes, so the neutral marker colour
    // drawn over them is fixed; every coloured series uses the (theme-independent) status fills.
    var SLATE = "#64748b";

    // ── DOM refs ──────────────────────────────────────────────────────────
    var alertBox = ui.inlineAlert(document.getElementById("responseAlert"));
    var stationsInput = document.getElementById("responseStations");
    var stationsValue = document.getElementById("responseStationsValue");
    var speedInput = document.getElementById("responseSpeed");
    var firstInput = document.getElementById("responseFirstThreshold");
    var secondInput = document.getElementById("responseSecondThreshold");
    var showIsochrone = document.getElementById("responseShowIsochrone");
    var optimizeBtn = document.getElementById("responseOptimizeBtn");
    var isochroneBtn = document.getElementById("responseIsochroneBtn");
    var resetBtn = document.getElementById("responseResetBtn");
    var stationList = document.getElementById("responseStationList");
    var stationHeading = document.getElementById("responseStationHeading");
    var summary = document.getElementById("responseSummary");
    var comparison = document.getElementById("responseComparison");
    var histogramSvg = document.getElementById("responseHistogramSvg");
    var resultsPanel = histogramSvg.closest(".card");
    var kpiP90 = document.getElementById("responseKpiP90");
    var kpiFirst = document.getElementById("responseKpiFirst");
    var kpiSecond = document.getElementById("responseKpiSecond");
    var kpiNfpa = document.getElementById("responseKpiNfpa");

    // ── State ─────────────────────────────────────────────────────────────
    var scenario = null;
    var candidateById = {};
    var demandById = {};
    var lastResult = null;
    var demandLayer = null;
    var assignmentLayer = null;
    var isochroneLayer = null;
    var stationMarkers = {};
    var pending = false;

    // ── Leaflet map ───────────────────────────────────────────────────────
    // PortfolioMap supplies the OSM tile layer, attribution and canvas rendering
    // (preferCanvas keeps hundreds of demand circles and assignment lines smooth).
    var map = window.PortfolioMap.create("responseMap", { label: "Redlands response coverage map" });

    map.createPane("isochrone");
    map.getPane("isochrone").style.zIndex = 380;

    // ── Bootstrap ────────────────────────────────────────────────────────
    optimizeBtn.addEventListener("click", optimize);
    isochroneBtn.addEventListener("click", showIsochroneForFirstStation);
    resetBtn.addEventListener("click", resetToExisting);
    stationsInput.addEventListener("input", function () {
        stationsValue.textContent = stationsInput.value;
    });

    loadScenario();

    // ── Data ────────────────────────────────────────────────────────────────

    // Runs `opts` through PortfolioUi.runAction on `trigger`, holding the other action
    // buttons busy as well. The pending flag also stops a map click starting a second request.
    function runExclusive(trigger, opts) {
        if (pending) { return Promise.resolve(); }
        pending = true;
        var others = [optimizeBtn, isochroneBtn, resetBtn].filter(function (b) { return b !== trigger; });
        var release = ui.busy({ buttons: others });
        opts.trigger = trigger;
        opts.alert = alertBox;
        opts.region = resultsPanel;
        return ui.runAction(opts).then(function (r) { release(); pending = false; return r; });
    }

    function loadScenario() {
        runExclusive(resetBtn, {
            request: function () { return apiGet(window.PortfolioApi.routes.spatialCompute.response.scenario); },
            render: function (result) {
                scenario = result;
                indexScenario();
                drawDemand(null);
                drawStations([]);
                fitToScenario();
                drawHistogram(null);
                buildExistingList();
                summary.textContent = JSON.stringify({
                    scenario: scenario.name,
                    demandPoints: scenario.demandPoints.length,
                    candidateSites: scenario.candidates.length,
                    existingStations: scenario.existingStationIds.length,
                    totalCallVolume: scenario.totalCallVolume
                }, null, 2);
            },
            summary: function () {
                return "Scenario loaded: " + scenario.demandPoints.length + " demand points and " +
                    scenario.existingStationIds.length + " stations in service today.";
            }
        });
    }

    function indexScenario() {
        candidateById = {};
        demandById = {};
        scenario.candidates.forEach(function (c) { candidateById[c.id] = c; });
        scenario.demandPoints.forEach(function (d) { demandById[d.id] = d; });
    }

    function optimize() {
        if (!scenario) {
            alertBox.show("The scenario has not loaded yet.");
            return;
        }

        runExclusive(optimizeBtn, {
            request: function () {
                return apiPost(window.PortfolioApi.routes.spatialCompute.response.optimize, {
                    demandPoints: scenario.demandPoints,
                    candidates: scenario.candidates,
                    existingStationIds: scenario.existingStationIds,
                    stationCount: parseInt(stationsInput.value, 10),
                    objectiveMode: parseInt(selectedObjective(), 10),
                    avgSpeedKmh: parseFloat(speedInput.value),
                    firstThresholdMinutes: parseFloat(firstInput.value),
                    secondThresholdMinutes: parseFloat(secondInput.value),
                    maxIterations: 50
                });
            },
            render: function (result) {
                lastResult = result;
                render(result);
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
        clearIsochrone();
        if (assignmentLayer) { map.removeLayer(assignmentLayer); assignmentLayer = null; }
        drawDemand(lastResult ? lastResult.baselineAssignments : null);
        drawStations([]);
        buildExistingList();
        if (lastResult) {
            updateKpis(lastResult.baseline, lastResult.baseline.percentWithinFirstThreshold >= 90);
            drawHistogram(lastResult);
            var text = "Showing today's stations: p90 " + fmt.unit(lastResult.baseline.p90Minutes, "min") + ", " +
                fmt.percent(lastResult.baseline.percentWithinFirstThreshold) + " within " +
                parseFloat(firstInput.value) + " minutes.";
            setComparison(text);
            ui.announce(text);
        } else {
            comparison.classList.add("d-none");
        }
    }

    function showIsochroneForFirstStation() {
        if (!scenario) { return; }
        var stationId = lastResult && lastResult.chosenCandidateIds.length > 0
            ? lastResult.chosenCandidateIds[0]
            : scenario.existingStationIds[0];
        if (stationId == null) { return; }
        requestIsochrone(candidateById[stationId], isochroneBtn);
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
            render: function (result) { drawIsochrone(result, candidate); },
            summary: function () { return comparison.textContent; }
        });
    }

    // ── Rendering ───────────────────────────────────────────────────────────

    function render(result) {
        updateKpis(result.optimized, result.meetsNfpa1710);
        drawDemand(result.assignments);
        drawStations(result.chosenCandidateIds);
        drawAssignments(result.assignments);
        drawHistogram(result);
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

    function drawDemand(assignments) {
        if (demandLayer) { map.removeLayer(demandLayer); demandLayer = null; }
        if (!scenario) { return; }

        var minutesById = {};
        if (assignments) {
            assignments.forEach(function (a) { minutesById[a.demandPointId] = a.responseMinutes; });
        }

        var maxVolume = 1;
        scenario.demandPoints.forEach(function (d) {
            if (d.callVolume > maxVolume) { maxVolume = d.callVolume; }
        });

        var ramp = responseRamp();
        var group = L.layerGroup();
        scenario.demandPoints.forEach(function (point) {
            var minutes = minutesById[point.id];
            var radius = 2.5 + Math.sqrt(point.callVolume / maxVolume) * 7;
            var marker = L.circleMarker([point.latitude, point.longitude], {
                radius: radius,
                stroke: false,
                fillColor: minutes == null ? SLATE : responseColor(minutes, ramp),
                fillOpacity: minutes == null ? 0.45 : 0.8
            });
            marker.bindTooltip(point.callVolume + " calls/yr" +
                (minutes == null ? "" : " · " + fmt.unit(minutes, "min")),
                { direction: "top" });
            marker.addTo(group);
        });

        group.addTo(map);
        demandLayer = group;
    }

    function drawAssignments(assignments) {
        if (assignmentLayer) { map.removeLayer(assignmentLayer); assignmentLayer = null; }
        if (!assignments) { return; }

        // A faint line from each demand point to its first-due station. The starburst
        // makes the districting legible without drawing any polygon boundary.
        var group = L.layerGroup();
        assignments.forEach(function (a) {
            var demand = demandById[a.demandPointId];
            var station = candidateById[a.assignedCandidateId];
            if (!demand || !station) { return; }
            L.polyline([[demand.latitude, demand.longitude], [station.latitude, station.longitude]], {
                color: SLATE,
                opacity: 0.35,
                weight: 1,
                interactive: false
            }).addTo(group);
        });

        group.addTo(map);
        assignmentLayer = group;
    }

    function drawStations(chosenIds) {
        Object.keys(stationMarkers).forEach(function (id) { map.removeLayer(stationMarkers[id]); });
        stationMarkers = {};
        if (!scenario) { return; }

        var chosen = {};
        (chosenIds || []).forEach(function (id) { chosen[id] = true; });
        var chosenFill = charts.status("info");
        var outline = charts.token("on-dark");

        scenario.candidates.forEach(function (candidate) {
            var isChosen = !!chosen[candidate.id];
            var marker = L.circleMarker([candidate.latitude, candidate.longitude], {
                radius: isChosen ? 10 : candidate.isExisting ? 7 : 4,
                fillColor: isChosen ? chosenFill : candidate.isExisting ? SLATE : "transparent",
                fillOpacity: isChosen ? 0.95 : candidate.isExisting ? 0.85 : 0,
                color: isChosen || candidate.isExisting ? outline : SLATE,
                weight: isChosen ? 2.5 : 1.2
            }).addTo(map);

            var tip = el("span", {}, el("strong", { text: candidate.label }));
            if (candidate.isExisting) { tip.append(document.createElement("br"), "In service today"); }
            if (isChosen) { tip.append(document.createElement("br"), "Selected by the optimizer"); }
            marker.bindTooltip(tip, { direction: "top" });

            marker.on("click", function () {
                if (showIsochrone.checked) { requestIsochrone(candidate, isochroneBtn); }
            });

            stationMarkers[candidate.id] = marker;
        });
    }

    function drawIsochrone(result, candidate) {
        clearIsochrone();

        var ramp = responseRamp();
        var group = L.layerGroup();
        result.nodes.forEach(function (node) {
            L.circleMarker([node.latitude, node.longitude], {
                pane: "isochrone",
                radius: 3,
                stroke: false,
                fillColor: bandColor(Math.min(node.bandIndex, ISOCHRONE_BANDS.length), ramp),
                fillOpacity: 0.55,
                interactive: false
            }).addTo(group);
        });

        group.addTo(map);
        isochroneLayer = group;

        var legend = ISOCHRONE_BANDS.map(function (band, i) {
            return "≤" + band + " min: " + result.bandCounts[i];
        }).join(" · ");
        setComparison([
            el("strong", { text: "Isochrone from " + candidate.label + ":" }),
            " " + legend + " · beyond " + ISOCHRONE_BANDS[ISOCHRONE_BANDS.length - 1] + " min: " +
                result.bandCounts[ISOCHRONE_BANDS.length] + " · unreachable: " + result.unreachableNodes
        ]);
    }

    function clearIsochrone() {
        if (isochroneLayer) { map.removeLayer(isochroneLayer); isochroneLayer = null; }
    }

    // ── Station list ────────────────────────────────────────────────────────
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
        button.addEventListener("click", function () { requestIsochrone(candidate, button); });
        return button;
    }

    function setStationList(heading, buttons) {
        stationHeading.textContent = heading;
        stationList.replaceChildren.apply(stationList, buttons);
    }

    // Before an optimization (and after Reset) the list is today's stations in service.
    function buildExistingList() {
        if (!scenario) { return; }
        var buttons = [];
        scenario.existingStationIds.forEach(function (id) {
            var candidate = candidateById[id];
            if (!candidate) { return; }
            buttons.push(stationButton(candidate, "In service", "in service today"));
        });
        setStationList("Stations in service today", buttons);
    }

    function buildStationList(result) {
        var loadById = {};
        var worstById = {};
        result.assignments.forEach(function (a) {
            var demand = demandById[a.demandPointId];
            if (!demand) { return; }
            loadById[a.assignedCandidateId] = (loadById[a.assignedCandidateId] || 0) + demand.callVolume;
            if (a.responseMinutes > (worstById[a.assignedCandidateId] || 0)) {
                worstById[a.assignedCandidateId] = a.responseMinutes;
            }
        });

        var buttons = [];
        result.chosenCandidateIds.forEach(function (id) {
            var candidate = candidateById[id];
            if (!candidate) { return; }
            buttons.push(stationButton(candidate, Math.round(loadById[id] || 0) + " calls",
                (candidate.isExisting ? "in service today · " : "new build · ") +
                "worst response " + fmt.unit(worstById[id] || 0, "min")));
        });
        setStationList("Chosen stations", buttons);
    }

    // ── Response-time histogram ─────────────────────────────────────────────

    function drawHistogram(result) {
        charts.svg.clear(histogramSvg);

        var left = 11, right = 97, top = 5, bottom = 48;
        var svg = charts.svg;

        svg.line(histogramSvg, left, top, left, bottom, { width: 0.3 });
        svg.line(histogramSvg, left, bottom, right, bottom, { width: 0.3 });
        svg.text(histogramSvg, (left + right) / 2, 58, "Response Time (minutes)", { size: 2.6, anchor: "middle" });
        svg.text(histogramSvg, 3.0, (top + bottom) / 2, "Calls / year", { size: 2.6, anchor: "middle", rotate: -90 });

        if (!result || !result.assignments || result.assignments.length === 0) {
            svg.text(histogramSvg, (left + right) / 2, (top + bottom) / 2,
                "Run an optimization to build the distribution", { size: 3, anchor: "middle" });
            charts.describe(histogramSvg, "Response-time histogram. No optimization has been run yet.");
            return;
        }

        var binCount = 14;
        var optimizedBins = binWeights(result.assignments, binCount);
        var baselineBins = binWeights(result.baselineAssignments, binCount);

        var maxWeight = 1;
        var i;
        for (i = 0; i < binCount; i++) {
            if (optimizedBins[i] > maxWeight) { maxWeight = optimizedBins[i]; }
            if (baselineBins[i] > maxWeight) { maxWeight = baselineBins[i]; }
        }

        var xFor = charts.scale.linear([0, binCount], [left, right]);
        var yFor = charts.scale.linear([0, maxWeight], [bottom, top]);

        // Y gridlines at quarter steps.
        var q;
        for (q = 1; q <= 4; q++) {
            var gy = yFor(maxWeight * q / 4);
            svg.line(histogramSvg, left, gy, right, gy, { stroke: "var(--border)", width: 0.18 });
            svg.text(histogramSvg, left - 1.2, gy + 0.9, formatCount(maxWeight * q / 4), { size: 2.2, anchor: "end" });
        }

        // Optimized bars.
        var barWidth = (right - left) / binCount;
        for (i = 0; i < binCount; i++) {
            if (optimizedBins[i] <= 0) { continue; }
            histogramSvg.appendChild(svg.el("rect", {
                x: (xFor(i) + barWidth * 0.12).toFixed(2),
                y: yFor(optimizedBins[i]).toFixed(2),
                width: (barWidth * 0.76).toFixed(2),
                height: Math.max(bottom - yFor(optimizedBins[i]), 0).toFixed(2),
                fill: "var(--chart-series)",
                "fill-opacity": "0.85"
            }));
        }

        // Baseline as a translucent step outline so the improvement reads in one glance.
        if (result.baselineAssignments && result.baselineAssignments.length > 0) {
            var points = [];
            for (i = 0; i < binCount; i++) {
                points.push(xFor(i).toFixed(2) + "," + yFor(baselineBins[i]).toFixed(2));
                points.push(xFor(i + 1).toFixed(2) + "," + yFor(baselineBins[i]).toFixed(2));
            }
            histogramSvg.appendChild(svg.el("polyline", {
                points: points.join(" "),
                fill: "none",
                stroke: "var(--text-muted)",
                "stroke-width": "0.4",
                "stroke-dasharray": "1.4 1"
            }));
            svg.text(histogramSvg, right, top + 2, "outline = today", { size: 2.2, anchor: "end" });
        }

        // X ticks every two minutes.
        for (i = 0; i <= binCount; i += 2) {
            var tx = xFor(i);
            svg.line(histogramSvg, tx, bottom, tx, bottom + 1.1, { width: 0.25 });
            svg.text(histogramSvg, tx, bottom + 4.2, String(i), { size: 2.3, anchor: "middle" });
        }

        // NFPA threshold markers.
        markMinute(parseFloat(firstInput.value), "first-due", "var(--status-success-fill)", "var(--status-success-text)",
            top, bottom, binCount, xFor);
        markMinute(parseFloat(secondInput.value), "ALS", "var(--status-warning-fill)", "var(--status-warning-text)",
            top, bottom, binCount, xFor);

        // Shade everything past the optimized p90 — the tail the standard is written about.
        var p90 = result.optimized.p90Minutes;
        if (p90 > 0 && p90 < binCount) {
            histogramSvg.appendChild(svg.el("rect", {
                x: xFor(p90).toFixed(2),
                y: String(top),
                width: Math.max(right - xFor(p90), 0).toFixed(2),
                height: (bottom - top).toFixed(2),
                fill: "var(--status-danger-fill)",
                "fill-opacity": "0.10"
            }));
            svg.text(histogramSvg, Math.min(xFor(p90) + 1, right - 8), top + 5.5,
                "p90 " + fmt.number(p90, { decimals: 1 }), { size: 2.2, anchor: "start", fill: "var(--status-danger-text)" });
        }

        charts.describe(histogramSvg, "Response-time histogram, call volume per one-minute bin. Optimized p90 is " +
            fmt.unit(p90, "min") + "; the dashed outline shows today's stations.");
    }

    function markMinute(minute, label, lineColor, textColor, top, bottom, binCount, xFor) {
        if (!isFinite(minute) || minute <= 0 || minute > binCount) { return; }
        var x = xFor(minute);
        var line = charts.svg.line(histogramSvg, x, top, x, bottom, { stroke: lineColor, width: 0.35 });
        line.setAttribute("stroke-dasharray", "1.2 1");
        charts.svg.text(histogramSvg, x, top - 0.6, label, { size: 2.2, anchor: "middle", fill: textColor });
    }

    // Sums call volume into one-minute bins, so the y axis is calls rather than incidents.
    function binWeights(assignments, binCount) {
        var bins = [];
        var i;
        for (i = 0; i < binCount; i++) { bins.push(0); }
        if (!assignments) { return bins; }

        assignments.forEach(function (a) {
            var demand = demandById[a.demandPointId];
            if (!demand) { return; }
            var bin = Math.floor(a.responseMinutes);
            if (bin < 0) { bin = 0; }
            if (bin >= binCount) { bin = binCount - 1; }
            bins[bin] += demand.callVolume;
        });

        return bins;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // Green -> amber -> red from the status fills (resolved once per draw).
    function responseRamp() {
        return { low: charts.status("success"), mid: charts.status("warning"), high: charts.status("danger") };
    }

    function responseColor(minutes, ramp) {
        var t = Math.max(0, Math.min(1, minutes / RAMP_MAX_MINUTES));
        if (t < 0.5) {
            return charts.mix(ramp.low, ramp.mid, t / 0.5);
        }
        return charts.mix(ramp.mid, ramp.high, (t - 0.5) / 0.5);
    }

    // Isochrone band i of n bands (plus one "beyond" band) along the same ramp.
    function bandColor(index, ramp) {
        return responseColor(index / ISOCHRONE_BANDS.length * RAMP_MAX_MINUTES, ramp);
    }

    function fitToScenario() {
        var lats = [], lngs = [];
        scenario.demandPoints.forEach(function (d) { lats.push(d.latitude); lngs.push(d.longitude); });
        scenario.candidates.forEach(function (c) { lats.push(c.latitude); lngs.push(c.longitude); });
        if (lats.length === 0) { return; }
        map.fitBounds([[Math.min.apply(null, lats), Math.min.apply(null, lngs)],
                       [Math.max.apply(null, lats), Math.max.apply(null, lngs)]], { padding: [24, 24] });
    }

    function formatCount(value) {
        if (value >= 1000) { return fmt.number(value / 1000, { decimals: 1 }) + "k"; }
        return String(Math.round(value));
    }
}());
