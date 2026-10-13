// Viewshed Analyzer Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var fmt = window.PortfolioFormat;

    var sizeInput = document.getElementById("viewshedSize");
    var heightInput = document.getElementById("viewshedHeight");
    var heightValue = document.getElementById("viewshedHeightValue");
    var terrainSelect = document.getElementById("viewshedTerrain");
    var observerXInput = document.getElementById("viewshedObserverX");
    var observerYInput = document.getElementById("viewshedObserverY");
    var runBtn = document.getElementById("viewshedRunBtn");
    var regenBtn = document.getElementById("viewshedRegenBtn");
    var alertBox = PortfolioUi.inlineAlert(document.getElementById("viewshedAlert"));
    var grid = document.getElementById("viewshedGrid");
    var resultsPanel = grid.closest(".card");
    var summary = document.getElementById("viewshedSummary");
    var kpiVisible = document.getElementById("viewshedKpiVisible");
    var kpiPct = document.getElementById("viewshedKpiPct");
    var kpiObserver = document.getElementById("viewshedKpiObserver");
    var kpiNative = document.getElementById("viewshedKpiNative");

    // Seeded LCG shared with the other pages (PortfolioCharts.rng); one call advances it one step.
    var rng = PortfolioCharts.rng(20260718);
    var width = 24;
    var height = 24;
    var elevation = [];
    var observerX = 12;
    var observerY = 12;
    var running = false;

    heightInput.addEventListener("input", function () { heightValue.textContent = heightInput.value; });
    runBtn.addEventListener("click", function () { onObserverInput(); run(); });
    regenBtn.addEventListener("click", function () { rng(); regenerate(); });
    terrainSelect.addEventListener("change", regenerate);
    // Cells are mouse targets; the column/row inputs are the keyboard-operable equivalent (FE-A05).
    grid.addEventListener("click", onCellClick);
    observerXInput.addEventListener("change", onObserverInput);
    observerYInput.addEventListener("change", onObserverInput);

    regenerate();

    function regenerate() {
        width = clampSize(Number(sizeInput.value));
        height = width;
        sizeInput.value = width;
        observerXInput.max = width - 1;
        observerYInput.max = height - 1;
        setObserver(Math.floor(width / 2), Math.floor(height / 2));
        elevation = generateTerrain(terrainSelect.value, width, height);
        drawGrid(null);
        summary.textContent = "";
    }

    function setObserver(x, y) {
        observerX = x;
        observerY = y;
        observerXInput.value = x;
        observerYInput.value = y;
    }

    // Typed column/row: clamp into the grid and move the marker; the next Compute uses it. The grid is
    // redrawn (shading cleared) only when the observer actually moved, so pressing Compute with the
    // observer where it already is does not blank the last result, nor leave it blank if the run fails.
    function onObserverInput() {
        if (running) { return; }
        var x = Math.max(0, Math.min(width - 1, Math.round(Number(observerXInput.value) || 0)));
        var y = Math.max(0, Math.min(height - 1, Math.round(Number(observerYInput.value) || 0)));
        var moved = x !== observerX || y !== observerY;
        setObserver(x, y);
        if (moved) { drawGrid(null); }
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

    // One request per Compute press or cell click (the compute endpoints are rate limited).
    function run() {
        var request = {
            width: width,
            height: height,
            cellSize: 30,
            observerX: observerX,
            observerY: observerY,
            observerHeight: Number(heightInput.value),
            elevation: elevation
        };

        running = true;
        var releaseRegen = PortfolioUi.busy({ buttons: [regenBtn] });
        PortfolioUi.runAction({
            trigger: runBtn,
            alert: alertBox,
            region: resultsPanel,
            request: function () { return apiPost(window.PortfolioApi.routes.spatialCompute.viewshed.compute, request); },
            render: render,
            summary: function (result) {
                return fmt.number(result.visibleCells) + " of " + fmt.number(result.totalCells) + " cells visible (" +
                    fmt.percent(result.visibleCells / result.totalCells, { fraction: true }) + ") from observer (" +
                    result.observerX + "," + result.observerY + ").";
            }
        }).then(function () {
            running = false;
            releaseRegen();
        });
    }

    function render(result) {
        drawGrid(result);
        kpiVisible.textContent = fmt.number(result.visibleCells);
        kpiPct.textContent = fmt.percent(result.visibleCells / result.totalCells, { fraction: true });
        kpiObserver.textContent = "(" + result.observerX + "," + result.observerY + ")";
        kpiNative.textContent = fmt.yesNo(result.nativeAccelerated);
        summary.textContent = JSON.stringify({
            nativeAccelerated: result.nativeAccelerated,
            visibleCells: result.visibleCells,
            totalCells: result.totalCells,
            observer: [result.observerX, result.observerY]
        }, null, 2);
    }

    function drawGrid(result) {
        grid.replaceChildren();
        grid.style.gridTemplateColumns = "repeat(" + width + ", 1fr)";
        var min = Math.min.apply(null, elevation);
        var max = Math.max.apply(null, elevation);
        var range = Math.max(1, max - min);
        // The legend swatch carries the observer colour, so read it rather than repeat a literal.
        var swatch = document.querySelector(".viewshed-swatch-observer");
        var observerColor = swatch ? getComputedStyle(swatch).backgroundColor : "";

        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var idx = y * width + x;
                var cell = document.createElement("div");
                cell.className = "raster-cell viewshed-cell";
                cell.dataset.x = x;
                cell.dataset.y = y;

                var shade = Math.round(60 + ((elevation[idx] - min) / range) * 160);
                if (x === observerX && y === observerY) {
                    cell.style.backgroundColor = observerColor;
                } else if (result && result.visibility[idx] === 1) {
                    cell.style.backgroundColor = "rgba(56, 161, 105, " + (0.35 + ((elevation[idx] - min) / range) * 0.6) + ")";
                } else if (result) {
                    cell.style.backgroundColor = "rgb(" + Math.round(shade * 0.45) + "," + Math.round(shade * 0.45) + "," + Math.round(shade * 0.5) + ")";
                } else {
                    cell.style.backgroundColor = "rgb(" + shade + "," + shade + "," + shade + ")";
                }
                grid.appendChild(cell);
            }
        }
        grid.setAttribute("aria-label", "Viewshed visibility grid, " + width + " by " + height + " cells, observer at column " +
            observerX + ", row " + observerY + (result ? ", " + fmt.number(result.visibleCells) + " cells visible" : ""));
    }

    function onCellClick(event) {
        var cell = event.target.closest(".viewshed-cell");
        if (!cell || running) { return; }
        setObserver(Number(cell.dataset.x), Number(cell.dataset.y));
        run();
    }
}());
