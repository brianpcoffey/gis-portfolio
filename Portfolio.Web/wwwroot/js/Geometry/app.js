// Geometry Toolkit Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var fmt = window.PortfolioFormat;
    var charts = window.PortfolioCharts;

    var samplePoints = [
        { x: 0.10, y: 0.20 },
        { x: 0.42, y: 0.08 },
        { x: 0.82, y: 0.28 },
        { x: 0.74, y: 0.78 },
        { x: 0.22, y: 0.88 }
    ];

    var pointsText = document.getElementById("geometryPoints");
    var triangulateBtn = document.getElementById("geometryTriangulateBtn");
    var clipBtn = document.getElementById("geometryClipBtn");
    var alertBox = PortfolioUi.inlineAlert(document.getElementById("geometryAlert"));
    var svg = document.getElementById("geometrySvg");
    var resultsPanel = svg.closest(".card");
    var resultText = document.getElementById("geometryResult");
    var kpiShapes = document.getElementById("geometryKpiShapes");
    var kpiNative = document.getElementById("geometryKpiNative");
    var kpiMode = document.getElementById("geometryKpiMode");

    pointsText.value = JSON.stringify(samplePoints, null, 2);
    triangulateBtn.addEventListener("click", triangulate);
    clipBtn.addEventListener("click", clip);
    charts.describe(svg, "No geometry computed yet. Choose Triangulate or Clip Vertices.");

    // Runs one operation; the sibling button is guarded too so only one request is in flight.
    function runOperation(trigger, sibling, request, render, summary) {
        var releaseSibling = PortfolioUi.busy({ buttons: [sibling] });
        PortfolioUi.runAction({
            trigger: trigger,
            alert: alertBox,
            region: resultsPanel,
            request: request,
            render: render,
            summary: summary
        }).then(releaseSibling);
    }

    function triangulate() {
        alertBox.hide();
        var points = readPoints();
        if (!points) return;

        runOperation(triangulateBtn, clipBtn,
            function () { return apiPost(window.PortfolioApi.routes.spatialCompute.geometry.triangulate, { points: points }); },
            function (result) { renderTriangles(result, points); },
            function (result) { return "Triangulation complete: " + fmt.number(result.triangles.length) + " triangles."; });
    }

    function clip() {
        alertBox.hide();
        var points = readPoints();
        if (!points) return;

        var request = {
            subject: points,
            minX: Number(document.getElementById("clipMinX").value),
            minY: Number(document.getElementById("clipMinY").value),
            maxX: Number(document.getElementById("clipMaxX").value),
            maxY: Number(document.getElementById("clipMaxY").value)
        };

        runOperation(clipBtn, triangulateBtn,
            function () { return apiPost(window.PortfolioApi.routes.spatialCompute.geometry.clip, request); },
            renderClip,
            function (result) { return "Clip complete: " + fmt.number(result.vertices.length) + " vertices in the clipped shape."; });
    }

    function renderTriangles(result, originalPoints) {
        charts.svg.clear(svg);
        result.triangles.forEach(function (triangle) {
            svg.appendChild(charts.svg.el("polygon", {
                "class": "triangle-shape",
                points: [triangle.a, triangle.b, triangle.c].map(toSvgPoint).join(" ")
            }));
        });
        renderPoints(originalPoints);
        kpiShapes.textContent = fmt.number(result.triangles.length);
        kpiNative.textContent = fmt.yesNo(result.nativeAccelerated);
        kpiMode.textContent = "Tri";
        resultText.textContent = JSON.stringify(result, null, 2);
        charts.describe(svg, "Triangulation of " + originalPoints.length + " points into " + result.triangles.length + " triangles.");
    }

    function renderClip(result) {
        charts.svg.clear(svg);
        svg.appendChild(charts.svg.el("polygon", {
            "class": "clip-shape",
            points: result.vertices.map(toSvgPoint).join(" ")
        }));
        renderPoints(result.vertices);
        kpiShapes.textContent = result.vertices.length > 0 ? 1 : 0;
        kpiNative.textContent = fmt.yesNo(result.nativeAccelerated);
        kpiMode.textContent = "Clip";
        resultText.textContent = JSON.stringify(result, null, 2);
        charts.describe(svg, "Clipped polygon with " + result.vertices.length + " vertices.");
    }

    function renderPoints(points) {
        points.forEach(function (point) {
            svg.appendChild(charts.svg.el("circle", {
                cx: point.x * 100,
                cy: 100 - point.y * 100,
                r: "1.4",
                fill: "currentColor"
            }));
        });
    }

    function readPoints() {
        try {
            return JSON.parse(pointsText.value);
        } catch (err) {
            alertBox.show("Point set must be valid JSON.");
            return null;
        }
    }

    function toSvgPoint(point) {
        return (point.x * 100) + "," + (100 - point.y * 100);
    }
}());
