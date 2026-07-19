// Emergency Response Coverage Optimizer — Redlands, CA | Leaflet + /api/v1/response
// Self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var SVGNS = "http://www.w3.org/2000/svg";

    // Isochrone bands in minutes. The first two straddle the NFPA 1710 four-minute
    // first-due threshold; the Redlands network is small enough that a plain 4/8/12
    // split would paint almost everything a single colour.
    var ISOCHRONE_BANDS = [2, 4, 8, 12];
    var BAND_COLORS = ["#2f9e44", "#74b816", "#f59f00", "#e8590c", "#c92a2a"];

    // Response-time ramp used for the demand circles, in minutes.
    var RAMP_MAX_MINUTES = 12;

    // ── DOM refs ──────────────────────────────────────────────────────────
    var alertBox = document.getElementById("responseAlert");
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
    var summary = document.getElementById("responseSummary");
    var comparison = document.getElementById("responseComparison");
    var histogramSvg = document.getElementById("responseHistogramSvg");
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

    // ── Leaflet map ───────────────────────────────────────────────────────
    // preferCanvas keeps hundreds of demand circles and assignment lines smooth.
    var map = L.map("responseMap", {
        center: [34.0565, -117.1850],
        zoom: 13,
        zoomControl: true,
        preferCanvas: true
    });

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

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

    function loadScenario() {
        hideAlert();
        setBusy(true);
        apiGet(window.PortfolioApi.routes.spatialCompute.response.scenario)
            .then(function (result) {
                scenario = result;
                indexScenario();
                drawDemand(null);
                drawStations([]);
                fitToScenario();
                drawHistogram(null);
                summary.textContent = JSON.stringify({
                    scenario: scenario.name,
                    demandPoints: scenario.demandPoints.length,
                    candidateSites: scenario.candidates.length,
                    existingStations: scenario.existingStationIds.length,
                    totalCallVolume: scenario.totalCallVolume
                }, null, 2);
            })
            .catch(function (err) { showAlert(err.message || "Failed to load the response scenario."); })
            .finally(function () { setBusy(false); });
    }

    function indexScenario() {
        candidateById = {};
        demandById = {};
        scenario.candidates.forEach(function (c) { candidateById[c.id] = c; });
        scenario.demandPoints.forEach(function (d) { demandById[d.id] = d; });
    }

    function optimize() {
        if (!scenario) {
            showAlert("The scenario has not loaded yet.");
            return;
        }

        hideAlert();
        setBusy(true);

        apiPost(window.PortfolioApi.routes.spatialCompute.response.optimize, {
            demandPoints: scenario.demandPoints,
            candidates: scenario.candidates,
            existingStationIds: scenario.existingStationIds,
            stationCount: parseInt(stationsInput.value, 10),
            objectiveMode: parseInt(selectedObjective(), 10),
            avgSpeedKmh: parseFloat(speedInput.value),
            firstThresholdMinutes: parseFloat(firstInput.value),
            secondThresholdMinutes: parseFloat(secondInput.value),
            maxIterations: 50
        })
            .then(function (result) {
                lastResult = result;
                render(result);
            })
            .catch(function (err) { showAlert(err.message || "Optimization failed."); })
            .finally(function () { setBusy(false); });
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
        if (lastResult) {
            updateKpis(lastResult.baseline, lastResult.baseline.percentWithinFirstThreshold >= 90);
            drawHistogram(lastResult);
            comparison.textContent = "Showing today's stations: p90 " +
                lastResult.baseline.p90Minutes.toFixed(2) + " min, " +
                lastResult.baseline.percentWithinFirstThreshold.toFixed(1) + "% within " +
                parseFloat(firstInput.value) + " minutes.";
            comparison.classList.remove("d-none");
        } else {
            comparison.classList.add("d-none");
        }
        stationList.innerHTML = "";
    }

    function showIsochroneForFirstStation() {
        if (!scenario) { return; }
        var stationId = lastResult && lastResult.chosenCandidateIds.length > 0
            ? lastResult.chosenCandidateIds[0]
            : scenario.existingStationIds[0];
        if (stationId == null) { return; }
        requestIsochrone(candidateById[stationId]);
    }

    function requestIsochrone(candidate) {
        if (!candidate) { return; }

        hideAlert();
        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.response.isochrone, {
            originNodeId: candidate.nodeId,
            avgSpeedKmh: parseFloat(speedInput.value),
            bandMinutes: ISOCHRONE_BANDS
        })
            .then(function (result) { drawIsochrone(result, candidate); })
            .catch(function (err) { showAlert(err.message || "Isochrone calculation failed."); })
            .finally(function () { setBusy(false); });
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
            comparison.innerHTML = "<strong>Today:</strong> p90 " + result.baseline.p90Minutes.toFixed(2) +
                " min, " + result.baseline.percentWithinFirstThreshold.toFixed(1) + "% within " +
                parseFloat(firstInput.value) + " min. &nbsp;<strong>Optimized:</strong> p90 " +
                result.optimized.p90Minutes.toFixed(2) + " min, " +
                result.optimized.percentWithinFirstThreshold.toFixed(1) + "% within " +
                parseFloat(firstInput.value) + " min &mdash; " +
                (deltaP90 >= 0 ? "a " + deltaP90.toFixed(2) + " minute improvement." : "a " + Math.abs(deltaP90).toFixed(2) + " minute regression.");
            comparison.classList.remove("d-none");
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

    function updateKpis(stats, meetsNfpa) {
        kpiP90.textContent = stats.p90Minutes.toFixed(2) + " min";
        kpiFirst.textContent = stats.percentWithinFirstThreshold.toFixed(1) + "%";
        kpiSecond.textContent = stats.percentWithinSecondThreshold.toFixed(1) + "%";
        kpiNfpa.textContent = meetsNfpa ? "PASS" : "FAIL";
        kpiNfpa.style.color = meetsNfpa ? "#2f9e44" : "#c92a2a";
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

        var group = L.layerGroup();
        scenario.demandPoints.forEach(function (point) {
            var minutes = minutesById[point.id];
            var radius = 2.5 + Math.sqrt(point.callVolume / maxVolume) * 7;
            var marker = L.circleMarker([point.latitude, point.longitude], {
                radius: radius,
                stroke: false,
                fillColor: minutes == null ? "#64748b" : responseColor(minutes),
                fillOpacity: minutes == null ? 0.45 : 0.8
            });
            marker.bindTooltip(point.callVolume + " calls/yr" +
                (minutes == null ? "" : " · " + minutes.toFixed(2) + " min"),
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
                color: "rgba(100,116,139,0.35)",
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

        scenario.candidates.forEach(function (candidate) {
            var isChosen = !!chosen[candidate.id];
            var marker = L.circleMarker([candidate.latitude, candidate.longitude], {
                radius: isChosen ? 10 : candidate.isExisting ? 7 : 4,
                fillColor: isChosen ? "#2563eb" : candidate.isExisting ? "#64748b" : "transparent",
                fillOpacity: isChosen ? 0.95 : candidate.isExisting ? 0.85 : 0,
                color: isChosen ? "#fff" : candidate.isExisting ? "#fff" : "#94a3b8",
                weight: isChosen ? 2.5 : 1.2
            }).addTo(map);

            marker.bindTooltip("<strong>" + escapeHtml(candidate.label) + "</strong>" +
                (candidate.isExisting ? "<br>In service today" : "") +
                (isChosen ? "<br>Selected by the optimizer" : ""),
                { direction: "top" });

            marker.on("click", function () {
                if (showIsochrone.checked) { requestIsochrone(candidate); }
            });

            stationMarkers[candidate.id] = marker;
        });
    }

    function drawIsochrone(result, candidate) {
        clearIsochrone();

        var group = L.layerGroup();
        result.nodes.forEach(function (node) {
            L.circleMarker([node.latitude, node.longitude], {
                pane: "isochrone",
                radius: 3,
                stroke: false,
                fillColor: BAND_COLORS[Math.min(node.bandIndex, BAND_COLORS.length - 1)],
                fillOpacity: 0.55,
                interactive: false
            }).addTo(group);
        });

        group.addTo(map);
        isochroneLayer = group;

        var legend = ISOCHRONE_BANDS.map(function (band, i) {
            return "≤" + band + " min: " + result.bandCounts[i];
        }).join(" · ");
        comparison.innerHTML = "<strong>Isochrone from " + escapeHtml(candidate.label) + ":</strong> " +
            legend + " · beyond " + ISOCHRONE_BANDS[ISOCHRONE_BANDS.length - 1] + " min: " +
            result.bandCounts[ISOCHRONE_BANDS.length] + " · unreachable: " + result.unreachableNodes;
        comparison.classList.remove("d-none");
    }

    function clearIsochrone() {
        if (isochroneLayer) { map.removeLayer(isochroneLayer); isochroneLayer = null; }
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

        stationList.innerHTML = result.chosenCandidateIds.map(function (id) {
            var candidate = candidateById[id];
            if (!candidate) { return ""; }
            return '<div class="geo-cell-item">' +
                '<div class="d-flex align-items-center justify-content-between">' +
                '<span>' + escapeHtml(candidate.label) + '</span>' +
                '<strong>' + Math.round(loadById[id] || 0) + ' calls</strong></div>' +
                '<div class="geo-cell-meta">' +
                (candidate.isExisting ? "in service today · " : "new build · ") +
                'worst response ' + (worstById[id] || 0).toFixed(2) + ' min</div></div>';
        }).join("");
    }

    // ── Response-time histogram ─────────────────────────────────────────────

    function drawHistogram(result) {
        histogramSvg.innerHTML = "";

        var left = 11, right = 97, top = 5, bottom = 48;

        appendLine(histogramSvg, left, top, left, bottom, "var(--text-muted)", 0.3);
        appendLine(histogramSvg, left, bottom, right, bottom, "var(--text-muted)", 0.3);
        appendText(histogramSvg, (left + right) / 2, 58, "Response Time (minutes)", 2.6, "middle", "var(--text-muted)");
        appendText(histogramSvg, 3.0, (top + bottom) / 2, "Calls / year", 2.6, "middle", "var(--text-muted)", -90);

        if (!result || !result.assignments || result.assignments.length === 0) {
            appendText(histogramSvg, (left + right) / 2, (top + bottom) / 2,
                "Run an optimization to build the distribution", 3, "middle", "var(--text-muted)");
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

        function xFor(minute) {
            return left + (minute / binCount) * (right - left);
        }

        function yFor(weight) {
            return bottom - (weight / maxWeight) * (bottom - top);
        }

        // Y gridlines at quarter steps.
        var q;
        for (q = 1; q <= 4; q++) {
            var gy = yFor(maxWeight * q / 4);
            appendLine(histogramSvg, left, gy, right, gy, "var(--border)", 0.18);
            appendText(histogramSvg, left - 1.2, gy + 0.9, formatCount(maxWeight * q / 4), 2.2, "end", "var(--text-muted)");
        }

        // Optimized bars.
        var barWidth = (right - left) / binCount;
        for (i = 0; i < binCount; i++) {
            if (optimizedBins[i] <= 0) { continue; }
            var rect = document.createElementNS(SVGNS, "rect");
            rect.setAttribute("x", (xFor(i) + barWidth * 0.12).toFixed(2));
            rect.setAttribute("y", yFor(optimizedBins[i]).toFixed(2));
            rect.setAttribute("width", (barWidth * 0.76).toFixed(2));
            rect.setAttribute("height", Math.max(bottom - yFor(optimizedBins[i]), 0).toFixed(2));
            rect.setAttribute("fill", "var(--primary-light)");
            rect.setAttribute("fill-opacity", "0.85");
            histogramSvg.appendChild(rect);
        }

        // Baseline as a translucent step outline so the improvement reads in one glance.
        if (result.baselineAssignments && result.baselineAssignments.length > 0) {
            var points = [];
            for (i = 0; i < binCount; i++) {
                points.push(xFor(i).toFixed(2) + "," + yFor(baselineBins[i]).toFixed(2));
                points.push(xFor(i + 1).toFixed(2) + "," + yFor(baselineBins[i]).toFixed(2));
            }
            var outline = document.createElementNS(SVGNS, "polyline");
            outline.setAttribute("points", points.join(" "));
            outline.setAttribute("fill", "none");
            outline.setAttribute("stroke", "var(--text-muted)");
            outline.setAttribute("stroke-width", "0.4");
            outline.setAttribute("stroke-dasharray", "1.4 1");
            histogramSvg.appendChild(outline);
            appendText(histogramSvg, right, top + 2, "outline = today", 2.2, "end", "var(--text-muted)");
        }

        // X ticks every two minutes.
        for (i = 0; i <= binCount; i += 2) {
            var tx = xFor(i);
            appendLine(histogramSvg, tx, bottom, tx, bottom + 1.1, "var(--text-muted)", 0.25);
            appendText(histogramSvg, tx, bottom + 4.2, String(i), 2.3, "middle", "var(--text-muted)");
        }

        // NFPA threshold markers.
        markMinute(parseFloat(firstInput.value), "first-due", "#2f9e44", left, right, top, bottom, xFor);
        markMinute(parseFloat(secondInput.value), "ALS", "#f59f00", left, right, top, bottom, xFor);

        // Shade everything past the optimized p90 — the tail the standard is written about.
        var p90 = result.optimized.p90Minutes;
        if (p90 > 0 && p90 < binCount) {
            var shade = document.createElementNS(SVGNS, "rect");
            shade.setAttribute("x", xFor(p90).toFixed(2));
            shade.setAttribute("y", String(top));
            shade.setAttribute("width", Math.max(right - xFor(p90), 0).toFixed(2));
            shade.setAttribute("height", (bottom - top).toFixed(2));
            shade.setAttribute("fill", "#c92a2a");
            shade.setAttribute("fill-opacity", "0.10");
            histogramSvg.appendChild(shade);
            appendText(histogramSvg, Math.min(xFor(p90) + 1, right - 8), top + 5.5,
                "p90 " + p90.toFixed(2), 2.2, "start", "#c92a2a");
        }
    }

    function markMinute(minute, label, color, left, right, top, bottom, xFor) {
        if (!isFinite(minute) || minute <= 0) { return; }
        var x = xFor(minute);
        if (x < left || x > right) { return; }
        var line = appendLine(histogramSvg, x, top, x, bottom, color, 0.35);
        line.setAttribute("stroke-dasharray", "1.2 1");
        appendText(histogramSvg, x, top - 0.6, label, 2.2, "middle", color);
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

    function responseColor(minutes) {
        var t = Math.max(0, Math.min(1, minutes / RAMP_MAX_MINUTES));
        if (t < 0.5) {
            return mixHex("#2f9e44", "#f59f00", t / 0.5);
        }
        return mixHex("#f59f00", "#c92a2a", (t - 0.5) / 0.5);
    }

    function mixHex(from, to, t) {
        var f = parseInt(from.slice(1), 16);
        var g = parseInt(to.slice(1), 16);
        var r = Math.round(((f >> 16) & 255) + (((g >> 16) & 255) - ((f >> 16) & 255)) * t);
        var gr = Math.round(((f >> 8) & 255) + (((g >> 8) & 255) - ((f >> 8) & 255)) * t);
        var b = Math.round((f & 255) + ((g & 255) - (f & 255)) * t);
        return "rgb(" + r + "," + gr + "," + b + ")";
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
        if (value >= 1000) { return (value / 1000).toFixed(1) + "k"; }
        return String(Math.round(value));
    }

    function appendLine(svg, x1, y1, x2, y2, stroke, width) {
        var line = document.createElementNS(SVGNS, "line");
        line.setAttribute("x1", x1.toFixed(2));
        line.setAttribute("y1", y1.toFixed(2));
        line.setAttribute("x2", x2.toFixed(2));
        line.setAttribute("y2", y2.toFixed(2));
        line.setAttribute("stroke", stroke);
        line.setAttribute("stroke-width", String(width));
        svg.appendChild(line);
        return line;
    }

    function appendText(svg, x, y, content, size, anchor, fill, rotate) {
        var text = document.createElementNS(SVGNS, "text");
        text.setAttribute("x", x.toFixed(2));
        text.setAttribute("y", y.toFixed(2));
        text.setAttribute("font-size", String(size));
        text.setAttribute("text-anchor", anchor);
        text.setAttribute("fill", fill);
        if (rotate) {
            text.setAttribute("transform", "rotate(" + rotate + " " + x.toFixed(2) + " " + y.toFixed(2) + ")");
        }
        text.textContent = content;
        svg.appendChild(text);
        return text;
    }

    function setBusy(busy) {
        optimizeBtn.disabled = busy;
        isochroneBtn.disabled = busy;
        resetBtn.disabled = busy;
    }

    function showAlert(message) {
        alertBox.textContent = message;
        alertBox.classList.remove("d-none");
    }

    function hideAlert() {
        alertBox.classList.add("d-none");
        alertBox.textContent = "";
    }

    function escapeHtml(text) {
        var div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }
}());
