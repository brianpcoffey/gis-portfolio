// Outage Manager & Network Trace Page — self-contained IIFE, no ES module syntax.
// Depends on the deferred shared modules: PortfolioUi, PortfolioFormat, PortfolioCharts, PortfolioDom.
(function () {
    "use strict";

    var charts = window.PortfolioCharts;
    var fmt = window.PortfolioFormat;
    var ui = window.PortfolioUi;
    var el = window.PortfolioDom.el;
    var svgEl = charts.svg.el;

    // Mirrors Portfolio.Common.DTOs.DeviceTypes. The wire format is an int, so the
    // names live in exactly two places and this is the second one.
    var DEVICE = {
        CONDUCTOR: 0,
        SWITCH: 1,
        FUSE: 2,
        RECLOSER: 3,
        BREAKER: 4,
        TIE_SWITCH: 5,
        TRANSFORMER: 6
    };

    var DEVICE_NAMES = ["Conductor", "Switch", "Fuse", "Recloser", "Breaker", "Tie switch", "Transformer"];
    // Theme tokens as `var()` strings: they are used as SVG attributes and CSS custom
    // properties, so they follow the light/dark switch without a redraw.
    var DEVICE_COLORS = [
        "var(--text-muted)", "var(--chart-4)", "var(--chart-2)", "var(--chart-5)",
        "var(--chart-8)", "var(--chart-3)", "var(--chart-1)"
    ];

    var COLOR_OUT = "var(--status-danger-fill)";
    var COLOR_UPSTREAM = "var(--status-info-fill)";
    var COLOR_ISOLATE = "var(--chart-7)";
    var COLOR_RESTORED = "var(--status-success-fill)";

    // Cache DOM nodes up front.
    var alertBox = ui.inlineAlert(document.getElementById("outageAlert"));
    var feederSelect = document.getElementById("outageFeeder");
    var faultSelect = document.getElementById("outageFault");
    var placeBtn = document.getElementById("outagePlaceBtn");
    var repairInput = document.getElementById("outageRepair");
    var restoreBtn = document.getElementById("outageRestoreBtn");
    var clearBtn = document.getElementById("outageClearBtn");
    var legendBox = document.getElementById("outageLegend");
    var planList = document.getElementById("outagePlan");
    var summary = document.getElementById("outageSummary");
    var svg = document.getElementById("outageSvg");
    var tooltip = document.getElementById("outageTooltip");
    var panel = svg.parentNode;
    var resultsPanel = svg.closest(".card");
    var kpiAffected = document.getElementById("outageKpiAffected");
    var kpiPercent = document.getElementById("outageKpiPercent");
    var kpiIsolation = document.getElementById("outageKpiIsolation");
    var kpiRestored = document.getElementById("outageKpiRestored");

    // Module state.
    var network = null;
    var projection = null;
    var elementsById = {};
    var trace = null;
    var restoration = null;
    var pending = false;

    restoreBtn.addEventListener("click", proposeRestoration);
    clearBtn.addEventListener("click", clearFault);
    placeBtn.addEventListener("click", function () {
        var id = parseInt(faultSelect.value, 10);
        if (!isNaN(id)) { placeFault(id); }
    });
    faultSelect.addEventListener("change", function () {
        placeBtn.disabled = faultSelect.value === "";
    });
    feederSelect.addEventListener("change", function () {
        populateFaultOptions();
        draw();
    });
    svg.addEventListener("mouseleave", hideTooltip);

    buildLegend();
    loadNetwork();

    // ── Data ────────────────────────────────────────────────────────────────

    // Runs `opts` through PortfolioUi.runAction on `trigger`, holding the other buttons busy too.
    // Diagram clicks are not buttons, so a pending flag stops a second request starting mid-flight.
    function runExclusive(trigger, held, opts) {
        if (pending) { return Promise.resolve(); }
        pending = true;
        var release = ui.busy({ buttons: held.filter(function (b) { return b !== trigger; }) });
        opts.trigger = trigger;
        opts.alert = alertBox;
        opts.region = resultsPanel;
        return ui.runAction(opts).then(function (r) { release(); pending = false; return r; });
    }

    function loadNetwork() {
        runExclusive(clearBtn, [placeBtn, restoreBtn], {
            request: function () { return apiGet(window.PortfolioApi.routes.spatialCompute.outage.network); },
            render: function (result) {
                network = result;
                elementsById = {};
                network.elements.forEach(function (e) { elementsById[e.id] = e; });
                projection = buildProjection(network.elements);
                populateFaultOptions();
                clearFault();
            }
            // No summary: this runs once on page load, not because the visitor asked for it.
        });
    }

    function placeFault(elementId) {
        // A diagram click while a request is pending is dropped by runExclusive; bail out before the
        // side effects below, so the select never shows a fault that was not placed.
        if (!network || pending) { return; }
        restoration = null;
        faultSelect.value = String(elementId);
        placeBtn.disabled = false;

        runExclusive(placeBtn, [clearBtn, restoreBtn], {
            request: function () {
                return apiPost(window.PortfolioApi.routes.spatialCompute.outage.trace, {
                    elements: network.elements,
                    sourceNodeId: network.sourceNodeId,
                    faultElementId: elementId
                });
            },
            render: function (result) {
                trace = result;
                restoreBtn.disabled = false;
                render();
            },
            summary: function (result) {
                return "Fault placed on " + labelOf(result.faultElementId) + ": " +
                    fmt.number(result.customersAffected) + " customers affected (" +
                    fmt.percent(result.percentAffected) + " of the system), " +
                    result.isolationDeviceIds.length + " isolation devices.";
            }
        });
    }

    function proposeRestoration() {
        if (!trace) { return; }

        runExclusive(restoreBtn, [placeBtn, clearBtn], {
            request: function () {
                return apiPost(window.PortfolioApi.routes.spatialCompute.outage.restore, {
                    elements: network.elements,
                    sourceNodeId: network.sourceNodeId,
                    faultElementId: trace.faultElementId,
                    isolationDeviceIds: trace.isolationDeviceIds,
                    assumedRepairMinutes: parseFloat(repairInput.value) || 0
                });
            },
            render: function (result) {
                restoration = result;
                render();
            },
            summary: function (result) {
                return "Restoration search complete: " + fmt.number(result.customersRestored) +
                    " customers restored, " + fmt.number(result.customersStillOut) + " still out.";
            }
        });
    }

    function clearFault() {
        trace = null;
        restoration = null;
        restoreBtn.disabled = true;
        faultSelect.value = "";
        placeBtn.disabled = true;
        render();
    }

    // Fault location list (FE-A06): the keyboard / switch-access equivalent of clicking a
    // conductor on the diagram. Options are grouped by feeder and follow the feeder filter.
    function populateFaultOptions() {
        var previous = faultSelect.value;
        var feeder = feederSelect.value;
        var groups = [];
        var byFeeder = {};

        (network ? network.elements : []).forEach(function (e) {
            if (feeder !== "" && e.feederName !== feeder && e.feederName !== "Tie") { return; }
            var group = byFeeder[e.feederName];
            if (!group) {
                group = el("optgroup", { label: e.feederName });
                byFeeder[e.feederName] = group;
                groups.push(group);
            }
            var text = e.deviceType === DEVICE.CONDUCTOR ? e.label : e.label + " (" + DEVICE_NAMES[e.deviceType] + ")";
            group.appendChild(el("option", { value: e.id, text: text }));
        });

        var placeholder = el("option", { value: "", text: "Select a conductor…" });
        faultSelect.replaceChildren.apply(faultSelect, [placeholder].concat(groups));
        faultSelect.value = previous && faultSelect.querySelector('option[value="' + previous + '"]') ? previous : "";
        placeBtn.disabled = faultSelect.value === "";
    }

    // ── Rendering ───────────────────────────────────────────────────────────

    function render() {
        draw();
        renderKpis();
        renderPlan();
        renderSummary();
    }

    function renderKpis() {
        kpiAffected.textContent = trace ? String(trace.customersAffected) : "–";
        kpiPercent.textContent = trace ? fmt.percent(trace.percentAffected) : "–";
        kpiIsolation.textContent = trace ? String(trace.isolationDeviceIds.length) : "–";
        kpiRestored.textContent = restoration ? String(restoration.customersRestored) : "–";
    }

    function renderPlan() {
        if (!restoration || restoration.plan.length === 0) {
            planList.replaceChildren(el("li", {
                class: "geo-cell-item",
                text: trace ? "Run the restoration search to build a switching plan."
                            : "Click a conductor on the diagram, or choose a fault location, to place a fault."
            }));
            return;
        }

        var items = restoration.plan.map(function (step) {
            var closing = step.action === "CLOSE";
            return el("li", { class: "geo-cell-item" },
                el("div", { class: "d-flex align-items-center justify-content-between" },
                    el("strong", { text: step.label }),
                    el("span", { class: "badge " + (closing ? "text-bg-success" : "text-bg-warning"), text: step.action })),
                el("div", { class: "geo-cell-meta", text: "element #" + step.elementId }));
        });
        planList.replaceChildren.apply(planList, items);
    }

    function renderSummary() {
        if (!network) {
            summary.textContent = "";
            return;
        }

        var payload = {
            network: network.networkName,
            elements: network.elements.length,
            totalCustomers: network.totalCustomers
        };

        if (trace) {
            payload.nativeAccelerated = trace.nativeAccelerated;
            payload.faultElement = labelOf(trace.faultElementId);
            payload.elementsDeEnergized = trace.downstreamElementIds.length;
            payload.customersAffected = trace.customersAffected;
            payload.percentAffected = trace.percentAffected;
            payload.isolationDevices = trace.isolationDeviceIds.map(labelOf);
        }

        if (restoration) {
            payload.restorationFound = restoration.restorationFound;
            payload.customersRestored = restoration.customersRestored;
            payload.customersStillOut = restoration.customersStillOut;
            payload.estimatedSaidiMinutesAvoided = restoration.estimatedSaidiMinutesAvoided;
        }

        summary.textContent = JSON.stringify(payload, null, 2);
    }

    function labelOf(elementId) {
        var element = elementsById[elementId];
        return element ? element.label : "#" + elementId;
    }

    function buildLegend() {
        legendBox.replaceChildren.apply(legendBox, DEVICE_NAMES.map(function (name, index) {
            var swatch = el("span", { class: "legend__swatch", "aria-hidden": "true" });
            swatch.style.setProperty("--swatch", DEVICE_COLORS[index]);
            return el("span", { class: "d-inline-flex align-items-center gap-1 small" }, swatch, name);
        }));
    }

    // ── Projection ──────────────────────────────────────────────────────────

    // Equirectangular projection with longitude scaled by cos(mid-latitude), fitted
    // uniformly so the circuit keeps its true geographic shape.
    function buildProjection(elements) {
        var minLat = Infinity, maxLat = -Infinity, minLon = Infinity, maxLon = -Infinity;

        elements.forEach(function (e) {
            var lats = [e.fromLatitude, e.toLatitude];
            var lons = [e.fromLongitude, e.toLongitude];
            var i;
            for (i = 0; i < 2; i++) {
                if (lats[i] < minLat) { minLat = lats[i]; }
                if (lats[i] > maxLat) { maxLat = lats[i]; }
                if (lons[i] < minLon) { minLon = lons[i]; }
                if (lons[i] > maxLon) { maxLon = lons[i]; }
            }
        });

        var midLat = (minLat + maxLat) / 2;
        var cosLat = Math.cos(midLat * Math.PI / 180);
        var xSpan = (maxLon - minLon) * cosLat;
        var ySpan = maxLat - minLat;
        var pad = 5;
        var usable = 100 - pad * 2;
        var scale = usable / Math.max(xSpan, ySpan, 1e-9);

        return {
            minLat: minLat,
            minLon: minLon,
            cosLat: cosLat,
            scale: scale,
            offsetX: pad + (usable - xSpan * scale) / 2,
            offsetY: pad + (usable - ySpan * scale) / 2,
            ySpanScaled: ySpan * scale
        };
    }

    function projectX(longitude) {
        return projection.offsetX + (longitude - projection.minLon) * projection.cosLat * projection.scale;
    }

    function projectY(latitude) {
        // Latitude increases north, SVG y increases downward.
        return projection.offsetY + projection.ySpanScaled - (latitude - projection.minLat) * projection.scale;
    }

    // ── Single-line diagram ─────────────────────────────────────────────────

    function draw() {
        charts.svg.clear(svg);
        hideTooltip();
        if (!network || !projection) { return; }

        var downstream = toSet(trace ? trace.downstreamElementIds : []);
        var upstream = toSet(trace ? trace.upstreamElementIds : []);
        var isolation = toSet(trace ? trace.isolationDeviceIds : []);
        var energized = toSet(restoration ? restoration.energizedElementIds : []);
        var feeder = feederSelect.value;

        // Conductors first, then devices on top so the glyphs stay readable.
        var ordered = network.elements.slice().sort(function (a, b) {
            return (a.deviceType === DEVICE.CONDUCTOR ? 0 : 1) - (b.deviceType === DEVICE.CONDUCTOR ? 0 : 1);
        });

        ordered.forEach(function (element) {
            var dimmed = feeder !== "" && element.feederName !== feeder && element.feederName !== "Tie";
            var style = styleFor(element, downstream, upstream, isolation, energized);
            drawElement(element, style, dimmed);
        });

        drawSubstation();

        charts.describe(svg, "Single-line diagram of " + network.elements.length + " network elements." +
            (trace ? " Fault on " + labelOf(trace.faultElementId) + "; " + trace.customersAffected +
                " customers affected." : " No fault placed.") +
            (restoration ? " " + restoration.customersRestored + " customers restored." : "") +
            " Use the Fault location list to place a fault from the keyboard.");
    }

    function styleFor(element, downstream, upstream, isolation, energized) {
        var restored = restoration && downstream[element.id] && energized[element.id];

        if (trace && element.id === trace.faultElementId) {
            return { stroke: COLOR_OUT, width: 1.1, opacity: 1, halo: COLOR_OUT, fault: true };
        }
        if (isolation[element.id]) {
            return { stroke: COLOR_ISOLATE, width: 0.9, opacity: 1, halo: COLOR_ISOLATE };
        }
        if (restored) {
            return { stroke: COLOR_RESTORED, width: 0.8, opacity: 1 };
        }
        if (downstream[element.id]) {
            return { stroke: COLOR_OUT, width: 0.8, opacity: 0.95 };
        }
        if (upstream[element.id]) {
            return { stroke: COLOR_UPSTREAM, width: 0.8, opacity: 1 };
        }
        if (restoration && restoration.tieElementId === element.id) {
            return { stroke: COLOR_RESTORED, width: 0.8, opacity: 1, dashed: true };
        }
        if (element.isOpen) {
            return { stroke: "var(--text-muted)", width: 0.5, opacity: 0.75, dashed: true };
        }
        return { stroke: "var(--text-muted)", width: 0.45, opacity: trace ? 0.35 : 0.7 };
    }

    function drawElement(element, style, dimmed) {
        var x1 = projectX(element.fromLongitude);
        var y1 = projectY(element.fromLatitude);
        var x2 = projectX(element.toLongitude);
        var y2 = projectY(element.toLatitude);
        var opacity = dimmed ? 0.1 : style.opacity;

        var line = svgEl("line", {
            x1: x1.toFixed(2), y1: y1.toFixed(2), x2: x2.toFixed(2), y2: y2.toFixed(2),
            stroke: style.stroke,
            "stroke-width": String(style.width),
            "stroke-opacity": String(opacity),
            "stroke-linecap": "round"
        });
        if (style.dashed) {
            line.setAttribute("stroke-dasharray", "1.2 0.9");
        }
        svg.appendChild(line);

        if (element.deviceType !== DEVICE.CONDUCTOR) {
            drawGlyph(element, (x1 + x2) / 2, (y1 + y2) / 2, style, opacity);
        }

        // A wide transparent overlay gives the thin conductors a usable pointer hit area.
        // It is the pointer path only (FE-A06): the keyboard / assistive-tech path is the
        // Fault location list, so these ~260 lines are out of the tab order and hidden from
        // the accessibility tree rather than being 260 undersized controls.
        var hit = svgEl("line", {
            x1: x1.toFixed(2), y1: y1.toFixed(2), x2: x2.toFixed(2), y2: y2.toFixed(2),
            stroke: "transparent",
            "stroke-width": "1.8",
            "stroke-linecap": "round"
        });
        hit.style.cursor = dimmed ? "default" : "pointer";
        hit.addEventListener("mousemove", function (evt) { showTooltip(evt, element); });
        if (!dimmed) {
            hit.setAttribute("tabindex", "-1");
            hit.setAttribute("aria-hidden", "true");
            hit.addEventListener("click", function () { placeFault(element.id); });
        }
        svg.appendChild(hit);
    }

    function drawGlyph(element, cx, cy, style, opacity) {
        var fill = style.fault || style.halo ? style.stroke : DEVICE_COLORS[element.deviceType];
        var node;

        if (element.deviceType === DEVICE.RECLOSER) {
            node = svgEl("circle", { cx: cx.toFixed(2), cy: cy.toFixed(2), r: "1.05" });
        } else if (element.deviceType === DEVICE.TRANSFORMER) {
            node = svgEl("polygon", {
                points: (cx).toFixed(2) + "," + (cy - 0.95).toFixed(2) + " " +
                    (cx - 0.9).toFixed(2) + "," + (cy + 0.8).toFixed(2) + " " +
                    (cx + 0.9).toFixed(2) + "," + (cy + 0.8).toFixed(2)
            });
        } else {
            var half = element.deviceType === DEVICE.BREAKER ? 1.15
                : element.deviceType === DEVICE.FUSE ? 0.6 : 0.85;
            node = svgEl("rect", {
                x: (cx - half).toFixed(2), y: (cy - 0.85).toFixed(2),
                width: (half * 2).toFixed(2), height: "1.7", rx: "0.15"
            });
        }

        // A normally-open point is drawn hollow: it is a device that is deliberately
        // not carrying current, which is the whole reason a tie exists.
        if (element.isOpen) {
            node.setAttribute("fill", "var(--surface)");
            node.setAttribute("stroke", fill);
            node.setAttribute("stroke-width", "0.28");
            node.setAttribute("stroke-dasharray", "0.6 0.4");
        } else {
            node.setAttribute("fill", fill);
        }
        node.setAttribute("opacity", String(opacity));
        svg.appendChild(node);

        if (style.halo) {
            svg.appendChild(svgEl("circle", {
                cx: cx.toFixed(2), cy: cy.toFixed(2), r: "2.2", fill: "none",
                stroke: style.halo, "stroke-width": "0.35", opacity: String(opacity)
            }));
        }
    }

    function drawSubstation() {
        var source = null;
        network.elements.forEach(function (e) {
            if (source === null && e.fromNodeId === network.sourceNodeId) {
                source = { lat: e.fromLatitude, lon: e.fromLongitude };
            }
        });
        if (!source) { return; }

        var cx = projectX(source.lon);
        var cy = projectY(source.lat);

        svg.appendChild(svgEl("rect", {
            x: (cx - 1.8).toFixed(2), y: (cy - 1.8).toFixed(2), width: "3.6", height: "3.6", rx: "0.4",
            fill: "var(--surface)", stroke: "var(--text)", "stroke-width": "0.35"
        }));

        charts.svg.text(svg, cx, cy + 4.4, "SUB", { size: 2.4, anchor: "middle" });
    }

    // ── Tooltip ─────────────────────────────────────────────────────────────

    function showTooltip(evt, element) {
        var bounds = panel.getBoundingClientRect();
        var state = element.isOpen ? "open" : "closed";

        tooltip.replaceChildren(
            el("strong", { text: element.label }),
            document.createElement("br"),
            DEVICE_NAMES[element.deviceType] + " · " + state,
            document.createElement("br"),
            element.customerCount + " customer" + (element.customerCount === 1 ? "" : "s") +
                " · " + element.feederName);

        tooltip.style.left = (evt.clientX - bounds.left + 14) + "px";
        tooltip.style.top = (evt.clientY - bounds.top + 14) + "px";
        tooltip.classList.remove("d-none");
    }

    function hideTooltip() {
        tooltip.classList.add("d-none");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    function toSet(ids) {
        var set = {};
        (ids || []).forEach(function (id) { set[id] = true; });
        return set;
    }
}());
