// Fleet Route Optimizer — CVRPTW over the Redlands street network.
// Self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var SVGNS = "http://www.w3.org/2000/svg";

    // One colour per vehicle; routes, markers and schedule bars all key off this.
    var VEHICLE_COLORS = [
        "#2563eb", "#e8590c", "#2f9e44", "#ae3ec9",
        "#0d9488", "#c2255c", "#f59f00", "#4263eb",
        "#7048e8", "#1098ad"
    ];

    // Cache DOM nodes up front.
    var alertBox = document.getElementById("fleetAlert");
    var presetSelect = document.getElementById("fleetPreset");
    var presetHint = document.getElementById("fleetPresetHint");
    var vehiclesInput = document.getElementById("fleetVehicles");
    var capacityInput = document.getElementById("fleetCapacity");
    var fixedCostInput = document.getElementById("fleetFixedCost");
    var iterationsInput = document.getElementById("fleetIterations");
    var iterationsValue = document.getElementById("fleetIterationsValue");
    var optimizeBtn = document.getElementById("fleetOptimizeBtn");
    var resetBtn = document.getElementById("fleetResetBtn");
    var routeList = document.getElementById("fleetRouteList");
    var summary = document.getElementById("fleetSummary");
    var convergenceSvg = document.getElementById("fleetConvergenceSvg");
    var scheduleSvg = document.getElementById("fleetScheduleSvg");
    var kpiVehicles = document.getElementById("fleetKpiVehicles");
    var kpiDistance = document.getElementById("fleetKpiDistance");
    var kpiLongest = document.getElementById("fleetKpiLongest");
    var kpiImprovement = document.getElementById("fleetKpiImprovement");

    // Module state.
    var scenario = null;
    var lastResult = null;

    // preferCanvas keeps a few thousand polyline vertices smooth.
    var map = L.map("fleetMap", {
        center: [34.0565, -117.1850],
        zoom: 13,
        zoomControl: true,
        preferCanvas: true
    });

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

    var overlay = L.layerGroup().addTo(map);

    optimizeBtn.addEventListener("click", optimize);
    resetBtn.addEventListener("click", loadScenario);
    presetSelect.addEventListener("change", loadScenario);
    iterationsInput.addEventListener("input", function () {
        iterationsValue.textContent = iterationsInput.value;
    });

    loadScenario();

    // ── Data ────────────────────────────────────────────────────────────────

    function loadScenario() {
        hideAlert();
        setBusy(true);

        apiGet(window.PortfolioApi.routes.spatialCompute.fleet.scenario + "?preset=" +
            encodeURIComponent(presetSelect.value))
            .then(function (result) {
                scenario = result;
                lastResult = null;
                presetHint.textContent = scenario.description;
                vehiclesInput.value = scenario.vehicleCount;
                capacityInput.value = scenario.vehicleCapacity;
                fixedCostInput.value = scenario.vehicleFixedCost;

                resetKpis();
                routeList.innerHTML = "";
                drawMap(null);
                drawConvergence(null);
                drawSchedule(null);

                summary.textContent = JSON.stringify({
                    scenario: scenario.name,
                    stops: scenario.stops.length,
                    totalDemand: totalDemand(),
                    shift: formatClock(scenario.shiftStartMinutes) + "–" + formatClock(scenario.shiftEndMinutes)
                }, null, 2);
            })
            .catch(function (err) { showAlert(err.message || "Failed to load the scenario."); })
            .finally(function () { setBusy(false); });
    }

    function optimize() {
        if (!scenario) {
            showAlert("The scenario has not loaded yet.");
            return;
        }

        hideAlert();
        setBusy(true);

        apiPost(window.PortfolioApi.routes.spatialCompute.fleet.optimize, {
            depotLatitude: scenario.depotLatitude,
            depotLongitude: scenario.depotLongitude,
            stops: scenario.stops,
            vehicleCount: parseInt(vehiclesInput.value, 10),
            vehicleCapacity: parseFloat(capacityInput.value),
            shiftStartMinutes: scenario.shiftStartMinutes,
            shiftEndMinutes: scenario.shiftEndMinutes,
            vehicleFixedCost: parseFloat(fixedCostInput.value),
            maxIterations: parseInt(iterationsInput.value, 10)
        })
            .then(function (result) {
                lastResult = result;
                render(result);
            })
            .catch(function (err) { showAlert(err.message || "Optimization failed."); })
            .finally(function () { setBusy(false); });
    }

    function totalDemand() {
        var total = 0;
        scenario.stops.forEach(function (s) { total += s.demand; });
        return total;
    }

    function render(result) {
        var longest = 0;
        result.routes.forEach(function (r) {
            if (r.distanceKm > longest) { longest = r.distanceKm; }
        });

        kpiVehicles.textContent = result.vehiclesUsed + " / " + vehiclesInput.value;
        kpiDistance.textContent = result.totalDistanceKm.toFixed(1) + " km";
        kpiLongest.textContent = longest.toFixed(1) + " km";
        kpiImprovement.textContent = result.improvementPercent.toFixed(1) + "%";

        buildRouteList(result);
        drawMap(result);
        drawConvergence(result);
        drawSchedule(result);

        summary.textContent = JSON.stringify({
            nativeAccelerated: result.nativeAccelerated,
            feasible: result.feasible,
            vehiclesUsed: result.vehiclesUsed,
            totalDistanceKm: round(result.totalDistanceKm, 2),
            unservedStops: result.unservedStopIds.length,
            initialObjective: round(result.initialObjective, 2),
            finalObjective: round(result.finalObjective, 2),
            improvementPercent: round(result.improvementPercent, 2),
            localSearchPasses: result.iterationCosts.length - 1,
            matrixBuildMs: round(result.matrixBuildMs, 1),
            solveMs: round(result.solveMs, 1),
            pathExpandMs: round(result.pathExpandMs, 1)
        }, null, 2);
    }

    // ── Route summary list ──────────────────────────────────────────────────

    function buildRouteList(result) {
        var capacity = parseFloat(capacityInput.value);
        var rows = result.routes.map(function (route) {
            var color = colorFor(route.vehicleIndex);
            var cls = route.feasible ? "geo-cell-item" : "geo-cell-item geo-cell-anomaly";
            return '<div class="' + cls + '">' +
                '<div class="d-flex align-items-center justify-content-between">' +
                '<span class="d-inline-flex align-items-center gap-2">' +
                '<span style="width:0.7rem;height:0.7rem;border-radius:2px;background:' + color +
                ';display:inline-block;"></span>Vehicle ' + (route.vehicleIndex + 1) + '</span>' +
                '<strong>' + route.distanceKm.toFixed(1) + ' km</strong></div>' +
                '<div class="geo-cell-meta">' + route.stopIds.length + ' stops · ' +
                Math.round(route.load) + '/' + Math.round(capacity) + ' kg · back ' +
                formatClock(route.returnMinutes) + '</div></div>';
        });

        if (result.unservedStopIds.length > 0) {
            rows.push('<div class="geo-cell-item geo-cell-anomaly">' +
                '<div class="d-flex align-items-center justify-content-between">' +
                '<span>Unserved</span><strong>' + result.unservedStopIds.length + '</strong></div>' +
                '<div class="geo-cell-meta">Stops ' + escapeHtml(result.unservedStopIds.join(", ")) +
                ' could not be served within capacity and their time windows.</div></div>');
        }

        routeList.innerHTML = rows.join("");
    }

    // ── Map ─────────────────────────────────────────────────────────────────

    function drawMap(result) {
        overlay.clearLayers();
        if (!scenario) {
            return;
        }

        var bounds = [];

        // Routes first so the markers sit on top of the polylines.
        if (result) {
            result.routes.forEach(function (route) {
                if (!route.path || route.path.length < 2) { return; }
                var latlngs = route.path.map(function (c) { return [c.y, c.x]; });
                L.polyline(latlngs, {
                    color: colorFor(route.vehicleIndex),
                    weight: 4,
                    opacity: 0.85,
                    lineJoin: "round",
                    lineCap: "round"
                }).addTo(overlay);
            });
        }

        var assignment = {};
        var sequence = {};
        var arrival = {};
        if (result) {
            result.routes.forEach(function (route) {
                route.stopIds.forEach(function (id, index) {
                    assignment[id] = route.vehicleIndex;
                    sequence[id] = index + 1;
                    arrival[id] = route.arrivalMinutes[index];
                });
            });
        }

        scenario.stops.forEach(function (stop) {
            var assigned = assignment[stop.id];
            var served = assigned !== undefined;
            var marker = L.circleMarker([stop.latitude, stop.longitude], {
                radius: served ? 7 : 5,
                fillColor: served ? colorFor(assigned) : "#94a3b8",
                color: "#fff",
                weight: served ? 1.6 : 1,
                fillOpacity: served ? 0.95 : 0.5,
                dashArray: served ? null : "2 2"
            }).addTo(overlay);

            var tip = "<strong>" + escapeHtml(stop.label) + "</strong><br/>" +
                Math.round(stop.demand) + " kg · window " +
                formatClock(stop.readyMinutes) + "–" + formatClock(stop.dueMinutes);
            if (served) {
                tip += "<br/>Vehicle " + (assigned + 1) + ", stop #" + sequence[stop.id] +
                    " · ETA " + formatClock(arrival[stop.id]);
            } else {
                tip += "<br/><em>Unserved</em>";
            }
            marker.bindTooltip(tip, { direction: "top" });

            bounds.push([stop.latitude, stop.longitude]);
        });

        var depot = L.circleMarker([scenario.depotLatitude, scenario.depotLongitude], {
            radius: 10,
            fillColor: "#111827",
            color: "#fff",
            weight: 2.5,
            fillOpacity: 0.95
        }).addTo(overlay);
        depot.bindTooltip("<strong>" + escapeHtml(scenario.depotLabel) + "</strong>", { direction: "top" });
        bounds.push([scenario.depotLatitude, scenario.depotLongitude]);

        if (bounds.length > 0) {
            map.fitBounds(bounds, { padding: [30, 30] });
        }
    }

    // ── Convergence chart ───────────────────────────────────────────────────

    function drawConvergence(result) {
        convergenceSvg.innerHTML = "";

        var left = 13, right = 97, top = 5, bottom = 36;

        appendLine(convergenceSvg, left, top, left, bottom, "var(--text-muted)", 0.3);
        appendLine(convergenceSvg, left, bottom, right, bottom, "var(--text-muted)", 0.3);
        appendText(convergenceSvg, (left + right) / 2, 44, "Local-search pass", 2.6, "middle", "var(--text-muted)");
        appendText(convergenceSvg, 3.2, (top + bottom) / 2, "Objective", 2.6, "middle", "var(--text-muted)", -90);

        if (!result || !result.iterationCosts || result.iterationCosts.length < 2) {
            appendText(convergenceSvg, (left + right) / 2, (top + bottom) / 2,
                "Run the optimizer to trace convergence", 3, "middle", "var(--text-muted)");
            return;
        }

        var costs = result.iterationCosts;
        var maxCost = costs[0];
        var minCost = costs[costs.length - 1];
        for (var i = 0; i < costs.length; i++) {
            if (costs[i] > maxCost) { maxCost = costs[i]; }
            if (costs[i] < minCost) { minCost = costs[i]; }
        }

        // Pad the value axis so a nearly flat trace is still readable.
        var span = maxCost - minCost;
        if (span < 1e-6) { span = Math.max(maxCost * 0.02, 1); }
        var lo = minCost - span * 0.25;
        var hi = maxCost + span * 0.15;

        function xFor(index) {
            return left + (costs.length === 1 ? 0 : index / (costs.length - 1)) * (right - left);
        }

        function yFor(cost) {
            return bottom - (cost - lo) / (hi - lo) * (bottom - top);
        }

        var q;
        for (q = 0; q <= 3; q++) {
            var value = lo + (hi - lo) * q / 3;
            var gy = yFor(value);
            appendLine(convergenceSvg, left, gy, right, gy, "var(--border)", 0.18);
            appendText(convergenceSvg, left - 1.2, gy + 0.9, value.toFixed(0), 2.2, "end", "var(--text-muted)");
        }

        var points = costs.map(function (cost, index) {
            return xFor(index).toFixed(2) + "," + yFor(cost).toFixed(2);
        }).join(" ");

        var polyline = document.createElementNS(SVGNS, "polyline");
        polyline.setAttribute("points", points);
        polyline.setAttribute("fill", "none");
        polyline.setAttribute("stroke", "var(--primary-light)");
        polyline.setAttribute("stroke-width", "0.7");
        polyline.setAttribute("stroke-linejoin", "round");
        convergenceSvg.appendChild(polyline);

        markPoint(xFor(0), yFor(costs[0]), "Clarke-Wright " + costs[0].toFixed(0), "start");
        markPoint(xFor(costs.length - 1), yFor(costs[costs.length - 1]),
            "Final " + costs[costs.length - 1].toFixed(0), "end");

        appendText(convergenceSvg, (left + right) / 2, top + 2,
            result.improvementPercent.toFixed(1) + "% lower after " + (costs.length - 1) + " passes",
            2.6, "middle", "var(--accent)");

        function markPoint(cx, cy, label, anchor) {
            var dot = document.createElementNS(SVGNS, "circle");
            dot.setAttribute("cx", cx.toFixed(2));
            dot.setAttribute("cy", cy.toFixed(2));
            dot.setAttribute("r", "0.9");
            dot.setAttribute("fill", "#e03131");
            convergenceSvg.appendChild(dot);
            appendText(convergenceSvg, cx, cy - 2, label, 2.3, anchor, "var(--text)");
        }
    }

    // ── Schedule timeline ───────────────────────────────────────────────────

    function drawSchedule(result) {
        scheduleSvg.innerHTML = "";

        var left = 12, right = 98, top = 6;

        if (!result || result.routes.length === 0 || !scenario) {
            appendText(scheduleSvg, (left + right) / 2, 28,
                "Run the optimizer to build the dispatch schedule", 3, "middle", "var(--text-muted)");
            return;
        }

        var shiftStart = scenario.shiftStartMinutes;
        var shiftEnd = scenario.shiftEndMinutes;
        var rowHeight = Math.min(8, (54 - top) / result.routes.length);
        var barHeight = rowHeight * 0.55;

        function xFor(minutes) {
            var t = (minutes - shiftStart) / (shiftEnd - shiftStart);
            return left + Math.max(0, Math.min(1, t)) * (right - left);
        }

        // Hour gridlines across the shift.
        var hour;
        for (hour = Math.ceil(shiftStart / 60) * 60; hour <= shiftEnd; hour += 60) {
            var gx = xFor(hour);
            appendLine(scheduleSvg, gx, top - 2, gx, top + rowHeight * result.routes.length,
                "var(--border)", 0.18);
            appendText(scheduleSvg, gx, top - 3, formatClock(hour), 2.2, "middle", "var(--text-muted)");
        }

        var byId = {};
        scenario.stops.forEach(function (s) { byId[s.id] = s; });

        result.routes.forEach(function (route, rowIndex) {
            var y = top + rowIndex * rowHeight;
            var color = colorFor(route.vehicleIndex);

            appendText(scheduleSvg, left - 1.5, y + barHeight * 0.85, "V" + (route.vehicleIndex + 1),
                2.4, "end", "var(--text-muted)");

            appendLine(scheduleSvg, left, y + barHeight / 2, right, y + barHeight / 2,
                "var(--border)", 0.15);

            route.stopIds.forEach(function (stopId, index) {
                var stop = byId[stopId];
                if (!stop) { return; }

                // Time window behind, actual service in front.
                appendRect(xFor(stop.readyMinutes), y,
                    Math.max(xFor(stop.dueMinutes) - xFor(stop.readyMinutes), 0.3),
                    barHeight, color, 0.16);

                var arrive = route.arrivalMinutes[index];
                appendRect(xFor(arrive), y + barHeight * 0.15,
                    Math.max(xFor(arrive + stop.serviceMinutes) - xFor(arrive), 0.5),
                    barHeight * 0.7, color, 0.95)
                    .appendChild(titleFor(stop, arrive, route.vehicleIndex));
            });
        });

        appendText(scheduleSvg, (left + right) / 2, top + rowHeight * result.routes.length + 5,
            "Solid blocks are arrival and service; the pale band behind each is the customer's window.",
            2.3, "middle", "var(--text-muted)");

        function titleFor(stop, arrive, vehicleIndex) {
            var title = document.createElementNS(SVGNS, "title");
            title.textContent = stop.label + " — vehicle " + (vehicleIndex + 1) +
                ", ETA " + formatClock(arrive) +
                ", window " + formatClock(stop.readyMinutes) + "–" + formatClock(stop.dueMinutes);
            return title;
        }
    }

    // ── SVG helpers ─────────────────────────────────────────────────────────

    function appendRect(x, y, width, height, fill, opacity) {
        var rect = document.createElementNS(SVGNS, "rect");
        rect.setAttribute("x", x.toFixed(2));
        rect.setAttribute("y", y.toFixed(2));
        rect.setAttribute("width", width.toFixed(2));
        rect.setAttribute("height", height.toFixed(2));
        rect.setAttribute("rx", "0.3");
        rect.setAttribute("fill", fill);
        rect.setAttribute("fill-opacity", String(opacity));
        scheduleSvg.appendChild(rect);
        return rect;
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

    // ── Helpers ─────────────────────────────────────────────────────────────

    function colorFor(index) {
        return VEHICLE_COLORS[index % VEHICLE_COLORS.length];
    }

    function formatClock(minutes) {
        if (!isFinite(minutes)) { return "–"; }
        var total = Math.round(minutes);
        var h = Math.floor(total / 60) % 24;
        var m = total % 60;
        return (h < 10 ? "0" : "") + h + ":" + (m < 10 ? "0" : "") + m;
    }

    function round(value, places) {
        var factor = Math.pow(10, places);
        return Math.round(value * factor) / factor;
    }

    function resetKpis() {
        kpiVehicles.textContent = "–";
        kpiDistance.textContent = "–";
        kpiLongest.textContent = "–";
        kpiImprovement.textContent = "–";
    }

    function setBusy(busy) {
        optimizeBtn.disabled = busy;
        resetBtn.disabled = busy;
        presetSelect.disabled = busy;
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
