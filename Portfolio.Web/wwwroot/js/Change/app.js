// Raster Change Detection Page — self-contained IIFE, no ES module syntax.
//
// Rendering note: at 256x256 the raster is 65,536 cells and at 512x512 it is 262,144.
// The .raster-grid / .raster-cell approach the Terrain Analyzer uses would mean that many
// DOM nodes, which is a layout cost the browser cannot absorb. Everything raster here is
// drawn to a <canvas> via ImageData instead; only the histogram, which has 128 elements,
// is SVG.
(function () {
    "use strict";

    var SVGNS = "http://www.w3.org/2000/svg";
    var MASK_DISPLAY_SIZE = 512;

    // Band indices in the stack. The composite is NIR/Red/Green — the standard
    // vegetation-forward false-colour composite, in which healthy vegetation reads red.
    var BAND_RED = 0;
    var BAND_GREEN = 1;
    var BAND_NIR = 2;

    // Reflectance ceiling used to stretch 0..1 surface reflectance into 0..255.
    var DISPLAY_GAIN = 255 / 0.6;

    // Cache DOM nodes up front.
    var alertBox = document.getElementById("changeAlert");
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
    var canvasA = document.getElementById("changeCanvasA");
    var canvasB = document.getElementById("changeCanvasB");
    var swipeHandle = document.getElementById("changeSwipeHandle");
    var maskCanvas = document.getElementById("changeMaskCanvas");
    var histogramSvg = document.getElementById("changeHistogramSvg");
    var kpiDetections = document.getElementById("changeKpiDetections");
    var kpiArea = document.getElementById("changeKpiArea");
    var kpiThreshold = document.getElementById("changeKpiThreshold");
    var kpiRecovered = document.getElementById("changeKpiRecovered");

    // Module state.
    var scene = null;
    var detection = null;
    var selectedBlobId = 0;
    var swipeFraction = 0.5;
    var dragging = false;

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

    blobList.addEventListener("click", function (event) {
        var row = event.target.closest ? event.target.closest("[data-blob-id]") : null;
        if (!row) { return; }
        var id = parseInt(row.getAttribute("data-blob-id"), 10);
        selectedBlobId = selectedBlobId === id ? 0 : id;
        renderBlobList();
        drawMask();
    });

    syncThresholdMode();
    loadScene();

    // ── Data ────────────────────────────────────────────────────────────────

    function loadScene() {
        hideAlert();
        setBusy(true);

        var size = parseInt(sizeSelect.value, 10);
        var noise = parseFloat(noiseInput.value);
        var route = window.PortfolioApi.routes.spatialCompute.change.scene +
            "?width=" + size + "&height=" + size + "&noise=" + noise;

        apiGet(route)
            .then(function (result) {
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
            })
            .catch(function (err) { showAlert(err.message || "Failed to build the scene."); })
            .finally(function () { setBusy(false); });
    }

    function run() {
        if (!scene) {
            showAlert("The scene has not loaded yet.");
            return;
        }

        hideAlert();
        setBusy(true);

        apiPost(window.PortfolioApi.routes.spatialCompute.change.detect, {
            width: scene.width,
            height: scene.height,
            bandCount: scene.bandCount,
            epochA: scene.epochA,
            epochB: scene.epochB,
            thresholdMode: modeManual.checked ? "manual" : "otsu",
            manualThreshold: parseFloat(manualInput.value),
            openIterations: parseInt(openInput.value, 10),
            minBlobArea: parseInt(minAreaInput.value, 10)
        })
            .then(function (result) {
                detection = result;
                selectedBlobId = 0;
                render();
            })
            .catch(function (err) { showAlert(err.message || "Detection failed."); })
            .finally(function () { setBusy(false); });
    }

    function render() {
        var recovered = scoreAgainstGroundTruth();

        kpiDetections.textContent = detection.blobs.length;
        kpiArea.textContent = detection.changedPercent.toFixed(2) + "%";
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

    // ── False-colour composites and the swipe ───────────────────────────────

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
    }

    // ── Change mask overlay ─────────────────────────────────────────────────

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

        var scale = MASK_DISPLAY_SIZE / width;

        // Ground truth first, so detections draw on top of it.
        context.setLineDash([6, 4]);
        context.strokeStyle = "#4dabf7";
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
            context.strokeStyle = selected ? "#ffd43b" : "#51cf66";
            context.lineWidth = selected ? 3 : 2;
            strokeBox(context, blob, scale);

            context.fillStyle = selected ? "#ffd43b" : "#51cf66";
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

    // ── Otsu histogram ──────────────────────────────────────────────────────

    function drawHistogram() {
        histogramSvg.innerHTML = "";

        var left = 8, right = 97, top = 4, bottom = 44;

        appendLine(histogramSvg, left, top, left, bottom, "var(--text-muted)", 0.3);
        appendLine(histogramSvg, left, bottom, right, bottom, "var(--text-muted)", 0.3);
        appendText(histogramSvg, (left + right) / 2, 53, "CVA magnitude", 2.6, "middle", "var(--text-muted)");

        if (!detection || !detection.histogram || detection.histogram.length === 0) {
            appendText(histogramSvg, (left + right) / 2, (top + bottom) / 2,
                "Run a detection to build the histogram", 3, "middle", "var(--text-muted)");
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
            var rect = document.createElementNS(SVGNS, "rect");
            rect.setAttribute("x", (left + i * barWidth).toFixed(3));
            rect.setAttribute("y", (bottom - barHeight).toFixed(3));
            rect.setAttribute("width", Math.max(barWidth * 0.9, 0.15).toFixed(3));
            rect.setAttribute("height", barHeight.toFixed(3));
            rect.setAttribute("fill", "var(--primary-light)");
            histogramSvg.appendChild(rect);
        }

        var span = detection.histogramMax - detection.histogramMin;
        if (span > 0) {
            var t = (detection.threshold - detection.histogramMin) / span;
            t = Math.max(0, Math.min(1, t));
            var x = left + t * (right - left);
            var line = appendLine(histogramSvg, x, top, x, bottom, "#e03131", 0.45);
            line.setAttribute("stroke-dasharray", "1.6 1.2");
            appendText(histogramSvg, Math.min(x + 1, right - 12), top + 3,
                detection.thresholdMode + " " + detection.threshold.toFixed(3), 2.4, "start", "#e03131");
        }

        appendText(histogramSvg, left, bottom + 3.6, detection.histogramMin.toFixed(2), 2.2, "start", "var(--text-muted)");
        appendText(histogramSvg, right, bottom + 3.6, detection.histogramMax.toFixed(2), 2.2, "end", "var(--text-muted)");
    }

    function appendLine(svg, x1, y1, x2, y2, stroke, width) {
        var line = document.createElementNS(SVGNS, "line");
        line.setAttribute("x1", x1.toFixed(2));
        line.setAttribute("y1", y1.toFixed(2));
        line.setAttribute("x2", x2.toFixed(2));
        line.setAttribute("y2", y2.toFixed(2));
        line.setAttribute("stroke", stroke);
        line.setAttribute("stroke-width", String(width));
        svg.appendChild(line);
        return line;
    }

    function appendText(svg, x, y, content, size, anchor, fill) {
        var text = document.createElementNS(SVGNS, "text");
        text.setAttribute("x", x.toFixed(2));
        text.setAttribute("y", y.toFixed(2));
        text.setAttribute("font-size", String(size));
        text.setAttribute("text-anchor", anchor);
        text.setAttribute("fill", fill);
        text.textContent = content;
        svg.appendChild(text);
        return text;
    }

    // ── Detection list ──────────────────────────────────────────────────────

    function renderBlobList() {
        if (!detection || detection.blobs.length === 0) {
            blobList.innerHTML = '<div class="geo-cell-meta">No detections yet.</div>';
            return;
        }

        var rows = detection.blobs.map(function (blob) {
            var label = groundTruthLabelFor(blob);
            var percent = Math.round(blob.confidence * 100);
            var selected = blob.id === selectedBlobId;
            return '<div class="geo-cell-item' + (selected ? " geo-cell-anomaly" : "") +
                '" data-blob-id="' + blob.id + '" style="cursor:pointer;">' +
                '<div class="d-flex align-items-center justify-content-between">' +
                '<strong>#' + blob.id + " " + escapeHtml(label) + "</strong>" +
                "<span>" + blob.area + " px</span></div>" +
                '<div class="geo-cell-meta">centroid (' + blob.centroidX.toFixed(1) + ", " +
                blob.centroidY.toFixed(1) + ") &middot; mean magnitude " + blob.meanMagnitude.toFixed(3) + "</div>" +
                '<div class="progress mt-1" style="height:4px;" role="progressbar" aria-label="Relative confidence" ' +
                'aria-valuenow="' + percent + '" aria-valuemin="0" aria-valuemax="100">' +
                '<div class="progress-bar" style="width:' + percent + '%;background:var(--accent);"></div></div>' +
                '<div class="geo-cell-meta">relative confidence ' + percent + "%</div></div>";
        }).join("");

        blobList.innerHTML = rows;
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

    // ── Helpers ─────────────────────────────────────────────────────────────

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

    function setBusy(busy) {
        runBtn.disabled = busy;
        sceneBtn.disabled = busy;
        sizeSelect.disabled = busy;
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
