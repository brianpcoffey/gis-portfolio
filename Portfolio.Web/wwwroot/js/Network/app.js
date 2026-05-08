// Route Planner Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var nodes = [
        { id: 1, latitude: 34.04, longitude: -117.22 },
        { id: 2, latitude: 34.07, longitude: -117.20 },
        { id: 3, latitude: 34.05, longitude: -117.18 },
        { id: 4, latitude: 34.08, longitude: -117.16 },
        { id: 5, latitude: 34.045, longitude: -117.145 }
    ];

    var edges = [
        { fromNodeId: 1, toNodeId: 2, cost: 3, bidirectional: true },
        { fromNodeId: 1, toNodeId: 3, cost: 6, bidirectional: true },
        { fromNodeId: 2, toNodeId: 3, cost: 2, bidirectional: true },
        { fromNodeId: 2, toNodeId: 4, cost: 5, bidirectional: true },
        { fromNodeId: 3, toNodeId: 5, cost: 4, bidirectional: true },
        { fromNodeId: 4, toNodeId: 5, cost: 2, bidirectional: true }
    ];

    var startSelect = document.getElementById("networkStart");
    var endSelect = document.getElementById("networkEnd");
    var maxCostInput = document.getElementById("networkMaxCost");
    var routeBtn = document.getElementById("networkRouteBtn");
    var serviceAreaBtn = document.getElementById("networkServiceAreaBtn");
    var alertBox = document.getElementById("networkAlert");
    var svg = document.getElementById("networkSvg");
    var resultText = document.getElementById("networkResult");
    var kpiCost = document.getElementById("networkKpiCost");
    var kpiNodes = document.getElementById("networkKpiNodes");
    var kpiNative = document.getElementById("networkKpiNative");

    routeBtn.addEventListener("click", findRoute);
    serviceAreaBtn.addEventListener("click", computeServiceArea);
    renderGraph([]);

    function findRoute() {
        hideAlert();
        var request = {
            nodes: nodes,
            edges: edges,
            startNodeId: Number(startSelect.value),
            endNodeId: Number(endSelect.value)
        };

        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.network.route, request)
            .then(function (result) {
                renderGraph(result.nodeIds || []);
                kpiCost.textContent = result.totalCost;
                kpiNodes.textContent = result.nodeIds.length;
                kpiNative.textContent = result.nativeAccelerated ? "Yes" : "No";
                resultText.textContent = JSON.stringify(result, null, 2);
            })
            .catch(function (err) { showAlert(err.message || "Route calculation failed."); })
            .finally(function () { setBusy(false); });
    }

    function computeServiceArea() {
        hideAlert();
        var request = {
            nodes: nodes,
            edges: edges,
            originNodeId: Number(startSelect.value),
            maxCost: Number(maxCostInput.value)
        };

        setBusy(true);
        apiPost(window.PortfolioApi.routes.spatialCompute.network.serviceArea, request)
            .then(function (result) {
                renderGraph(result.reachableNodeIds || []);
                kpiCost.textContent = request.maxCost;
                kpiNodes.textContent = result.reachableNodeIds.length;
                kpiNative.textContent = result.nativeAccelerated ? "Yes" : "No";
                resultText.textContent = JSON.stringify(result, null, 2);
            })
            .catch(function (err) { showAlert(err.message || "Service-area calculation failed."); })
            .finally(function () { setBusy(false); });
    }

    function renderGraph(highlightNodeIds) {
        svg.innerHTML = "";
        edges.forEach(function (edge) {
            var a = findNode(edge.fromNodeId);
            var b = findNode(edge.toNodeId);
            var line = document.createElementNS("http://www.w3.org/2000/svg", "line");
            line.setAttribute("x1", toX(a));
            line.setAttribute("y1", toY(a));
            line.setAttribute("x2", toX(b));
            line.setAttribute("y2", toY(b));
            line.setAttribute("stroke", "rgba(128,128,128,0.55)");
            line.setAttribute("stroke-width", "1.4");
            svg.appendChild(line);
        });

        if (highlightNodeIds.length > 1) {
            var path = document.createElementNS("http://www.w3.org/2000/svg", "polyline");
            path.setAttribute("class", "spatial-line");
            path.setAttribute("points", highlightNodeIds.map(function (id) { var n = findNode(id); return toX(n) + "," + toY(n); }).join(" "));
            svg.appendChild(path);
        }

        nodes.forEach(function (node) {
            var circle = document.createElementNS("http://www.w3.org/2000/svg", "circle");
            circle.setAttribute("cx", toX(node));
            circle.setAttribute("cy", toY(node));
            circle.setAttribute("r", highlightNodeIds.indexOf(node.id) >= 0 ? "3" : "2.2");
            circle.setAttribute("fill", highlightNodeIds.indexOf(node.id) >= 0 ? "var(--bs-success)" : "var(--bs-info)");
            svg.appendChild(circle);

            var text = document.createElementNS("http://www.w3.org/2000/svg", "text");
            text.setAttribute("x", toX(node) + 2.5);
            text.setAttribute("y", toY(node) - 2.5);
            text.setAttribute("font-size", "4");
            text.setAttribute("fill", "currentColor");
            text.textContent = node.id;
            svg.appendChild(text);
        });
    }

    function findNode(id) {
        return nodes.filter(function (node) { return node.id === id; })[0];
    }

    function toX(node) {
        return 8 + ((node.longitude + 117.23) / 0.10) * 84;
    }

    function toY(node) {
        return 92 - ((node.latitude - 34.035) / 0.055) * 84;
    }

    function setBusy(busy) {
        routeBtn.disabled = busy;
        serviceAreaBtn.disabled = busy;
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
