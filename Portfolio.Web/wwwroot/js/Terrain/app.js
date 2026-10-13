// Terrain Analyzer Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var fmt = window.PortfolioFormat;

    var widthInput = document.getElementById("terrainWidth");
    var heightInput = document.getElementById("terrainHeight");
    var azimuthInput = document.getElementById("terrainAzimuth");
    var azimuthValue = document.getElementById("terrainAzimuthValue");
    var hillshadeBtn = document.getElementById("terrainHillshadeBtn");
    var heatmapBtn = document.getElementById("terrainHeatmapBtn");
    var alertBox = PortfolioUi.inlineAlert(document.getElementById("terrainAlert"));
    var grid = document.getElementById("terrainGrid");
    var resultsPanel = grid.closest(".card");
    var summary = document.getElementById("terrainSummary");
    var kpiCells = document.getElementById("terrainKpiCells");
    var kpiNative = document.getElementById("terrainKpiNative");
    var kpiMode = document.getElementById("terrainKpiMode");

    hillshadeBtn.addEventListener("click", runHillshade);
    heatmapBtn.addEventListener("click", runHeatmap);
    azimuthInput.addEventListener("input", showAzimuth);
    showAzimuth();

    // Visible value for the azimuth slider plus the matching aria-valuetext ("315°").
    function showAzimuth() {
        var text = azimuthInput.value + "°";
        azimuthValue.textContent = text;
        azimuthInput.setAttribute("aria-valuetext", text);
    }

    // Runs one request; the sibling button is guarded too so only one request is in flight.
    function runOperation(trigger, sibling, request, render, label) {
        var releaseSibling = PortfolioUi.busy({ buttons: [sibling] });
        PortfolioUi.runAction({
            trigger: trigger,
            alert: alertBox,
            region: resultsPanel,
            request: request,
            render: render,
            summary: function (result) { return label + " generated: " + fmt.number(result.width * result.height) + " cells."; }
        }).then(releaseSibling);
    }

    function runHillshade() {
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

        runOperation(hillshadeBtn, heatmapBtn,
            function () { return apiPost(window.PortfolioApi.routes.spatialCompute.raster.hillshade, request); },
            function (result) { renderRaster(result.width, result.height, result.intensities, "hillshade", result.nativeAccelerated); },
            "Hillshade");
    }

    function runHeatmap() {
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

        runOperation(heatmapBtn, hillshadeBtn,
            function () { return apiPost(window.PortfolioApi.routes.spatialCompute.raster.heatmap, request); },
            function (result) { renderRaster(result.width, result.height, result.values, "heatmap", result.nativeAccelerated); },
            "Heatmap");
    }

    function renderRaster(width, height, values, mode, nativeAccelerated) {
        grid.replaceChildren();
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

        kpiCells.textContent = fmt.number(width * height);
        kpiNative.textContent = fmt.yesNo(nativeAccelerated);
        kpiMode.textContent = mode === "hillshade" ? "Shade" : "Heat";
        summary.textContent = JSON.stringify({ width: width, height: height, nativeAccelerated: nativeAccelerated, mode: mode }, null, 2);
        grid.setAttribute("aria-label", (mode === "hillshade" ? "Hillshade" : "Heatmap") + " raster, " + width + " by " + height + " cells");
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
}());
