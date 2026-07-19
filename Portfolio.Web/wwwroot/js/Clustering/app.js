// Hotspot Clusterer Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var SVGNS = "http://www.w3.org/2000/svg";
    var PALETTE = [
        "#e6194B", "#3cb44b", "#4363d8", "#f58231", "#911eb4",
        "#42d4f4", "#f032e6", "#bfef45", "#fabed4", "#469990"
    ];

    var epsilonInput = document.getElementById("clusterEpsilon");
    var epsilonValue = document.getElementById("clusterEpsilonValue");
    var minPointsInput = document.getElementById("clusterMinPoints");
    var datasetSelect = document.getElementById("clusterDataset");
    var runBtn = document.getElementById("clusterRunBtn");
    var regenBtn = document.getElementById("clusterRegenBtn");
    var alertBox = document.getElementById("clusterAlert");
    var svg = document.getElementById("clusterSvg");
    var legend = document.getElementById("clusterLegend");
    var summary = document.getElementById("clusterSummary");
    var kpiPoints = document.getElementById("clusterKpiPoints");
    var kpiClusters = document.getElementById("clusterKpiClusters");
    var kpiNoise = document.getElementById("clusterKpiNoise");
    var kpiNative = document.getElementById("clusterKpiNative");

    var seed = 1337;
    var points = [];

    epsilonInput.addEventListener("input", function () { epsilonValue.textContent = Number(epsilonInput.value).toFixed(2); });
    runBtn.addEventListener("click", run);
    regenBtn.addEventListener("click", function () { seed = (seed * 48271) % 2147483647; regenerate(); });
    datasetSelect.addEventListener("change", regenerate);

    regenerate();

    // Deterministic pseudo-random generator so a given seed reproduces the same dataset.
    function rng() {
        seed = (seed * 48271) % 2147483647;
        return seed / 2147483647;
    }

    function gaussian(mean, spread) {
        var u = Math.max(rng(), 1e-9);
        var v = rng();
        return mean + spread * Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v);
    }

    function clamp01(value) { return Math.max(0.01, Math.min(0.99, value)); }

    function regenerate() {
        var mode = datasetSelect.value;
        points = [];
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
        summary.textContent = "";
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

    function run() {
        hideAlert();
        var request = {
            epsilon: Number(epsilonInput.value),
            minPoints: Number(minPointsInput.value),
            points: points
        };

        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.clustering.dbscan, request)
            .then(function (result) { render(result); })
            .catch(function (err) { showAlert(err.message || "Clustering failed."); })
            .finally(function () { setBusy(false); });
    }

    function render(result) {
        drawPoints(result.points);
        kpiPoints.textContent = result.points.length;
        kpiClusters.textContent = result.clusterCount;
        kpiNoise.textContent = result.noiseCount;
        kpiNative.textContent = result.nativeAccelerated ? "Yes" : "No";
        buildLegend(result);
        summary.textContent = JSON.stringify({
            nativeAccelerated: result.nativeAccelerated,
            clusterCount: result.clusterCount,
            noiseCount: result.noiseCount,
            clusterSizes: result.clusterSizes
        }, null, 2);
    }

    function colorFor(clusterId) {
        if (clusterId < 0) return "var(--text-muted)";
        return PALETTE[clusterId % PALETTE.length];
    }

    function drawPoints(labelled) {
        svg.innerHTML = "";
        var list = labelled || points.map(function (p) { return { x: p.x, y: p.y, clusterId: -2 }; });
        list.forEach(function (p) {
            var circle = document.createElementNS(SVGNS, "circle");
            circle.setAttribute("cx", (p.x * 100).toFixed(2));
            circle.setAttribute("cy", (100 - p.y * 100).toFixed(2));
            circle.setAttribute("r", p.clusterId === -1 ? "1.1" : "1.7");
            if (p.clusterId === -2) {
                circle.setAttribute("fill", "var(--bs-info)");
                circle.setAttribute("opacity", "0.7");
            } else if (p.clusterId === -1) {
                circle.setAttribute("fill", "var(--text-muted)");
                circle.setAttribute("opacity", "0.55");
            } else {
                circle.setAttribute("fill", colorFor(p.clusterId));
            }
            svg.appendChild(circle);
        });
    }

    function buildLegend(result) {
        legend.innerHTML = "";
        result.clusterSizes.forEach(function (size, index) {
            legend.appendChild(legendChip(colorFor(index), "Cluster " + (index + 1) + " · " + size));
        });
        if (result.noiseCount > 0) {
            legend.appendChild(legendChip("var(--text-muted)", "Noise · " + result.noiseCount));
        }
    }

    function legendChip(color, label) {
        var chip = document.createElement("span");
        chip.className = "badge d-inline-flex align-items-center gap-1";
        chip.style.background = "var(--surface)";
        chip.style.color = "var(--text)";
        chip.style.border = "1px solid var(--border)";
        var dot = document.createElement("span");
        dot.style.width = "0.7rem";
        dot.style.height = "0.7rem";
        dot.style.borderRadius = "999px";
        dot.style.background = color;
        dot.style.display = "inline-block";
        chip.appendChild(dot);
        chip.appendChild(document.createTextNode(label));
        return chip;
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
