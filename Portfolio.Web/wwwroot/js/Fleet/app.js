// Fleet Route Optimizer — CVRPTW over the Redlands street network.
// Self-contained IIFE, no ES module syntax.
// Depends on the deferred shared modules: PortfolioUi, PortfolioFormat, PortfolioCharts, PortfolioDom,
// PortfolioMap (leaflet-setup.js, loaded after leaflet.js).
(function () {
    "use strict";

    var charts = window.PortfolioCharts;
    var fmt = window.PortfolioFormat;
    var ui = window.PortfolioUi;
    var el = window.PortfolioDom.el;

    // One colour per vehicle; routes, markers and schedule bars all key off this. The theme's
    // eight categorical series (--chart-1..8) are reused for vehicles 9 and 10, which the map
    // tells apart with a dashed route line.
    var VEHICLE_SERIES = 8;

    // Colours for map symbols that sit over the always-light OpenStreetMap raster tiles in
    // both themes (the depot and unserved stops), so they are fixed rather than tokens.
    var MAP_UNSERVED = "#94a3b8";
    var MAP_DEPOT = "#111827";

    // Cache DOM nodes up front.
    var alertBox = ui.inlineAlert(document.getElementById("fleetAlert"));
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
    var resultsPanel = convergenceSvg.closest(".card");
    var kpiVehicles = document.getElementById("fleetKpiVehicles");
    var kpiDistance = document.getElementById("fleetKpiDistance");
    var kpiLongest = document.getElementById("fleetKpiLongest");
    var kpiImprovement = document.getElementById("fleetKpiImprovement");

    // Module state.
    var scenario = null;
    var lastResult = null;
    var pending = false;
    // A preset change that arrives while a request is pending can't start a load (runExclusive refuses
    // while pending), so it is queued and replayed when the request finishes. presetRequested is the
    // preset behind the scenario on screen (or in flight); the replay is skipped if the select is back on it.
    var reloadQueued = false;
    var presetRequested = presetSelect.value;

    // PortfolioMap supplies the OSM tile layer, attribution and canvas rendering
    // (preferCanvas keeps a few thousand polyline vertices smooth).
    var map = window.PortfolioMap.create("fleetMap", { label: "Fleet route map" });

    var overlay = L.layerGroup().addTo(map);

    optimizeBtn.addEventListener("click", optimize);
    resetBtn.addEventListener("click", loadScenario);
    presetSelect.addEventListener("change", function () {
        if (pending) { reloadQueued = true; return; }
        loadScenario();
    });
    iterationsInput.addEventListener("input", function () {
        iterationsValue.textContent = iterationsInput.value;
    });
    // Canvas route and marker colours are resolved tokens, so repaint the map when the theme flips.
    charts.onThemeChange(function () {
        if (scenario) { drawMap(lastResult, true); }
    });

    loadScenario(true);

    // ── Data ────────────────────────────────────────────────────────────────

    // Runs `opts` through PortfolioUi.runAction on `trigger`, holding the other controls busy.
    function runExclusive(trigger, opts) {
        if (pending) { return Promise.resolve(); }
        pending = true;
        var others = [optimizeBtn, resetBtn, presetSelect].filter(function (c) { return c !== trigger; });
        var release = ui.busy({ buttons: others });
        opts.trigger = trigger;
        opts.alert = alertBox;
        opts.region = resultsPanel;
        return ui.runAction(opts).then(function (r) {
            release();
            pending = false;
            if (reloadQueued) {
                reloadQueued = false;
                if (presetSelect.value !== presetRequested) { loadScenario(); }
            }
            return r;
        });
    }

    // `onLoad === true` is the page-load call: it renders but does not announce. Reset Scenario passes the
    // click event, which is not `true`, so a visitor's own reset or preset change is still announced.
    function loadScenario(onLoad) {
        runExclusive(resetBtn, {
            request: function () {
                presetRequested = presetSelect.value;
                return apiGet(window.PortfolioApi.routes.spatialCompute.fleet.scenario + "?preset=" +
                    encodeURIComponent(presetRequested));
            },
            render: function (result) {
                scenario = result;
                lastResult = null;
                presetHint.textContent = scenario.description;
                vehiclesInput.value = scenario.vehicleCount;
                capacityInput.value = scenario.vehicleCapacity;
                fixedCostInput.value = scenario.vehicleFixedCost;

                resetKpis();
                routeList.replaceChildren();
                drawMap(null);
                drawConvergence(null);
                drawSchedule(null);

                summary.textContent = JSON.stringify({
                    scenario: scenario.name,
                    stops: scenario.stops.length,
                    totalDemand: totalDemand(),
                    shift: fmt.clock(scenario.shiftStartMinutes) + "–" + fmt.clock(scenario.shiftEndMinutes)
                }, null, 2);
            },
            summary: onLoad === true ? undefined : function (result) {
                return "Scenario loaded: " + result.name + ", " + result.stops.length + " stops.";
            }
        });
    }

    function optimize() {
        if (!scenario) {
            alertBox.show("The scenario has not loaded yet.");
            return;
        }

        runExclusive(optimizeBtn, {
            request: function () {
                return apiPost(window.PortfolioApi.routes.spatialCompute.fleet.optimize, {
                    depotLatitude: scenario.depotLatitude,
                    depotLongitude: scenario.depotLongitude,
                    stops: scenario.stops,
                    vehicleCount: parseInt(vehiclesInput.value, 10),
                    vehicleCapacity: parseFloat(capacityInput.value),
                    shiftStartMinutes: scenario.shiftStartMinutes,
                    shiftEndMinutes: scenario.shiftEndMinutes,
                    vehicleFixedCost: parseFloat(fixedCostInput.value),
                    maxIterations: parseInt(iterationsInput.value, 10)
                });
            },
            render: function (result) {
                lastResult = result;
                render(result);
            },
            summary: function (result) {
                var routeCount = result.routes.length;
                return routeCount + (routeCount === 1 ? " route planned" : " routes planned") +
                    (result.unservedStopIds.length > 0
                        ? ", " + result.unservedStopIds.length + " stops unserved"
                        : ", all stops served") +
                    ". Total distance " + fmt.unit(result.totalDistanceKm, "km") + ".";
            }
        });
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
        kpiDistance.textContent = fmt.unit(result.totalDistanceKm, "km");
        kpiLongest.textContent = fmt.unit(longest, "km");
        kpiImprovement.textContent = fmt.percent(result.improvementPercent);

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
            var swatch = el("span", { class: "legend__swatch", "aria-hidden": "true" });
            swatch.style.setProperty("--swatch", seriesVar(route.vehicleIndex));
            return el("div", { class: route.feasible ? "geo-cell-item" : "geo-cell-item geo-cell-anomaly" },
                el("div", { class: "d-flex align-items-center justify-content-between" },
                    el("span", { class: "d-inline-flex align-items-center gap-2" },
                        swatch, "Vehicle " + (route.vehicleIndex + 1)),
                    el("strong", { text: fmt.unit(route.distanceKm, "km") })),
                el("div", { class: "geo-cell-meta",
                    text: route.stopIds.length + " stops · " + Math.round(route.load) + "/" + Math.round(capacity) +
                        " kg · back " + fmt.clock(route.returnMinutes) }));
        });

        if (result.unservedStopIds.length > 0) {
            rows.push(el("div", { class: "geo-cell-item geo-cell-anomaly" },
                el("div", { class: "d-flex align-items-center justify-content-between" },
                    el("span", { text: "Unserved" }),
                    el("strong", { text: String(result.unservedStopIds.length) })),
                el("div", { class: "geo-cell-meta",
                    text: "Stops " + result.unservedStopIds.join(", ") +
                        " could not be served within capacity and their time windows." })));
        }

        routeList.replaceChildren.apply(routeList, rows);
    }

    // ── Map ─────────────────────────────────────────────────────────────────

    // `keepView` repaints without re-fitting the map (used when only the theme changed).
    function drawMap(result, keepView) {
        overlay.clearLayers();
        if (!scenario) {
            return;
        }

        var bounds = [];
        var series = charts.palette(VEHICLE_SERIES);
        var onDark = charts.token("on-dark");

        // Routes first so the markers sit on top of the polylines.
        if (result) {
            result.routes.forEach(function (route) {
                if (!route.path || route.path.length < 2) { return; }
                var latlngs = route.path.map(function (c) { return [c.y, c.x]; });
                L.polyline(latlngs, {
                    color: series[route.vehicleIndex % VEHICLE_SERIES],
                    weight: 4,
                    opacity: 0.85,
                    lineJoin: "round",
                    lineCap: "round",
                    dashArray: route.vehicleIndex >= VEHICLE_SERIES ? "10 6" : null
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
                fillColor: served ? series[assigned % VEHICLE_SERIES] : MAP_UNSERVED,
                color: onDark,
                weight: served ? 1.6 : 1,
                fillOpacity: served ? 0.95 : 0.5,
                dashArray: served ? null : "2 2"
            }).addTo(overlay);

            var tip = el("span", {}, el("strong", { text: stop.label }), document.createElement("br"),
                Math.round(stop.demand) + " kg · window " + fmt.clock(stop.readyMinutes) + "–" + fmt.clock(stop.dueMinutes));
            if (served) {
                tip.append(document.createElement("br"), "Vehicle " + (assigned + 1) + ", stop #" + sequence[stop.id] +
                    " · ETA " + fmt.clock(arrival[stop.id]));
            } else {
                tip.append(document.createElement("br"), el("em", { text: "Unserved" }));
            }
            marker.bindTooltip(tip, { direction: "top" });

            bounds.push([stop.latitude, stop.longitude]);
        });

        var depot = L.circleMarker([scenario.depotLatitude, scenario.depotLongitude], {
            radius: 10,
            fillColor: MAP_DEPOT,
            color: onDark,
            weight: 2.5,
            fillOpacity: 0.95
        }).addTo(overlay);
        depot.bindTooltip(el("strong", { text: scenario.depotLabel }), { direction: "top" });
        bounds.push([scenario.depotLatitude, scenario.depotLongitude]);

        if (bounds.length > 0 && !keepView) {
            map.fitBounds(bounds, { padding: [30, 30] });
        }
    }

    // ── Convergence chart ───────────────────────────────────────────────────

    function drawConvergence(result) {
        charts.svg.clear(convergenceSvg);
        var svg = charts.svg;

        var left = 13, right = 97, top = 5, bottom = 36;

        svg.line(convergenceSvg, left, top, left, bottom, { width: 0.3 });
        svg.line(convergenceSvg, left, bottom, right, bottom, { width: 0.3 });
        svg.text(convergenceSvg, (left + right) / 2, 44, "Local-search pass", { size: 2.6, anchor: "middle" });
        svg.text(convergenceSvg, 3.2, (top + bottom) / 2, "Objective", { size: 2.6, anchor: "middle", rotate: -90 });

        if (!result || !result.iterationCosts || result.iterationCosts.length < 2) {
            svg.text(convergenceSvg, (left + right) / 2, (top + bottom) / 2,
                "Run the optimizer to trace convergence", { size: 3, anchor: "middle" });
            charts.describe(convergenceSvg, "Objective convergence chart. The optimizer has not been run yet.");
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

        var xAt = charts.scale.linear([0, Math.max(costs.length - 1, 1)], [left, right]);
        var yFor = charts.scale.linear([lo, hi], [bottom, top]);

        var q;
        for (q = 0; q <= 3; q++) {
            var value = lo + (hi - lo) * q / 3;
            var gy = yFor(value);
            svg.line(convergenceSvg, left, gy, right, gy, { stroke: "var(--border)", width: 0.18 });
            svg.text(convergenceSvg, left - 1.2, gy + 0.9, fmt.number(value), { size: 2.2, anchor: "end" });
        }

        var points = costs.map(function (cost, index) {
            return xAt(index).toFixed(2) + "," + yFor(cost).toFixed(2);
        }).join(" ");

        convergenceSvg.appendChild(svg.el("polyline", {
            points: points,
            fill: "none",
            stroke: "var(--chart-series)",
            "stroke-width": "0.7",
            "stroke-linejoin": "round"
        }));

        markPoint(xAt(0), yFor(costs[0]), "Clarke-Wright " + fmt.number(costs[0]), "start");
        markPoint(xAt(costs.length - 1), yFor(costs[costs.length - 1]),
            "Final " + fmt.number(costs[costs.length - 1]), "end");

        svg.text(convergenceSvg, (left + right) / 2, top + 2,
            fmt.percent(result.improvementPercent) + " lower after " + (costs.length - 1) + " passes",
            { size: 2.6, anchor: "middle", fill: "var(--accent-text)" });

        charts.describe(convergenceSvg, "Objective convergence chart: " + fmt.number(costs[0]) + " after construction, " +
            fmt.number(costs[costs.length - 1]) + " after " + (costs.length - 1) + " local-search passes, " +
            fmt.percent(result.improvementPercent) + " lower.");

        function markPoint(cx, cy, label, anchor) {
            convergenceSvg.appendChild(svg.el("circle", {
                cx: cx.toFixed(2), cy: cy.toFixed(2), r: "0.9", fill: "var(--status-danger-fill)"
            }));
            svg.text(convergenceSvg, cx, cy - 2, label, { size: 2.3, anchor: anchor, fill: "var(--text)" });
        }
    }

    // ── Schedule timeline ───────────────────────────────────────────────────

    function drawSchedule(result) {
        charts.svg.clear(scheduleSvg);
        var svg = charts.svg;

        var left = 12, right = 98, top = 6;

        if (!result || result.routes.length === 0 || !scenario) {
            svg.text(scheduleSvg, (left + right) / 2, 28,
                "Run the optimizer to build the dispatch schedule", { size: 3, anchor: "middle" });
            charts.describe(scheduleSvg, "Vehicle schedule timeline. The optimizer has not been run yet.");
            return;
        }

        var shiftStart = scenario.shiftStartMinutes;
        var shiftEnd = scenario.shiftEndMinutes;
        var rowHeight = Math.min(8, (54 - top) / result.routes.length);
        var barHeight = rowHeight * 0.55;

        var xFor = charts.scale.linear([shiftStart, shiftEnd], [left, right]);

        // Hour gridlines across the shift.
        var hour;
        for (hour = Math.ceil(shiftStart / 60) * 60; hour <= shiftEnd; hour += 60) {
            var gx = xFor(hour);
            svg.line(scheduleSvg, gx, top - 2, gx, top + rowHeight * result.routes.length,
                { stroke: "var(--border)", width: 0.18 });
            svg.text(scheduleSvg, gx, top - 3, fmt.clock(hour), { size: 2.2, anchor: "middle" });
        }

        var byId = {};
        scenario.stops.forEach(function (s) { byId[s.id] = s; });

        result.routes.forEach(function (route, rowIndex) {
            var y = top + rowIndex * rowHeight;
            var color = seriesVar(route.vehicleIndex);

            svg.text(scheduleSvg, left - 1.5, y + barHeight * 0.85, "V" + (route.vehicleIndex + 1),
                { size: 2.4, anchor: "end" });

            svg.line(scheduleSvg, left, y + barHeight / 2, right, y + barHeight / 2,
                { stroke: "var(--border)", width: 0.15 });

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

        svg.text(scheduleSvg, (left + right) / 2, top + rowHeight * result.routes.length + 5,
            "Solid blocks are arrival and service; the pale band behind each is the customer's window.",
            { size: 2.3, anchor: "middle" });

        charts.describe(scheduleSvg, "Vehicle schedule timeline for " + result.routes.length + " vehicles across the " +
            fmt.clock(shiftStart) + " to " + fmt.clock(shiftEnd) + " shift. Each block is a stop's arrival and service " +
            "time inside its customer window.");

        function titleFor(stop, arrive, vehicleIndex) {
            var title = svg.el("title");
            title.textContent = stop.label + " — vehicle " + (vehicleIndex + 1) +
                ", ETA " + fmt.clock(arrive) +
                ", window " + fmt.clock(stop.readyMinutes) + "–" + fmt.clock(stop.dueMinutes);
            return title;
        }
    }

    function appendRect(x, y, width, height, fill, opacity) {
        var rect = charts.svg.el("rect", {
            x: x.toFixed(2), y: y.toFixed(2), width: width.toFixed(2), height: height.toFixed(2),
            rx: "0.3", fill: fill, "fill-opacity": String(opacity)
        });
        scheduleSvg.appendChild(rect);
        return rect;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // `var(--chart-N)` for SVG fills and CSS swatches (follows the theme without a redraw).
    function seriesVar(index) {
        return "var(--chart-" + ((index % VEHICLE_SERIES) + 1) + ")";
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
}());
