// Terrain Analyzer Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var widthInput = document.getElementById("terrainWidth");
    var heightInput = document.getElementById("terrainHeight");
    var azimuthInput = document.getElementById("terrainAzimuth");
    var hillshadeBtn = document.getElementById("terrainHillshadeBtn");
    var heatmapBtn = document.getElementById("terrainHeatmapBtn");
    var alertBox = document.getElementById("terrainAlert");
    var grid = document.getElementById("terrainGrid");
    var summary = document.getElementById("terrainSummary");
    var kpiCells = document.getElementById("terrainKpiCells");
    var kpiNative = document.getElementById("terrainKpiNative");
    var kpiMode = document.getElementById("terrainKpiMode");

    hillshadeBtn.addEventListener("click", runHillshade);
    heatmapBtn.addEventListener("click", runHeatmap);

    function runHillshade() {
        hideAlert();
        var width = Number(widthInput.value);
        var height = Number(heightInput.value);
        var request = {
            width: width,
            height: height,
            cellSize: 30,
            azimuthDegrees: Number(azimuthInput.value),
            altitudeDegrees: 45,
            elevation: generateElevation(width, height)
        };

        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.raster.hillshade, request)
            .then(function (result) { renderRaster(result.width, result.height, result.intensities, "hillshade", result.nativeAccelerated); })
            .catch(function (err) { showAlert(err.message || "Hillshade generation failed."); })
            .finally(function () { setBusy(false); });
    }

    function runHeatmap() {
        hideAlert();
        var width = Number(widthInput.value);
        var height = Number(heightInput.value);
        var request = {
            width: width,
            height: height,
            minX: 0,
            minY: 0,
            maxX: 1,
            maxY: 1,
            radius: 0.18,
            points: [
                { x: 0.28, y: 0.30, weight: 1.0 },
                { x: 0.70, y: 0.62, weight: 0.8 },
                { x: 0.52, y: 0.78, weight: 0.6 }
            ]
        };

        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.raster.heatmap, request)
            .then(function (result) { renderRaster(result.width, result.height, result.values, "heatmap", result.nativeAccelerated); })
            .catch(function (err) { showAlert(err.message || "Heatmap generation failed."); })
            .finally(function () { setBusy(false); });
    }

    function renderRaster(width, height, values, mode, nativeAccelerated) {
        grid.innerHTML = "";
        grid.style.gridTemplateColumns = "repeat(" + width + ", 1fr)";
        values.forEach(function (value) {
            var cell = document.createElement("div");
            cell.className = "raster-cell";
            if (mode === "hillshade") {
                cell.style.backgroundColor = "rgb(" + value + "," + value + "," + value + ")";
            } else {
                var pct = Math.max(0, Math.min(1, value));
                cell.style.backgroundColor = "rgba(220, 53, 69, " + (0.15 + pct * 0.85) + ")";
            }
            grid.appendChild(cell);
        });

        kpiCells.textContent = width * height;
        kpiNative.textContent = nativeAccelerated ? "Yes" : "No";
        kpiMode.textContent = mode === "hillshade" ? "Shade" : "Heat";
        summary.textContent = JSON.stringify({ width: width, height: height, nativeAccelerated: nativeAccelerated, mode: mode }, null, 2);
    }

    function generateElevation(width, height) {
        var values = [];
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var ridge = Math.sin(x / Math.max(1, width - 1) * Math.PI) * 60;
                var valley = Math.cos(y / Math.max(1, height - 1) * Math.PI) * 35;
                values.push(Math.round(400 + ridge + valley + x * 5 + y * 3));
            }
        }
        return values;
    }

    function setBusy(busy) {
        hillshadeBtn.disabled = busy;
        heatmapBtn.disabled = busy;
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
