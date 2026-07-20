// Outage Manager & Network Trace Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var SVGNS = "http://www.w3.org/2000/svg";

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
    var DEVICE_COLORS = ["#868e96", "#7048e8", "#f59f00", "#1098ad", "#e8590c", "#0ca678", "#4c6ef5"];

    var COLOR_OUT = "#e03131";
    var COLOR_UPSTREAM = "#1c7ed6";
    var COLOR_ISOLATE = "#f59f00";
    var COLOR_RESTORED = "#2f9e44";

    // Cache DOM nodes up front.
    var alertBox = document.getElementById("outageAlert");
    var feederSelect = document.getElementById("outageFeeder");
    var repairInput = document.getElementById("outageRepair");
    var restoreBtn = document.getElementById("outageRestoreBtn");
    var clearBtn = document.getElementById("outageClearBtn");
    var legendBox = document.getElementById("outageLegend");
    var planList = document.getElementById("outagePlan");
    var summary = document.getElementById("outageSummary");
    var svg = document.getElementById("outageSvg");
    var tooltip = document.getElementById("outageTooltip");
    var panel = svg.parentNode;
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

    restoreBtn.addEventListener("click", proposeRestoration);
    clearBtn.addEventListener("click", clearFault);
    feederSelect.addEventListener("change", draw);
    svg.addEventListener("mouseleave", hideTooltip);

    buildLegend();
    loadNetwork();

    // ── Data ────────────────────────────────────────────────────────────────

    function loadNetwork() {
        hideAlert();
        setBusy(true);
        apiGet(window.PortfolioApi.routes.spatialCompute.outage.network)
            .then(function (result) {
                network = result;
                elementsById = {};
                network.elements.forEach(function (e) { elementsById[e.id] = e; });
                projection = buildProjection(network.elements);
                clearFault();
            })
            .catch(function (err) { showAlert(err.message || "Failed to load the distribution network."); })
            .finally(function () { setBusy(false); });
    }

    function placeFault(elementId) {
        if (!network) { return; }

        hideAlert();
        setBusy(true);
        restoration = null;

        apiPost(window.PortfolioApi.routes.spatialCompute.outage.trace, {
            elements: network.elements,
            sourceNodeId: network.sourceNodeId,
            faultElementId: elementId
        })
            .then(function (result) {
                trace = result;
                restoreBtn.disabled = false;
                render();
            })
            .catch(function (err) { showAlert(err.message || "The fault trace failed."); })
            .finally(function () { setBusy(false); });
    }

    function proposeRestoration() {
        if (!trace) { return; }

        hideAlert();
        setBusy(true);

        apiPost(window.PortfolioApi.routes.spatialCompute.outage.restore, {
            elements: network.elements,
            sourceNodeId: network.sourceNodeId,
            faultElementId: trace.faultElementId,
            isolationDeviceIds: trace.isolationDeviceIds,
            assumedRepairMinutes: parseFloat(repairInput.value) || 0
        })
            .then(function (result) {
                restoration = result;
                render();
            })
            .catch(function (err) { showAlert(err.message || "The restoration search failed."); })
            .finally(function () { setBusy(false); });
    }

    function clearFault() {
        trace = null;
        restoration = null;
        restoreBtn.disabled = true;
        render();
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
        kpiPercent.textContent = trace ? trace.percentAffected.toFixed(1) + "%" : "–";
        kpiIsolation.textContent = trace ? String(trace.isolationDeviceIds.length) : "–";
        kpiRestored.textContent = restoration ? String(restoration.customersRestored) : "–";
    }

    function renderPlan() {
        if (!restoration || restoration.plan.length === 0) {
            planList.innerHTML = '<li class="geo-cell-item">' +
                (trace ? "Run the restoration search to build a switching plan."
                       : "Click a conductor on the diagram to place a fault.") + "</li>";
            return;
        }

        planList.innerHTML = restoration.plan.map(function (step) {
            var color = step.action === "CLOSE" ? COLOR_RESTORED : COLOR_ISOLATE;
            return '<li class="geo-cell-item">' +
                '<div class="d-flex align-items-center justify-content-between">' +
                "<strong>" + escapeHtml(step.label) + "</strong>" +
                '<span class="badge" style="background:' + color + ';">' + escapeHtml(step.action) + "</span>" +
                "</div>" +
                '<div class="geo-cell-meta">element #' + step.elementId + "</div></li>";
        }).join("");
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
        legendBox.innerHTML = DEVICE_NAMES.map(function (name, index) {
            return '<span class="d-inline-flex align-items-center gap-1 small">' +
                '<span style="width:0.7rem;height:0.7rem;border-radius:2px;background:' +
                DEVICE_COLORS[index] + ';display:inline-block;"></span>' +
                escapeHtml(name) + "</span>";
        }).join("");
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
        svg.innerHTML = "";
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

        var line = document.createElementNS(SVGNS, "line");
        line.setAttribute("x1", x1.toFixed(2));
        line.setAttribute("y1", y1.toFixed(2));
        line.setAttribute("x2", x2.toFixed(2));
        line.setAttribute("y2", y2.toFixed(2));
        line.setAttribute("stroke", style.stroke);
        line.setAttribute("stroke-width", String(style.width));
        line.setAttribute("stroke-opacity", String(opacity));
        line.setAttribute("stroke-linecap", "round");
        if (style.dashed) {
            line.setAttribute("stroke-dasharray", "1.2 0.9");
        }
        svg.appendChild(line);

        if (element.deviceType !== DEVICE.CONDUCTOR) {
            drawGlyph(element, (x1 + x2) / 2, (y1 + y2) / 2, style, opacity);
        }

        // A wide transparent overlay gives the thin conductors a usable hit area.
        var hit = document.createElementNS(SVGNS, "line");
        hit.setAttribute("x1", x1.toFixed(2));
        hit.setAttribute("y1", y1.toFixed(2));
        hit.setAttribute("x2", x2.toFixed(2));
        hit.setAttribute("y2", y2.toFixed(2));
        hit.setAttribute("stroke", "transparent");
        hit.setAttribute("stroke-width", "1.8");
        hit.setAttribute("stroke-linecap", "round");
        hit.style.cursor = dimmed ? "default" : "pointer";
        hit.addEventListener("mousemove", function (evt) { showTooltip(evt, element); });
        if (!dimmed) {
            // The hit target is the only way to place a fault, so it has to be a real
            // control: named, focusable, and answering to Enter and Space. Space is
            // prevented so activating a segment does not scroll the page.
            hit.setAttribute("role", "button");
            hit.setAttribute("tabindex", "0");
            hit.setAttribute("aria-label", "Place fault on " +
                DEVICE_NAMES[element.deviceType] + " " + element.label);
            hit.addEventListener("click", function () { placeFault(element.id); });
            hit.addEventListener("keydown", function (evt) {
                if (evt.key !== "Enter" && evt.key !== " " && evt.key !== "Spacebar") { return; }
                evt.preventDefault();
                placeFault(element.id);
            });
        }
        svg.appendChild(hit);
    }

    function drawGlyph(element, cx, cy, style, opacity) {
        var fill = style.fault || style.halo ? style.stroke : DEVICE_COLORS[element.deviceType];
        var node;

        if (element.deviceType === DEVICE.RECLOSER) {
            node = document.createElementNS(SVGNS, "circle");
            node.setAttribute("cx", cx.toFixed(2));
            node.setAttribute("cy", cy.toFixed(2));
            node.setAttribute("r", "1.05");
        } else if (element.deviceType === DEVICE.TRANSFORMER) {
            node = document.createElementNS(SVGNS, "polygon");
            node.setAttribute("points",
                (cx).toFixed(2) + "," + (cy - 0.95).toFixed(2) + " " +
                (cx - 0.9).toFixed(2) + "," + (cy + 0.8).toFixed(2) + " " +
                (cx + 0.9).toFixed(2) + "," + (cy + 0.8).toFixed(2));
        } else {
            var half = element.deviceType === DEVICE.BREAKER ? 1.15
                : element.deviceType === DEVICE.FUSE ? 0.6 : 0.85;
            node = document.createElementNS(SVGNS, "rect");
            node.setAttribute("x", (cx - half).toFixed(2));
            node.setAttribute("y", (cy - 0.85).toFixed(2));
            node.setAttribute("width", (half * 2).toFixed(2));
            node.setAttribute("height", "1.7");
            node.setAttribute("rx", "0.15");
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
            var halo = document.createElementNS(SVGNS, "circle");
            halo.setAttribute("cx", cx.toFixed(2));
            halo.setAttribute("cy", cy.toFixed(2));
            halo.setAttribute("r", "2.2");
            halo.setAttribute("fill", "none");
            halo.setAttribute("stroke", style.halo);
            halo.setAttribute("stroke-width", "0.35");
            halo.setAttribute("opacity", String(opacity));
            svg.appendChild(halo);
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

        var box = document.createElementNS(SVGNS, "rect");
        box.setAttribute("x", (cx - 1.8).toFixed(2));
        box.setAttribute("y", (cy - 1.8).toFixed(2));
        box.setAttribute("width", "3.6");
        box.setAttribute("height", "3.6");
        box.setAttribute("rx", "0.4");
        box.setAttribute("fill", "var(--surface)");
        box.setAttribute("stroke", "var(--text)");
        box.setAttribute("stroke-width", "0.35");
        svg.appendChild(box);

        var label = document.createElementNS(SVGNS, "text");
        label.setAttribute("x", cx.toFixed(2));
        label.setAttribute("y", (cy + 4.4).toFixed(2));
        label.setAttribute("font-size", "2.4");
        label.setAttribute("text-anchor", "middle");
        label.setAttribute("fill", "var(--text-muted)");
        label.textContent = "SUB";
        svg.appendChild(label);
    }

    // ── Tooltip ─────────────────────────────────────────────────────────────

    function showTooltip(evt, element) {
        var bounds = panel.getBoundingClientRect();
        var state = element.isOpen ? "open" : "closed";

        tooltip.innerHTML = "<strong>" + escapeHtml(element.label) + "</strong><br />" +
            escapeHtml(DEVICE_NAMES[element.deviceType]) + " · " + state + "<br />" +
            element.customerCount + " customer" + (element.customerCount === 1 ? "" : "s") +
            " · " + escapeHtml(element.feederName);

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

    function setBusy(busy) {
        clearBtn.disabled = busy;
        restoreBtn.disabled = busy || !trace;
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
