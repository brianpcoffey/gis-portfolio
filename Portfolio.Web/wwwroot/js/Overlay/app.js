// Spatial Overlay / Zone Tagger Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var SVGNS = "http://www.w3.org/2000/svg";
    var PALETTE = ["#4363d8", "#3cb44b", "#f58231", "#911eb4", "#e6194B", "#42d4f4", "#469990", "#bfef45"];

    var pointCountInput = document.getElementById("overlayPointCount");
    var zonesSelect = document.getElementById("overlayZones");
    var runBtn = document.getElementById("overlayRunBtn");
    var regenBtn = document.getElementById("overlayRegenBtn");
    var alertBox = document.getElementById("overlayAlert");
    var svg = document.getElementById("overlaySvg");
    var zoneTable = document.getElementById("overlayZoneTable");
    var summary = document.getElementById("overlaySummary");
    var kpiPoints = document.getElementById("overlayKpiPoints");
    var kpiZones = document.getElementById("overlayKpiZones");
    var kpiUnassigned = document.getElementById("overlayKpiUnassigned");
    var kpiNative = document.getElementById("overlayKpiNative");

    var seed = 987654321;
    var points = [];
    var zones = [];

    runBtn.addEventListener("click", run);
    regenBtn.addEventListener("click", function () { seed = (seed * 48271) % 2147483647; regenerate(); });
    zonesSelect.addEventListener("change", regenerate);

    regenerate();

    function rng() {
        seed = (seed * 48271) % 2147483647;
        return seed / 2147483647;
    }

    function clamp01(v) { return Math.max(0.01, Math.min(0.99, v)); }

    function regenerate() {
        zones = buildZones(zonesSelect.value);
        var count = Math.max(20, Math.min(4000, Number(pointCountInput.value) || 600));
        pointCountInput.value = count;
        points = [];
        for (var i = 0; i < count; i++) {
            // Cluster points loosely around a few centers so zone counts vary.
            var mode = rng();
            var cx = mode < 0.4 ? 0.3 : (mode < 0.7 ? 0.7 : 0.5);
            var cy = mode < 0.4 ? 0.35 : (mode < 0.7 ? 0.6 : 0.8);
            points.push({ x: clamp01(cx + (rng() - 0.5) * 0.6), y: clamp01(cy + (rng() - 0.5) * 0.6) });
        }
        drawScene(null);
        zoneTable.innerHTML = "";
        summary.textContent = "";
    }

    function buildZones(mode) {
        if (mode === "strips") {
            return [
                { name: "West", ring: rect(0.0, 0.0, 0.34, 1.0) },
                { name: "Central", ring: rect(0.34, 0.0, 0.66, 1.0) },
                { name: "East", ring: rect(0.66, 0.0, 1.0, 1.0) }
            ];
        }
        if (mode === "diamond") {
            return [
                { name: "Core", ring: [{ x: 0.5, y: 0.2 }, { x: 0.8, y: 0.5 }, { x: 0.5, y: 0.8 }, { x: 0.2, y: 0.5 }] },
                { name: "NW", ring: rect(0.0, 0.5, 0.5, 1.0) },
                { name: "NE", ring: rect(0.5, 0.5, 1.0, 1.0) },
                { name: "SW", ring: rect(0.0, 0.0, 0.5, 0.5) },
                { name: "SE", ring: rect(0.5, 0.0, 1.0, 0.5) }
            ];
        }
        return [
            { name: "NW District", ring: rect(0.0, 0.5, 0.5, 1.0) },
            { name: "NE District", ring: rect(0.5, 0.5, 1.0, 1.0) },
            { name: "SW District", ring: rect(0.0, 0.0, 0.5, 0.5) },
            { name: "SE District", ring: rect(0.5, 0.0, 1.0, 0.5) }
        ];
    }

    function rect(minX, minY, maxX, maxY) {
        return [
            { x: minX, y: minY },
            { x: maxX, y: minY },
            { x: maxX, y: maxY },
            { x: minX, y: maxY }
        ];
    }

    function run() {
        hideAlert();
        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.overlay.spatialJoin, { points: points, zones: zones })
            .then(function (result) { render(result); })
            .catch(function (err) { showAlert(err.message || "Spatial join failed."); })
            .finally(function () { setBusy(false); });
    }

    function render(result) {
        drawScene(result);
        kpiPoints.textContent = result.points.length;
        kpiZones.textContent = result.zones.length;
        kpiUnassigned.textContent = result.unassignedCount;
        kpiNative.textContent = result.nativeAccelerated ? "Yes" : "No";
        buildTable(result);
        summary.textContent = JSON.stringify({
            nativeAccelerated: result.nativeAccelerated,
            assignedCount: result.assignedCount,
            unassignedCount: result.unassignedCount
        }, null, 2);
    }

    function colorFor(index) { return PALETTE[index % PALETTE.length]; }

    function toSvg(p) { return (p.x * 100).toFixed(2) + "," + (100 - p.y * 100).toFixed(2); }

    function drawScene(result) {
        svg.innerHTML = "";
        var maxCount = 1;
        if (result) {
            result.zones.forEach(function (z) { maxCount = Math.max(maxCount, z.pointCount); });
        }

        zones.forEach(function (zone, index) {
            var polygon = document.createElementNS(SVGNS, "polygon");
            polygon.setAttribute("points", zone.ring.map(toSvg).join(" "));
            polygon.setAttribute("stroke", colorFor(index));
            polygon.setAttribute("stroke-width", "0.5");
            if (result) {
                var intensity = result.zones[index].pointCount / maxCount;
                polygon.setAttribute("fill", colorFor(index));
                polygon.setAttribute("fill-opacity", (0.12 + intensity * 0.55).toFixed(2));
            } else {
                polygon.setAttribute("fill", "var(--surface)");
                polygon.setAttribute("fill-opacity", "0.15");
            }
            svg.appendChild(polygon);
        });

        var list = result ? result.points : points.map(function (p) { return { x: p.x, y: p.y, zoneIndex: -1 }; });
        list.forEach(function (p) {
            var circle = document.createElementNS(SVGNS, "circle");
            circle.setAttribute("cx", (p.x * 100).toFixed(2));
            circle.setAttribute("cy", (100 - p.y * 100).toFixed(2));
            circle.setAttribute("r", "0.9");
            if (p.zoneIndex >= 0) {
                circle.setAttribute("fill", colorFor(p.zoneIndex));
            } else {
                circle.setAttribute("fill", "var(--text-muted)");
                circle.setAttribute("opacity", result ? "0.6" : "0.5");
            }
            svg.appendChild(circle);
        });
    }

    function buildTable(result) {
        var rows = result.zones.map(function (z) {
            return '<div class="d-flex align-items-center justify-content-between geo-cell-item mb-1">' +
                '<span class="d-inline-flex align-items-center gap-2">' +
                '<span style="width:0.7rem;height:0.7rem;border-radius:2px;background:' + colorFor(z.zoneIndex) + ';display:inline-block;"></span>' +
                escapeHtml(z.name || ("Zone " + (z.zoneIndex + 1))) + '</span>' +
                '<strong>' + z.pointCount + '</strong></div>';
        }).join("");
        zoneTable.innerHTML = rows;
    }

    function escapeHtml(text) {
        var div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    function setBusy(busy) {
        runBtn.disabled = busy;
        regenBtn.disabled = busy;
    }

    function showAlert(message) {
        alertBox.textContent = message;
        alertBox.classList.remove("d-none");
    }

    function hideAlert() {
        alertBox.classList.add("d-none");
        alertBox.textContent = "";
    }
}());
