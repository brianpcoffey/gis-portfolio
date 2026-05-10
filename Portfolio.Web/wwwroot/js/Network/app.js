// Route Planner — Redlands, CA  |  Leaflet + /api/v1/network
(function () {
    "use strict";

    // ── DOM refs ──────────────────────────────────────────────────────────
    var startSelect      = document.getElementById("networkStart");
    var routeBtn         = document.getElementById("networkRouteBtn");
    var serviceAreaBtn   = document.getElementById("networkServiceAreaBtn");
    var maxCostInput     = document.getElementById("networkMaxCost");
    var alertBox         = document.getElementById("networkAlert");
    var kpiDistance      = document.getElementById("kpiDistance");
    var kpiTime          = document.getElementById("kpiTime");
    var kpiExplored      = document.getElementById("kpiExplored");
    var kpiAlgo          = document.getElementById("kpiAlgo");
    var routeStepsList   = document.getElementById("routeSteps");
    var routeStepsEmpty  = document.getElementById("routeStepsEmpty");
    var nativeStatus     = document.getElementById("nativeStatus");
    var loadingSpinner   = document.getElementById("loadingSpinner");
    var mapHint          = document.getElementById("mapOverlayHint");
    var algoHint         = document.getElementById("algoHint");

    // ── State ─────────────────────────────────────────────────────────────
    var graph            = null;   // RoadGraphDto
    var originNodeId     = null;
    var nodeMarkers      = {};     // id -> L.CircleMarker
    var routePolyline    = null;
    var serviceAreaLayer = null;

    // ── Leaflet map ───────────────────────────────────────────────────────
    var map = L.map("networkMap", {
        center: [34.0550, -117.1820],
        zoom: 14,
        zoomControl: true
    });

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

    // ── Bootstrap ────────────────────────────────────────────────────────
    routeBtn.addEventListener("click", findRoute);
    serviceAreaBtn.addEventListener("click", computeServiceArea);

    document.querySelectorAll("input[name='algoRadio']").forEach(function (radio) {
        radio.addEventListener("change", function () {
            algoHint.textContent = radio.value === "astar"
                ? "A* uses a haversine heuristic to Esri HQ."
                : "Dijkstra explores all reachable nodes by cost.";
        });
    });

    startSelect.addEventListener("change", function () {
        var id = parseInt(startSelect.value, 10);
        if (!isNaN(id)) setOrigin(id);
    });

    fetchGraph();

    // ── Graph fetch ───────────────────────────────────────────────────────
    function fetchGraph() {
        fetch(window.PortfolioApi.routes.spatialCompute.network.graph)
            .then(function (r) {
                if (!r.ok) throw new Error("Graph load failed (" + r.status + ")");
                return r.json();
            })
            .then(function (data) {
                graph = data;
                populateOriginSelect();
                renderGraphOnMap();
                routeBtn.disabled = false;
                serviceAreaBtn.disabled = false;
                mapHint.classList.remove("d-none");
            })
            .catch(function (err) {
                showAlert("Could not load road graph: " + err.message);
            });
    }

    // ── Origin select ─────────────────────────────────────────────────────
    function populateOriginSelect() {
        startSelect.innerHTML = "";
        var placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = "— click map or choose —";
        startSelect.appendChild(placeholder);

        graph.nodes.forEach(function (node) {
            if (node.id === graph.destinationNodeId) return;
            var opt = document.createElement("option");
            opt.value = node.id;
            opt.textContent = node.label || ("Node " + node.id);
            startSelect.appendChild(opt);
        });
    }

    // ── Map rendering ─────────────────────────────────────────────────────
    function renderGraphOnMap() {
        var lats = [], lngs = [];

        // Draw edges first (polylines)
        graph.edges.forEach(function (edge) {
            var a = findNode(edge.fromNodeId);
            var b = findNode(edge.toNodeId);
            if (!a || !b) return;
            L.polyline([[a.latitude, a.longitude], [b.latitude, b.longitude]], {
                color: "rgba(100,116,139,0.4)",
                weight: 2,
                interactive: false
            }).addTo(map);
        });

        // Draw nodes
        graph.nodes.forEach(function (node) {
            lats.push(node.latitude);
            lngs.push(node.longitude);
            var isDestination = node.id === graph.destinationNodeId;
            var marker;

            if (isDestination) {
                marker = L.circleMarker([node.latitude, node.longitude], {
                    radius: 10,
                    fillColor: "#198754",
                    color: "#fff",
                    weight: 2.5,
                    fillOpacity: 0.95
                }).addTo(map);

                marker.bindTooltip(
                    "<strong>🏁 " + (node.label || "Esri HQ") + "</strong>",
                    { permanent: false, direction: "top", className: "network-tooltip" }
                );
            } else {
                marker = L.circleMarker([node.latitude, node.longitude], {
                    radius: 6,
                    fillColor: "#64748b",
                    color: "#fff",
                    weight: 1.5,
                    fillOpacity: 0.8
                }).addTo(map);

                marker.bindTooltip(node.label || ("Node " + node.id), {
                    direction: "top",
                    className: "network-tooltip"
                });

                (function (n) {
                    marker.on("click", function () { setOrigin(n.id); });
                }(node));
            }

            nodeMarkers[node.id] = marker;
        });

        // Fit the map to the full extent of the road network.
        if (lats.length > 0) {
            var south = Math.min.apply(null, lats);
            var north = Math.max.apply(null, lats);
            var west  = Math.min.apply(null, lngs);
            var east  = Math.max.apply(null, lngs);
            map.fitBounds([[south, west], [north, east]], { padding: [32, 32] });
        }
    }

    // ── Origin selection ─────────────────────────────────────────────────
    function setOrigin(id) {
        originNodeId = id;
        startSelect.value = id;
        mapHint.classList.add("d-none");

        // Reset previous origin style
        graph.nodes.forEach(function (node) {
            if (node.id === graph.destinationNodeId) return;
            nodeMarkers[node.id].setStyle({
                fillColor: "#64748b",
                radius: 6
            });
        });

        // Highlight selected origin
        if (nodeMarkers[id]) {
            nodeMarkers[id].setStyle({
                fillColor: "#2563eb",
                radius: 9
            });
        }

        clearRoute();
    }

    // ── Find route ────────────────────────────────────────────────────────
    function findRoute() {
        if (!graph) return;
        var start = parseInt(startSelect.value, 10);
        if (isNaN(start) || start === graph.destinationNodeId) {
            showAlert("Please select a valid origin.");
            return;
        }
        originNodeId = start;

        var algo = document.querySelector("input[name='algoRadio']:checked").value;

        var request = {
            nodes: graph.nodes,
            edges: graph.edges,
            startNodeId: start,
            endNodeId: graph.destinationNodeId,
            algorithm: algo
        };

        hideAlert();
        setBusy(true);
        clearRoute();

        apiPost(window.PortfolioApi.routes.spatialCompute.network.route, request)
            .then(function (result) {
                renderRoute(result);
                updateKpis(result);
                updateTurnByTurn(result);
                setNativeStatus(result.nativeAccelerated);
            })
            .catch(function (err) {
                showAlert(err.message || "Route calculation failed.");
            })
            .finally(function () {
                setBusy(false);
            });
    }

    // ── Compute service area ─────────────────────────────────────────────
    function computeServiceArea() {
        if (!graph) return;
        var origin = parseInt(startSelect.value, 10);
        if (isNaN(origin)) {
            showAlert("Please select an origin first.");
            return;
        }

        var request = {
            nodes: graph.nodes,
            edges: graph.edges,
            originNodeId: origin,
            maxCost: parseFloat(maxCostInput.value) || 1.5
        };

        hideAlert();
        setBusy(true);
        clearRoute();

        apiPost(window.PortfolioApi.routes.spatialCompute.network.serviceArea, request)
            .then(function (result) {
                renderServiceArea(result.reachableNodeIds || []);
                kpiExplored.textContent = result.reachableNodeIds.length;
                kpiAlgo.textContent = "area";
                setNativeStatus(result.nativeAccelerated);
            })
            .catch(function (err) {
                showAlert(err.message || "Service-area calculation failed.");
            })
            .finally(function () {
                setBusy(false);
            });
    }

    // ── Route rendering ───────────────────────────────────────────────────
    function renderRoute(result) {
        if (!result.found || !result.path || result.path.length < 2) {
            showAlert("No route found between selected nodes.");
            return;
        }

        var latlngs = result.path.map(function (c) { return [c.y, c.x]; });
        routePolyline = L.polyline(latlngs, {
            color: "#2563eb",
            weight: 5,
            opacity: 0.9,
            lineJoin: "round",
            lineCap: "round",
            className: "network-route-line"
        }).addTo(map);

        // Highlight route nodes
        result.nodeIds.forEach(function (id) {
            if (nodeMarkers[id] && id !== graph.destinationNodeId && id !== originNodeId) {
                nodeMarkers[id].setStyle({ fillColor: "#2563eb", radius: 7 });
            }
        });

        map.fitBounds(routePolyline.getBounds(), { padding: [40, 40] });
    }

    function renderServiceArea(reachableIds) {
        var idSet = {};
        reachableIds.forEach(function (id) { idSet[id] = true; });

        graph.nodes.forEach(function (node) {
            if (node.id === graph.destinationNodeId) return;
            var inArea = idSet[node.id];
            nodeMarkers[node.id].setStyle({
                fillColor: inArea ? "#f59e0b" : "#64748b",
                radius: inArea ? 8 : 5,
                fillOpacity: inArea ? 0.9 : 0.5
            });
        });
    }

    function clearRoute() {
        if (routePolyline) { map.removeLayer(routePolyline); routePolyline = null; }
        if (serviceAreaLayer) { map.removeLayer(serviceAreaLayer); serviceAreaLayer = null; }

        // Reset all non-origin, non-destination nodes
        if (graph) {
            graph.nodes.forEach(function (node) {
                if (node.id === graph.destinationNodeId) return;
                if (node.id === originNodeId) return;
                nodeMarkers[node.id].setStyle({ fillColor: "#64748b", radius: 6, fillOpacity: 0.8 });
            });
        }

        routeStepsList.classList.add("d-none");
        routeStepsList.innerHTML = "";
        routeStepsEmpty.style.display = "";
    }

    // ── KPIs and turn-by-turn ─────────────────────────────────────────────
    function updateKpis(result) {
        kpiDistance.textContent = result.distanceKm != null ? result.distanceKm.toFixed(2) : "—";
        kpiTime.textContent     = result.estimatedMinutes != null ? result.estimatedMinutes.toFixed(1) : "—";
        kpiExplored.textContent = result.exploredNodes != null ? result.exploredNodes : "—";
        kpiAlgo.textContent     = result.algorithmUsed ? result.algorithmUsed.toUpperCase() : "—";
    }

    function updateTurnByTurn(result) {
        if (!result.nodeIds || result.nodeIds.length === 0) return;
        routeStepsList.innerHTML = "";

        result.nodeIds.forEach(function (id, idx) {
            var node = findNode(id);
            var li = document.createElement("li");
            var label = (node && node.label) ? node.label : ("Node " + id);
            var prefix = idx === 0
                ? "🟦 "
                : idx === result.nodeIds.length - 1
                    ? "🏁 "
                    : "";
            li.textContent = prefix + label;
            routeStepsList.appendChild(li);
        });

        routeStepsList.classList.remove("d-none");
        routeStepsEmpty.style.display = "none";
    }

    function setNativeStatus(native) {
        nativeStatus.textContent = native ? "Native: ON" : "Native: OFF";
        nativeStatus.className   = native ? "badge bg-success" : "badge bg-secondary";
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    function findNode(id) {
        if (!graph) return null;
        return graph.nodes.filter(function (n) { return n.id === id; })[0] || null;
    }

    function setBusy(busy) {
        routeBtn.disabled      = busy;
        serviceAreaBtn.disabled = busy;
        loadingSpinner.classList.toggle("d-none", !busy);
    }

    function showAlert(msg) {
        alertBox.textContent = msg;
        alertBox.classList.remove("d-none");
    }

    function hideAlert() {
        alertBox.classList.add("d-none");
        alertBox.textContent = "";
    }

    function apiPost(url, body) {
        return fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body)
        }).then(function (r) {
            if (!r.ok) return r.json().then(function (e) { throw new Error(e.error || r.statusText); });
            return r.json();
        });
    }
}());

