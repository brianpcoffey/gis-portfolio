// Hotspot Clusterer Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var fmt = window.PortfolioFormat;
    var charts = window.PortfolioCharts;

    var epsilonInput = document.getElementById("clusterEpsilon");
    var epsilonValue = document.getElementById("clusterEpsilonValue");
    var minPointsInput = document.getElementById("clusterMinPoints");
    var datasetSelect = document.getElementById("clusterDataset");
    var runBtn = document.getElementById("clusterRunBtn");
    var regenBtn = document.getElementById("clusterRegenBtn");
    var alertBox = PortfolioUi.inlineAlert(document.getElementById("clusterAlert"));
    var svg = document.getElementById("clusterSvg");
    var resultsPanel = svg.closest(".card");
    var legend = document.getElementById("clusterLegend");
    var summary = document.getElementById("clusterSummary");
    var kpiPoints = document.getElementById("clusterKpiPoints");
    var kpiClusters = document.getElementById("clusterKpiClusters");
    var kpiNoise = document.getElementById("clusterKpiNoise");
    var kpiNative = document.getElementById("clusterKpiNative");

    // Seeded LCG shared with the other pages (PortfolioCharts.rng); one call advances it one step.
    var rng = charts.rng(1337);
    var points = [];
    var lastResult = null;

    epsilonInput.addEventListener("input", function () { epsilonValue.textContent = Number(epsilonInput.value).toFixed(2); });
    runBtn.addEventListener("click", run);
    regenBtn.addEventListener("click", function () { rng(); regenerate(); });
    datasetSelect.addEventListener("change", regenerate);
    // Palette colours are resolved per theme, so repaint when the theme flips.
    charts.onThemeChange(function () {
        drawPoints(lastResult ? lastResult.points : null);
        if (lastResult) { buildLegend(lastResult); }
    });

    regenerate();

    function gaussian(mean, spread) {
        var u = Math.max(rng(), 1e-9);
        var v = rng();
        return mean + spread * Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v);
    }

    function clamp01(value) { return Math.max(0.01, Math.min(0.99, value)); }

    function regenerate() {
        var mode = datasetSelect.value;
        points = [];
        lastResult = null;
        if (mode === "blobs") {
            addBlob(0.28, 0.30, 0.05, 35);
            addBlob(0.70, 0.35, 0.05, 30);
            addBlob(0.50, 0.75, 0.06, 40);
            addScatter(25);
        } else if (mode === "ring") {
            for (var i = 0; i < 60; i++) {
                var angle = rng() * Math.PI * 2;
                var radius = 0.32 + gaussian(0, 0.015);
                points.push({ x: clamp01(0.5 + radius * Math.cos(angle)), y: clamp01(0.5 + radius * Math.sin(angle)) });
            }
            addBlob(0.5, 0.5, 0.04, 25);
            addScatter(15);
        } else {
            for (var j = 0; j < 120; j++) {
                points.push({ x: clamp01(rng()), y: clamp01(rng()) });
            }
        }
        drawPoints(null);
        legend.replaceChildren();
        summary.textContent = "";
        charts.describe(svg, fmt.number(points.length) + " sample points, not yet clustered. Choose Run DBSCAN.");
    }

    function addBlob(cx, cy, spread, count) {
        for (var i = 0; i < count; i++) {
            points.push({ x: clamp01(gaussian(cx, spread)), y: clamp01(gaussian(cy, spread)) });
        }
    }

    function addScatter(count) {
        for (var i = 0; i < count; i++) {
            points.push({ x: clamp01(rng()), y: clamp01(rng()) });
        }
    }

    // Runs DBSCAN; the regenerate button is guarded too so the points cannot change mid-request.
    function run() {
        var request = {
            epsilon: Number(epsilonInput.value),
            minPoints: Number(minPointsInput.value),
            points: points
        };

        var releaseRegen = PortfolioUi.busy({ buttons: [regenBtn] });
        PortfolioUi.runAction({
            trigger: runBtn,
            alert: alertBox,
            region: resultsPanel,
            request: function () { return apiPost(window.PortfolioApi.routes.spatialCompute.clustering.dbscan, request); },
            render: render,
            summary: function (result) {
                return result.clusterCount + " clusters, " + result.noiseCount + " noise points.";
            }
        }).then(releaseRegen);
    }

    function render(result) {
        lastResult = result;
        drawPoints(result.points);
        kpiPoints.textContent = fmt.number(result.points.length);
        kpiClusters.textContent = fmt.number(result.clusterCount);
        kpiNoise.textContent = fmt.number(result.noiseCount);
        kpiNative.textContent = fmt.yesNo(result.nativeAccelerated);
        buildLegend(result);
        summary.textContent = JSON.stringify({
            nativeAccelerated: result.nativeAccelerated,
            clusterCount: result.clusterCount,
            noiseCount: result.noiseCount,
            clusterSizes: result.clusterSizes
        }, null, 2);
        charts.describe(svg, result.clusterCount + " clusters of " + result.clusterSizes.join(", ") +
            " points, and " + result.noiseCount + " noise points, from " + result.points.length + " points.");
    }

    function colorFor(clusterId) {
        if (clusterId < 0) return "var(--text-muted)";
        var palette = charts.palette(8);
        return palette[clusterId % palette.length];
    }

    // DBSCAN can find more clusters than the palette has colours. Clusters 1-8 are filled circles in
    // --chart-1..8 (unchanged); clusters 9-16 reuse those colours as hollow rings and 17-24 as filled
    // squares, so no two of the first 24 clusters share both colour and shape (the cycle then
    // repeats). The legend swatch mirrors the marker, so shape - not only colour - tells them apart.
    var PALETTE_SIZE = 8;
    var MARKERS = ["circle", "ring", "square"];

    function markerFor(clusterId) {
        return MARKERS[Math.floor(clusterId / PALETTE_SIZE) % MARKERS.length];
    }

    // The ring and square markers for clusters beyond the first eight (same footprint as the circle).
    function shapedMarker(p, color) {
        var cx = p.x * 100;
        var cy = 100 - p.y * 100;
        if (markerFor(p.clusterId) === "square") {
            return charts.svg.el("rect", {
                x: (cx - 1.5).toFixed(2), y: (cy - 1.5).toFixed(2), width: "3", height: "3", fill: color
            });
        }
        return charts.svg.el("circle", {
            cx: cx.toFixed(2), cy: cy.toFixed(2), r: "1.4", fill: "none", stroke: color, "stroke-width": "0.6"
        });
    }

    function drawPoints(labelled) {
        charts.svg.clear(svg);
        var list = labelled || points.map(function (p) { return { x: p.x, y: p.y, clusterId: -2 }; });
        var palette = charts.palette(8);
        var unclustered = charts.status("info");
        list.forEach(function (p) {
            if (p.clusterId >= 0 && markerFor(p.clusterId) !== "circle") {
                svg.appendChild(shapedMarker(p, palette[p.clusterId % palette.length]));
                return;
            }
            var circle = charts.svg.el("circle", {
                cx: (p.x * 100).toFixed(2),
                cy: (100 - p.y * 100).toFixed(2),
                r: p.clusterId === -1 ? "1.1" : "1.7"
            });
            if (p.clusterId === -2) {
                circle.setAttribute("fill", unclustered);
                circle.setAttribute("opacity", "0.7");
            } else if (p.clusterId === -1) {
                circle.setAttribute("fill", "var(--text-muted)");
                circle.setAttribute("opacity", "0.55");
            } else {
                circle.setAttribute("fill", palette[p.clusterId % palette.length]);
            }
            svg.appendChild(circle);
        });
    }

    function buildLegend(result) {
        var items = result.clusterSizes.map(function (size, index) {
            return { color: colorFor(index), label: "Cluster " + (index + 1) + " · " + fmt.number(size) };
        });
        if (result.noiseCount > 0) {
            items.push({ color: "var(--text-muted)", label: "Noise · " + fmt.number(result.noiseCount) });
        }
        charts.legend(legend, items);
        // Mirror the plot markers on the swatches (.legend__swatch--ring / --square, spatialcompute.css).
        result.clusterSizes.forEach(function (size, index) {
            var marker = markerFor(index);
            if (marker !== "circle") {
                legend.children[index].querySelector(".legend__swatch").classList.add("legend__swatch--" + marker);
            }
        });
    }
}());
