// Raster Change Detection Page — self-contained IIFE, no ES module syntax.
//
// Rendering note: at 256x256 the raster is 65,536 cells and at 512x512 it is 262,144.
// The .raster-grid / .raster-cell approach the Terrain Analyzer uses would mean that many
// DOM nodes, which is a layout cost the browser cannot absorb. Everything raster here is
// drawn to a <canvas> via ImageData instead; only the histogram, which has 128 elements,
// is SVG.
// Depends on the deferred shared modules: PortfolioUi, PortfolioFormat, PortfolioCharts, PortfolioDom.
(function () {
    "use strict";

    var MASK_DISPLAY_SIZE = 512;
    var charts = window.PortfolioCharts;
    var fmt = window.PortfolioFormat;
    var ui = window.PortfolioUi;

    // Bounding-box colours for the mask canvas. The canvas is painted onto an always-dark,
    // desaturated raster in both themes, so the bright colours are fixed rather than tokens.
    var BOX_COLORS = { truth: "#4dabf7", detected: "#51cf66", selected: "#ffd43b" };

    // Band indices in the stack. The composite is NIR/Red/Green — the standard
    // vegetation-forward false-colour composite, in which healthy vegetation reads red.
    var BAND_RED = 0;
    var BAND_GREEN = 1;
    var BAND_NIR = 2;

    // Reflectance ceiling used to stretch 0..1 surface reflectance into 0..255.
    var DISPLAY_GAIN = 255 / 0.6;

    var alertBox = ui.inlineAlert(document.getElementById("changeAlert"));
    var sizeSelect = document.getElementById("changeSize");
    var noiseInput = document.getElementById("changeNoise");
    var noiseValue = document.getElementById("changeNoiseValue");
    var modeOtsu = document.getElementById("changeModeOtsu");
    var modeManual = document.getElementById("changeModeManual");
    var manualInput = document.getElementById("changeManual");
    var manualValue = document.getElementById("changeManualValue");
    var openInput = document.getElementById("changeOpen");
    var minAreaInput = document.getElementById("changeMinArea");
    var runBtn = document.getElementById("changeRunBtn");
    var sceneBtn = document.getElementById("changeSceneBtn");
    var blobList = document.getElementById("changeBlobList");
    var summary = document.getElementById("changeSummary");
    var swipeStage = document.getElementById("changeSwipeStage");
    var resultsPanel = swipeStage.closest(".card");
    var swipeRange = document.getElementById("changeSwipeRange");
    var canvasA = document.getElementById("changeCanvasA");
    var canvasB = document.getElementById("changeCanvasB");
    var swipeHandle = document.getElementById("changeSwipeHandle");
    var maskCanvas = document.getElementById("changeMaskCanvas");
    var histogramSvg = document.getElementById("changeHistogramSvg");
    var kpiDetections = document.getElementById("changeKpiDetections");
    var kpiArea = document.getElementById("changeKpiArea");
    var kpiThreshold = document.getElementById("changeKpiThreshold");
    var kpiRecovered = document.getElementById("changeKpiRecovered");

    var scene = null;
    var detection = null;
    var selectedBlobId = 0;
    var swipeFraction = 0.5;
    var dragging = false;
    var sceneSeq = 0;

    runBtn.addEventListener("click", run);
    sceneBtn.addEventListener("click", loadScene);
    sizeSelect.addEventListener("change", loadScene);
    noiseInput.addEventListener("input", function () {
        noiseValue.textContent = parseFloat(noiseInput.value).toFixed(3);
    });
    noiseInput.addEventListener("change", loadScene);
    manualInput.addEventListener("input", function () {
        manualValue.textContent = parseFloat(manualInput.value).toFixed(2);
    });
    modeOtsu.addEventListener("change", syncThresholdMode);
    modeManual.addEventListener("change", syncThresholdMode);

    swipeStage.addEventListener("pointerdown", function (event) {
        dragging = true;
        swipeStage.setPointerCapture(event.pointerId);
        applySwipe(event);
    });
    swipeStage.addEventListener("pointermove", function (event) {
        if (dragging) { applySwipe(event); }
    });
    swipeStage.addEventListener("pointerup", function (event) {
        dragging = false;
        swipeStage.releasePointerCapture(event.pointerId);
    });
    swipeStage.addEventListener("pointercancel", function () { dragging = false; });

    // Keyboard/AT path to the divider. The range is the accessible mirror of the
    // drag gesture; applySwipeFraction pushes the other direction so the two stay
    // in step whichever one the visitor used.
    swipeRange.addEventListener("input", function () {
        applySwipeFraction(parseFloat(swipeRange.value) / 100);
    });

    blobList.addEventListener("click", function (event) {
        toggleBlobRow(event.target.closest ? event.target.closest("[data-blob-id]") : null);
    });

    // The rows are role="button", so they have to answer to Enter and Space the way a
    // real button does. Space is prevented so activating a row does not scroll the page.
    blobList.addEventListener("keydown", function (event) {
        if (event.key !== "Enter" && event.key !== " " && event.key !== "Spacebar") { return; }
        var row = event.target.closest ? event.target.closest("[data-blob-id]") : null;
        if (!row) { return; }
        event.preventDefault();
        toggleBlobRow(row);
    });

    // Re-rendering the list replaces the row that was activated, so focus is moved back
    // onto its replacement — otherwise keyboard focus falls to the top of the document.
    function toggleBlobRow(row) {
        if (!row) { return; }
        var id = parseInt(row.getAttribute("data-blob-id"), 10);
        selectedBlobId = selectedBlobId === id ? 0 : id;
        renderBlobList();
        drawMask();
        var refocus = blobList.querySelector('[data-blob-id="' + id + '"]');
        if (refocus) { refocus.focus(); }
    }

    syncThresholdMode();
    loadScene(true);

    // Holds the other controls busy while `opts` runs through PortfolioUi.runAction on `trigger`.
    function runExclusive(trigger, held, opts) {
        var release = ui.busy({ buttons: held });
        opts.trigger = trigger;
        opts.alert = alertBox;
        opts.region = resultsPanel;
        return ui.runAction(opts).then(function (r) { release(); return r; });
    }

    // `onLoad === true` is the page-load call: it renders but does not announce. The listeners pass an
    // event, which is not `true`, so a visitor's own New Scene / size / noise change is still announced.
    function loadScene(onLoad) {
        var size = parseInt(sizeSelect.value, 10);
        var noise = parseFloat(noiseInput.value);
        var route = window.PortfolioApi.routes.spatialCompute.change.scene +
            "?width=" + size + "&height=" + size + "&noise=" + noise;
        var seq = ++sceneSeq;

        runExclusive(sceneBtn, [runBtn, sizeSelect], {
            request: function () { return apiGet(route); },
            render: function (result) {
                if (seq !== sceneSeq) { return; }
                scene = result;
                detection = null;
                selectedBlobId = 0;
                drawComposites();
                drawMask();
                drawHistogram();
                renderBlobList();
                resetKpis();
                summary.textContent = JSON.stringify({
                    scene: scene.sceneName,
                    size: scene.width + " x " + scene.height,
                    bands: scene.bandNames.join(", "),
                    noiseLevel: scene.noiseLevel,
                    plantedChanges: scene.groundTruth.length
                }, null, 2);
            },
            summary: onLoad === true ? undefined : function (result) {
                return "Scene ready: " + result.width + " by " + result.height + " pixels, " +
                    result.groundTruth.length + " planted changes.";
            }
        });
    }

    function run() {
        if (!scene) {
            alertBox.show("The scene has not loaded yet.");
            return;
        }

        // The size select and noise slider stay live while Detect runs (they are only aria-disabled),
        // so a scene change can land first. Its result then belongs to a scene that is gone.
        var seq = sceneSeq;

        runExclusive(runBtn, [sceneBtn, sizeSelect], {
            request: function () {
                return apiPost(window.PortfolioApi.routes.spatialCompute.change.detect, {
                    width: scene.width,
                    height: scene.height,
                    bandCount: scene.bandCount,
                    epochA: scene.epochA,
                    epochB: scene.epochB,
                    thresholdMode: modeManual.checked ? "manual" : "otsu",
                    manualThreshold: parseFloat(manualInput.value),
                    openIterations: parseInt(openInput.value, 10),
                    minBlobArea: parseInt(minAreaInput.value, 10)
                });
            },
            render: function (result) {
                if (seq !== sceneSeq) { return; }
                detection = result;
                selectedBlobId = 0;
                render();
            },
            summary: function (result) {
                if (seq !== sceneSeq) { return ""; }
                return "Detection complete: " + result.blobs.length + " changes found, " +
                    kpiRecovered.textContent + " ground-truth changes recovered.";
            }
        });
    }

    function render() {
        var recovered = scoreAgainstGroundTruth();

        kpiDetections.textContent = detection.blobs.length;
        kpiArea.textContent = fmt.percent(detection.changedPercent);
        kpiThreshold.textContent = detection.threshold.toFixed(3);
        kpiRecovered.textContent = recovered + " of " + scene.groundTruth.length;

        drawMask();
        drawHistogram();
        renderBlobList();

        summary.textContent = JSON.stringify({
            nativeAccelerated: detection.nativeAccelerated,
            thresholdMode: detection.thresholdMode,
            threshold: round(detection.threshold, 4),
            changedPixels: detection.changedPixels,
            changedPercent: round(detection.changedPercent, 3),
            blobsBeforeFiltering: detection.blobsBeforeFiltering,
            detections: detection.blobs.length,
            groundTruthRecovered: recovered + "/" + scene.groundTruth.length
        }, null, 2);
    }

    // A planted change counts as recovered when at least one detection's centroid falls
    // inside its ground-truth box. Centroid-in-box is a deliberately simple criterion —
    // it is not IoU, and it says nothing about how much of the change was captured.
    function scoreAgainstGroundTruth() {
        if (!scene || !detection) { return 0; }

        var recovered = 0;
        scene.groundTruth.forEach(function (box) {
            var hit = false;
            detection.blobs.forEach(function (blob) {
                if (blob.centroidX >= box.minX && blob.centroidX <= box.maxX &&
                    blob.centroidY >= box.minY && blob.centroidY <= box.maxY) {
                    hit = true;
                }
            });
            if (hit) { recovered++; }
        });
        return recovered;
    }

    function drawComposites() {
        if (!scene) { return; }
        paintComposite(canvasA, scene.epochA);
        paintComposite(canvasB, scene.epochB);
        applySwipeFraction(swipeFraction);
    }

    function paintComposite(canvas, epoch) {
        var width = scene.width;
        var height = scene.height;
        var pixels = width * height;

        canvas.width = width;
        canvas.height = height;

        var context = canvas.getContext("2d");
        var image = context.createImageData(width, height);
        var data = image.data;

        var nirOffset = BAND_NIR * pixels;
        var redOffset = BAND_RED * pixels;
        var greenOffset = BAND_GREEN * pixels;

        for (var i = 0; i < pixels; i++) {
            var o = i * 4;
            data[o] = stretch(epoch[nirOffset + i]);
            data[o + 1] = stretch(epoch[redOffset + i]);
            data[o + 2] = stretch(epoch[greenOffset + i]);
            data[o + 3] = 255;
        }

        context.putImageData(image, 0, 0);
    }

    function stretch(reflectance) {
        var value = Math.round(reflectance * DISPLAY_GAIN);
        if (value < 0) { return 0; }
        return value > 255 ? 255 : value;
    }

    function applySwipe(event) {
        var rect = swipeStage.getBoundingClientRect();
        if (rect.width <= 0) { return; }
        applySwipeFraction((event.clientX - rect.left) / rect.width);
    }

    function applySwipeFraction(fraction) {
        swipeFraction = Math.max(0, Math.min(1, fraction));
        var percent = swipeFraction * 100;
        // Epoch B is revealed to the left of the divider, epoch A stays visible right of it.
        canvasB.style.clipPath = "inset(0 " + (100 - percent).toFixed(2) + "% 0 0)";
        swipeHandle.style.left = percent.toFixed(2) + "%";
        if (swipeRange) { swipeRange.value = Math.round(percent); }
    }

    function drawMask() {
        if (!scene) { return; }

        var width = scene.width;
        var height = scene.height;
        var pixels = width * height;

        maskCanvas.width = MASK_DISPLAY_SIZE;
        maskCanvas.height = MASK_DISPLAY_SIZE;
        var context = maskCanvas.getContext("2d");
        context.imageSmoothingEnabled = false;
        context.clearRect(0, 0, MASK_DISPLAY_SIZE, MASK_DISPLAY_SIZE);

        // Build the base at native raster resolution, then blit it up so the boxes can be
        // drawn with a sane stroke width in display space.
        var offscreen = document.createElement("canvas");
        offscreen.width = width;
        offscreen.height = height;
        var offContext = offscreen.getContext("2d");
        var image = offContext.createImageData(width, height);
        var data = image.data;

        var nirOffset = BAND_NIR * pixels;
        var redOffset = BAND_RED * pixels;
        var greenOffset = BAND_GREEN * pixels;
        var mask = detection ? detection.mask : null;

        for (var i = 0; i < pixels; i++) {
            var o = i * 4;
            // Desaturated epoch B underneath so the mask reads clearly on top.
            var luminance = Math.round(
                0.5 * stretch(scene.epochB[nirOffset + i]) +
                0.3 * stretch(scene.epochB[redOffset + i]) +
                0.2 * stretch(scene.epochB[greenOffset + i]));
            if (luminance > 255) { luminance = 255; }

            if (mask && mask[i]) {
                data[o] = 240;
                data[o + 1] = 60;
                data[o + 2] = 60;
            } else {
                data[o] = Math.round(luminance * 0.55);
                data[o + 1] = Math.round(luminance * 0.55);
                data[o + 2] = Math.round(luminance * 0.6);
            }
            data[o + 3] = 255;
        }

        offContext.putImageData(image, 0, 0);
        context.drawImage(offscreen, 0, 0, MASK_DISPLAY_SIZE, MASK_DISPLAY_SIZE);

        charts.describe(maskCanvas, "Change mask over epoch B. " + scene.groundTruth.length + " planted ground-truth boxes" +
            (detection ? " and " + detection.blobs.length + " detections." : "; no detection has been run yet."));

        var scale = MASK_DISPLAY_SIZE / width;

        // Ground truth first, so detections draw on top of it.
        context.setLineDash([6, 4]);
        context.strokeStyle = BOX_COLORS.truth;
        context.lineWidth = 2;
        scene.groundTruth.forEach(function (box) {
            strokeBox(context, box, scale);
        });

        if (!detection) { return; }

        context.setLineDash([]);
        context.lineWidth = 2;
        context.font = "14px sans-serif";
        detection.blobs.forEach(function (blob) {
            var selected = blob.id === selectedBlobId;
            context.strokeStyle = selected ? BOX_COLORS.selected : BOX_COLORS.detected;
            context.lineWidth = selected ? 3 : 2;
            strokeBox(context, blob, scale);

            context.fillStyle = selected ? BOX_COLORS.selected : BOX_COLORS.detected;
            context.fillText(String(blob.id), blob.minX * scale + 3, Math.max(14, blob.minY * scale - 3));
        });
    }

    function strokeBox(context, box, scale) {
        context.strokeRect(
            box.minX * scale,
            box.minY * scale,
            (box.maxX - box.minX + 1) * scale,
            (box.maxY - box.minY + 1) * scale);
    }

    function drawHistogram() {
        charts.svg.clear(histogramSvg);

        var left = 8, right = 97, top = 4, bottom = 44;

        charts.svg.line(histogramSvg, left, top, left, bottom, { width: 0.3 });
        charts.svg.line(histogramSvg, left, bottom, right, bottom, { width: 0.3 });
        charts.svg.text(histogramSvg, (left + right) / 2, 53, "CVA magnitude", { size: 2.6, anchor: "middle" });

        if (!detection || !detection.histogram || detection.histogram.length === 0) {
            charts.svg.text(histogramSvg, (left + right) / 2, (top + bottom) / 2,
                "Run a detection to build the histogram", { size: 3, anchor: "middle" });
            charts.describe(histogramSvg, "Change vector analysis magnitude histogram. No detection has been run yet.");
            return;
        }

        var bins = detection.histogram;
        var maxCount = 0;
        var i;
        for (i = 0; i < bins.length; i++) {
            if (bins[i] > maxCount) { maxCount = bins[i]; }
        }
        if (maxCount <= 0) { maxCount = 1; }

        // Log counts: the unchanged population outnumbers the changed one by two orders of
        // magnitude, so a linear axis would flatten the second mode to nothing.
        var logMax = Math.log(1 + maxCount);
        var barWidth = (right - left) / bins.length;

        for (i = 0; i < bins.length; i++) {
            if (bins[i] <= 0) { continue; }
            var barHeight = (Math.log(1 + bins[i]) / logMax) * (bottom - top);
            histogramSvg.appendChild(charts.svg.el("rect", {
                x: (left + i * barWidth).toFixed(3),
                y: (bottom - barHeight).toFixed(3),
                width: Math.max(barWidth * 0.9, 0.15).toFixed(3),
                height: barHeight.toFixed(3),
                fill: "var(--chart-series)"
            }));
        }

        var span = detection.histogramMax - detection.histogramMin;
        if (span > 0) {
            var t = (detection.threshold - detection.histogramMin) / span;
            t = Math.max(0, Math.min(1, t));
            var x = left + t * (right - left);
            var line = charts.svg.line(histogramSvg, x, top, x, bottom, { stroke: "var(--status-danger-fill)", width: 0.45 });
            line.setAttribute("stroke-dasharray", "1.6 1.2");
            charts.svg.text(histogramSvg, Math.min(x + 1, right - 12), top + 3,
                detection.thresholdMode + " " + detection.threshold.toFixed(3),
                { size: 2.4, anchor: "start", fill: "var(--status-danger-text)" });
        }

        charts.svg.text(histogramSvg, left, bottom + 3.6, detection.histogramMin.toFixed(2), { size: 2.2, anchor: "start" });
        charts.svg.text(histogramSvg, right, bottom + 3.6, detection.histogramMax.toFixed(2), { size: 2.2, anchor: "end" });

        charts.describe(histogramSvg, "Change vector analysis magnitude histogram on a log count axis, " +
            detection.histogramMin.toFixed(2) + " to " + detection.histogramMax.toFixed(2) + ". The " +
            detection.thresholdMode + " threshold is " + detection.threshold.toFixed(3) + ".");
    }

    function renderBlobList() {
        var el = window.PortfolioDom.el;
        if (!detection || detection.blobs.length === 0) {
            blobList.replaceChildren(el("div", { class: "geo-cell-meta", text: "No detections yet." }));
            return;
        }

        var rows = detection.blobs.map(function (blob) {
            var label = groundTruthLabelFor(blob);
            var percent = Math.round(blob.confidence * 100);
            var selected = blob.id === selectedBlobId;
            var bar = el("div", { class: "progress-bar" });
            bar.style.width = percent + "%";
            return el("div", {
                    class: "geo-cell-item change-blob-row" + (selected ? " geo-cell-anomaly" : ""),
                    "data-blob-id": blob.id, role: "button", tabindex: "0", "aria-pressed": selected ? "true" : "false"
                },
                el("div", { class: "d-flex align-items-center justify-content-between" },
                    el("strong", { text: "#" + blob.id + " " + label }),
                    el("span", { text: blob.area + " px" })),
                el("div", { class: "geo-cell-meta",
                    text: "centroid (" + blob.centroidX.toFixed(1) + ", " + blob.centroidY.toFixed(1) +
                        ") · mean magnitude " + blob.meanMagnitude.toFixed(3) }),
                el("div", { class: "progress mt-1 change-blob-confidence", role: "progressbar", "aria-label": "Relative confidence",
                    "aria-valuenow": percent, "aria-valuemin": "0", "aria-valuemax": "100" }, bar),
                el("div", { class: "geo-cell-meta", text: "relative confidence " + percent + "%" }));
        });

        blobList.replaceChildren.apply(blobList, rows);
    }

    // Names a detection after the planted change it landed in, so hits and false positives
    // are readable without cross-referencing the raster.
    function groundTruthLabelFor(blob) {
        var label = "Unlabelled change";
        scene.groundTruth.forEach(function (box) {
            if (blob.centroidX >= box.minX && blob.centroidX <= box.maxX &&
                blob.centroidY >= box.minY && blob.centroidY <= box.maxY) {
                label = box.label;
            }
        });
        return label;
    }

    function syncThresholdMode() {
        manualInput.disabled = !modeManual.checked;
    }

    function resetKpis() {
        kpiDetections.textContent = "–";
        kpiArea.textContent = "–";
        kpiThreshold.textContent = "–";
        kpiRecovered.textContent = "–";
    }

    function round(value, digits) {
        var factor = Math.pow(10, digits);
        return Math.round(value * factor) / factor;
    }
}());
