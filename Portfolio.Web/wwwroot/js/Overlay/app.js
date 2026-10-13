// Spatial Overlay / Zone Tagger Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var fmt = window.PortfolioFormat;
    var charts = window.PortfolioCharts;

    var pointCountInput = document.getElementById("overlayPointCount");
    var zonesSelect = document.getElementById("overlayZones");
    var runBtn = document.getElementById("overlayRunBtn");
    var regenBtn = document.getElementById("overlayRegenBtn");
    var alertBox = PortfolioUi.inlineAlert(document.getElementById("overlayAlert"));
    var svg = document.getElementById("overlaySvg");
    var resultsPanel = svg.closest(".card");
    var zoneTable = document.getElementById("overlayZoneTable");
    var summary = document.getElementById("overlaySummary");
    var kpiPoints = document.getElementById("overlayKpiPoints");
    var kpiZones = document.getElementById("overlayKpiZones");
    var kpiUnassigned = document.getElementById("overlayKpiUnassigned");
    var kpiNative = document.getElementById("overlayKpiNative");

    // Seeded LCG shared with the other pages (PortfolioCharts.rng); one call advances it one step.
    var rng = charts.rng(987654321);
    var points = [];
    var zones = [];
    var lastResult = null;

    runBtn.addEventListener("click", run);
    regenBtn.addEventListener("click", function () { rng(); regenerate(); });
    zonesSelect.addEventListener("change", regenerate);
    // Palette colours are resolved per theme, so repaint when the theme flips.
    charts.onThemeChange(function () {
        drawScene(lastResult);
        if (lastResult) { buildTable(lastResult); }
    });

    regenerate();

    function clamp01(v) { return Math.max(0.01, Math.min(0.99, v)); }

    function regenerate() {
        zones = buildZones(zonesSelect.value);
        var count = Math.max(20, Math.min(4000, Number(pointCountInput.value) || 600));
        pointCountInput.value = count;
        points = [];
        lastResult = null;
        for (var i = 0; i < count; i++) {
            // Cluster points loosely around a few centers so zone counts vary.
            var mode = rng();
            var cx = mode < 0.4 ? 0.3 : (mode < 0.7 ? 0.7 : 0.5);
            var cy = mode < 0.4 ? 0.35 : (mode < 0.7 ? 0.6 : 0.8);
            points.push({ x: clamp01(cx + (rng() - 0.5) * 0.6), y: clamp01(cy + (rng() - 0.5) * 0.6) });
        }
        drawScene(null);
        zoneTable.replaceChildren();
        summary.textContent = "";
        charts.describe(svg, fmt.number(count) + " points over " + zones.length + " zones, not yet joined. Choose Run Spatial Join.");
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

    // Runs the join; the regenerate button is guarded too so the points cannot change mid-request.
    function run() {
        var request = { points: points, zones: zones };
        var releaseRegen = PortfolioUi.busy({ buttons: [regenBtn] });
        PortfolioUi.runAction({
            trigger: runBtn,
            alert: alertBox,
            region: resultsPanel,
            request: function () { return apiPost(window.PortfolioApi.routes.spatialCompute.overlay.spatialJoin, request); },
            render: render,
            summary: function (result) {
                return "Spatial join complete: " + fmt.number(result.assignedCount) + " points assigned, " +
                    fmt.number(result.unassignedCount) + " outside all zones.";
            }
        }).then(releaseRegen);
    }

    function render(result) {
        lastResult = result;
        drawScene(result);
        kpiPoints.textContent = fmt.number(result.points.length);
        kpiZones.textContent = fmt.number(result.zones.length);
        kpiUnassigned.textContent = fmt.number(result.unassignedCount);
        kpiNative.textContent = fmt.yesNo(result.nativeAccelerated);
        buildTable(result);
        summary.textContent = JSON.stringify({
            nativeAccelerated: result.nativeAccelerated,
            assignedCount: result.assignedCount,
            unassignedCount: result.unassignedCount
        }, null, 2);
        charts.describe(svg, result.zones.map(function (z) {
            return (z.name || ("Zone " + (z.zoneIndex + 1))) + ": " + z.pointCount;
        }).join("; ") + ". Outside all zones: " + result.unassignedCount + ".");
    }

    // `palette` is resolved once per draw (charts.palette reads computed styles), not once per point.
    function colorFor(palette, index) {
        return palette[index % palette.length];
    }

    function toSvg(p) { return (p.x * 100).toFixed(2) + "," + (100 - p.y * 100).toFixed(2); }

    function drawScene(result) {
        charts.svg.clear(svg);
        var palette = charts.palette(8);
        var maxCount = 1;
        if (result) {
            result.zones.forEach(function (z) { maxCount = Math.max(maxCount, z.pointCount); });
        }

        zones.forEach(function (zone, index) {
            var polygon = charts.svg.el("polygon", {
                points: zone.ring.map(toSvg).join(" "),
                stroke: colorFor(palette, index),
                "stroke-width": "0.5"
            });
            if (result) {
                var intensity = result.zones[index].pointCount / maxCount;
                polygon.setAttribute("fill", colorFor(palette, index));
                polygon.setAttribute("fill-opacity", (0.12 + intensity * 0.55).toFixed(2));
            } else {
                polygon.setAttribute("fill", "var(--surface)");
                polygon.setAttribute("fill-opacity", "0.15");
            }
            svg.appendChild(polygon);
        });

        var list = result ? result.points : points.map(function (p) { return { x: p.x, y: p.y, zoneIndex: -1 }; });
        list.forEach(function (p) {
            var circle = charts.svg.el("circle", {
                cx: (p.x * 100).toFixed(2),
                cy: (100 - p.y * 100).toFixed(2),
                r: "0.9"
            });
            if (p.zoneIndex >= 0) {
                circle.setAttribute("fill", colorFor(palette, p.zoneIndex));
            } else {
                circle.setAttribute("fill", "var(--text-muted)");
                circle.setAttribute("opacity", result ? "0.6" : "0.5");
            }
            svg.appendChild(circle);
        });
    }

    // One row per zone: colour swatch (--swatch via setProperty, CSP-safe), name, point count.
    function buildTable(result) {
        zoneTable.replaceChildren();
        var palette = charts.palette(8);
        result.zones.forEach(function (z) {
            var swatch = PortfolioDom.el("span", { "class": "legend__swatch", "aria-hidden": "true" });
            swatch.style.setProperty("--swatch", colorFor(palette, z.zoneIndex));
            zoneTable.appendChild(PortfolioDom.el("div", { "class": "d-flex align-items-center justify-content-between geo-cell-item mb-1" },
                PortfolioDom.el("span", { "class": "d-inline-flex align-items-center gap-2" },
                    swatch, z.name || ("Zone " + (z.zoneIndex + 1))),
                PortfolioDom.el("strong", { text: fmt.number(z.pointCount) })));
        });
    }
}());
