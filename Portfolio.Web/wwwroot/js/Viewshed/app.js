// Viewshed Analyzer Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var sizeInput = document.getElementById("viewshedSize");
    var heightInput = document.getElementById("viewshedHeight");
    var heightValue = document.getElementById("viewshedHeightValue");
    var terrainSelect = document.getElementById("viewshedTerrain");
    var runBtn = document.getElementById("viewshedRunBtn");
    var regenBtn = document.getElementById("viewshedRegenBtn");
    var alertBox = document.getElementById("viewshedAlert");
    var grid = document.getElementById("viewshedGrid");
    var summary = document.getElementById("viewshedSummary");
    var kpiVisible = document.getElementById("viewshedKpiVisible");
    var kpiPct = document.getElementById("viewshedKpiPct");
    var kpiObserver = document.getElementById("viewshedKpiObserver");
    var kpiNative = document.getElementById("viewshedKpiNative");

    var seed = 20260718;
    var width = 24;
    var height = 24;
    var elevation = [];
    var observerX = 12;
    var observerY = 12;
    var lastResult = null;

    heightInput.addEventListener("input", function () { heightValue.textContent = heightInput.value; });
    runBtn.addEventListener("click", run);
    regenBtn.addEventListener("click", function () { seed = (seed * 48271) % 2147483647; regenerate(); });
    terrainSelect.addEventListener("change", regenerate);

    regenerate();

    function rng() {
        seed = (seed * 48271) % 2147483647;
        return seed / 2147483647;
    }

    function regenerate() {
        width = clampSize(Number(sizeInput.value));
        height = width;
        sizeInput.value = width;
        observerX = Math.floor(width / 2);
        observerY = Math.floor(height / 2);
        elevation = generateTerrain(terrainSelect.value, width, height);
        lastResult = null;
        drawGrid(null);
        summary.textContent = "";
    }

    function clampSize(value) {
        if (isNaN(value)) return 24;
        return Math.max(8, Math.min(40, Math.round(value)));
    }

    function generateTerrain(mode, w, h) {
        var values = [];
        var cx = (w - 1) / 2;
        var cy = (h - 1) / 2;
        for (var y = 0; y < h; y++) {
            for (var x = 0; x < w; x++) {
                var base;
                if (mode === "peak") {
                    var dist = Math.sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / Math.max(w, h);
                    base = 500 * Math.exp(-dist * dist * 8);
                } else if (mode === "rolling") {
                    base = 120 * Math.sin(x / 3.0) * Math.cos(y / 3.5) + 60 * Math.sin((x + y) / 4.0);
                } else {
                    // ridge & valley: a diagonal ridge line
                    var ridge = Math.exp(-Math.pow((x - y) / (w * 0.18), 2));
                    base = 400 * ridge + 40 * Math.sin(y / 2.5);
                }
                values.push(Math.round(300 + base + rng() * 25));
            }
        }
        return values;
    }

    function run() {
        hideAlert();
        var request = {
            width: width,
            height: height,
            cellSize: 30,
            observerX: observerX,
            observerY: observerY,
            observerHeight: Number(heightInput.value),
            elevation: elevation
        };

        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.viewshed.compute, request)
            .then(function (result) {
                lastResult = result;
                drawGrid(result);
                kpiVisible.textContent = result.visibleCells;
                kpiPct.textContent = Math.round((result.visibleCells / result.totalCells) * 100) + "%";
                kpiObserver.textContent = "(" + result.observerX + "," + result.observerY + ")";
                kpiNative.textContent = result.nativeAccelerated ? "Yes" : "No";
                summary.textContent = JSON.stringify({
                    nativeAccelerated: result.nativeAccelerated,
                    visibleCells: result.visibleCells,
                    totalCells: result.totalCells,
                    observer: [result.observerX, result.observerY]
                }, null, 2);
            })
            .catch(function (err) { showAlert(err.message || "Viewshed computation failed."); })
            .finally(function () { setBusy(false); });
    }

    function drawGrid(result) {
        grid.innerHTML = "";
        grid.style.gridTemplateColumns = "repeat(" + width + ", 1fr)";
        var min = Math.min.apply(null, elevation);
        var max = Math.max.apply(null, elevation);
        var range = Math.max(1, max - min);

        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var idx = y * width + x;
                var cell = document.createElement("div");
                cell.className = "raster-cell viewshed-cell";
                cell.dataset.x = x;
                cell.dataset.y = y;

                var shade = Math.round(60 + ((elevation[idx] - min) / range) * 160);
                if (x === observerX && y === observerY) {
                    cell.style.backgroundColor = "#0d6efd";
                } else if (result && result.visibility[idx] === 1) {
                    cell.style.backgroundColor = "rgba(56, 161, 105, " + (0.35 + ((elevation[idx] - min) / range) * 0.6) + ")";
                } else if (result) {
                    cell.style.backgroundColor = "rgb(" + Math.round(shade * 0.45) + "," + Math.round(shade * 0.45) + "," + Math.round(shade * 0.5) + ")";
                } else {
                    cell.style.backgroundColor = "rgb(" + shade + "," + shade + "," + shade + ")";
                }
                cell.addEventListener("click", onCellClick);
                grid.appendChild(cell);
            }
        }
    }

    function onCellClick(event) {
        observerX = Number(event.currentTarget.dataset.x);
        observerY = Number(event.currentTarget.dataset.y);
        run();
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
