// Geometry Toolkit Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

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
    var alertBox = document.getElementById("geometryAlert");
    var svg = document.getElementById("geometrySvg");
    var resultText = document.getElementById("geometryResult");
    var kpiShapes = document.getElementById("geometryKpiShapes");
    var kpiNative = document.getElementById("geometryKpiNative");
    var kpiMode = document.getElementById("geometryKpiMode");

    pointsText.value = JSON.stringify(samplePoints, null, 2);
    triangulateBtn.addEventListener("click", triangulate);
    clipBtn.addEventListener("click", clip);

    function triangulate() {
        hideAlert();
        var points = readPoints();
        if (!points) return;

        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.geometry.triangulate, { points: points })
            .then(function (result) {
                renderTriangles(result, points);
            })
            .catch(function (err) { showAlert(err.message || "Triangulation failed."); })
            .finally(function () { setBusy(false); });
    }

    function clip() {
        hideAlert();
        var points = readPoints();
        if (!points) return;

        var request = {
            subject: points,
            minX: Number(document.getElementById("clipMinX").value),
            minY: Number(document.getElementById("clipMinY").value),
            maxX: Number(document.getElementById("clipMaxX").value),
            maxY: Number(document.getElementById("clipMaxY").value)
        };

        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.geometry.clip, request)
            .then(renderClip)
            .catch(function (err) { showAlert(err.message || "Clip operation failed."); })
            .finally(function () { setBusy(false); });
    }

    function renderTriangles(result, originalPoints) {
        clearSvg();
        result.triangles.forEach(function (triangle) {
            var polygon = document.createElementNS("http://www.w3.org/2000/svg", "polygon");
            polygon.setAttribute("class", "triangle-shape");
            polygon.setAttribute("points", [triangle.a, triangle.b, triangle.c].map(toSvgPoint).join(" "));
            svg.appendChild(polygon);
        });
        renderPoints(originalPoints);
        kpiShapes.textContent = result.triangles.length;
        kpiNative.textContent = result.nativeAccelerated ? "Yes" : "No";
        kpiMode.textContent = "Tri";
        resultText.textContent = JSON.stringify(result, null, 2);
    }

    function renderClip(result) {
        clearSvg();
        var polygon = document.createElementNS("http://www.w3.org/2000/svg", "polygon");
        polygon.setAttribute("class", "clip-shape");
        polygon.setAttribute("points", result.vertices.map(toSvgPoint).join(" "));
        svg.appendChild(polygon);
        renderPoints(result.vertices);
        kpiShapes.textContent = result.vertices.length > 0 ? 1 : 0;
        kpiNative.textContent = result.nativeAccelerated ? "Yes" : "No";
        kpiMode.textContent = "Clip";
        resultText.textContent = JSON.stringify(result, null, 2);
    }

    function renderPoints(points) {
        points.forEach(function (point) {
            var circle = document.createElementNS("http://www.w3.org/2000/svg", "circle");
            circle.setAttribute("cx", point.x * 100);
            circle.setAttribute("cy", 100 - point.y * 100);
            circle.setAttribute("r", "1.4");
            circle.setAttribute("fill", "currentColor");
            svg.appendChild(circle);
        });
    }

    function readPoints() {
        try {
            return JSON.parse(pointsText.value);
        } catch (err) {
            showAlert("Point set must be valid JSON.");
            return null;
        }
    }

    function toSvgPoint(point) {
        return (point.x * 100) + "," + (100 - point.y * 100);
    }

    function clearSvg() {
        svg.innerHTML = "";
    }

    function setBusy(busy) {
        triangulateBtn.disabled = busy;
        clipBtn.disabled = busy;
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
