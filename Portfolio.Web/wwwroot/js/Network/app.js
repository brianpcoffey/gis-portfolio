// Route Planner — Redlands, CA  |  Leaflet + /api/v1/network
// Dense real-OpenStreetMap network: real intersections + curve vertices.
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
    var showExplored     = document.getElementById("showExplored"); // optional checkbox

    // ── State ─────────────────────────────────────────────────────────────
    var graph            = null;   // RoadGraphDto
    var nodeById         = {};     // id -> node
    var degree           = {};     // id -> degree (from edges)
    var junctionIds      = [];     // ids of routable junction/endpoint nodes (deg !== 2)
    var originNodeId     = null;
    var nodeMarkers      = {};     // id -> L.CircleMarker (junctions only)
    var routePolyline    = null;
    var serviceAreaLayer = null;
    var exploredLayer    = null;   // L.LayerGroup of settled-node dots
    var lastResult       = null;

    // ── Leaflet map ───────────────────────────────────────────────────────
    // preferCanvas keeps thousands of edges/markers smooth.
    var map = L.map("networkMap", {
        center: [34.0565, -117.1850],
        zoom: 14,
        zoomControl: true,
        preferCanvas: true
    });

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

    // dedicated pane so explored dots sit under the route line and markers
    map.createPane("explored");
    map.getPane("explored").style.zIndex = 380; // between tiles(200) and overlay/markers(400+)

    // ── Bootstrap ────────────────────────────────────────────────────────
    routeBtn.addEventListener("click", findRoute);
    serviceAreaBtn.addEventListener("click", computeServiceArea);

    document.querySelectorAll("input[name='algoRadio']").forEach(function (radio) {
        radio.addEventListener("change", function () {
            algoHint.textContent = radio.value === "astar"
                ? "A* uses a haversine heuristic to Esri HQ — watch it explore far fewer nodes."
                : "Dijkstra explores every node by increasing cost — a full flood outward.";
        });
    });

    startSelect.addEventListener("change", function () {
        var id = parseInt(startSelect.value, 10);
        if (!isNaN(id)) setOrigin(id);
    });

    if (showExplored) {
        showExplored.addEventListener("change", function () {
            if (lastResult) renderExplored(lastResult);
        });
    }

    // Click anywhere on the map → snap to the nearest intersection.
    map.on("click", function (e) {
        var id = nearestJunction(e.latlng.lat, e.latlng.lng);
        if (id != null) setOrigin(id);
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
                indexGraph();
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

    // Build id→node, node degree, and the junction list.
    function indexGraph() {
        nodeById = {}; degree = {}; junctionIds = [];
        graph.nodes.forEach(function (n) { nodeById[n.id] = n; degree[n.id] = 0; });
        graph.edges.forEach(function (e) {
            if (degree[e.fromNodeId] != null) degree[e.fromNodeId]++;
            if (degree[e.toNodeId] != null) degree[e.toNodeId]++;
        });
        graph.nodes.forEach(function (n) {
            if (n.id !== graph.destinationNodeId && degree[n.id] !== 2) junctionIds.push(n.id);
        });
    }

    // A node is a routable junction/endpoint (not a mid-curve shape vertex).
    function isJunction(id) {
        return id === graph.destinationNodeId || degree[id] !== 2;
    }

    // ── Origin select (junctions only) ─────────────────────────────────────
    function populateOriginSelect() {
        startSelect.innerHTML = "";
        var placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = "— click map or choose —";
        startSelect.appendChild(placeholder);

        junctionIds
            .map(function (id) { return nodeById[id]; })
            .sort(function (a, b) { return (a.label || "").localeCompare(b.label || ""); })
            .forEach(function (node) {
                var opt = document.createElement("option");
                opt.value = node.id;
                opt.textContent = node.label || ("Node " + node.id);
                startSelect.appendChild(opt);
            });
    }

    // ── Map rendering ─────────────────────────────────────────────────────
    function renderGraphOnMap() {
        var lats = [], lngs = [];

        // Edges first — these trace the true (curved) street geometry.
        graph.edges.forEach(function (edge) {
            var a = nodeById[edge.fromNodeId];
            var b = nodeById[edge.toNodeId];
            if (!a || !b) return;
            L.polyline([[a.latitude, a.longitude], [b.latitude, b.longitude]], {
                color: "rgba(100,116,139,0.35)",
                weight: 2,
                interactive: false
            }).addTo(map);
        });

        graph.nodes.forEach(function (node) {
            lats.push(node.latitude); lngs.push(node.longitude);
        });

        // Destination marker
        var hq = nodeById[graph.destinationNodeId];
        if (hq) {
            var hqMarker = L.circleMarker([hq.latitude, hq.longitude], {
                radius: 10, fillColor: "#198754", color: "#fff", weight: 2.5, fillOpacity: 0.95
            }).addTo(map);
            hqMarker.bindTooltip("<strong>🏁 " + (hq.label || "Esri HQ") + "</strong>",
                { direction: "top", className: "network-tooltip" });
            nodeMarkers[hq.id] = hqMarker;
        }

        // Junction markers only (curve vertices are drawn by the edges above).
        junctionIds.forEach(function (id) {
            var node = nodeById[id];
            var marker = L.circleMarker([node.latitude, node.longitude], {
                radius: 5, fillColor: "#64748b", color: "#fff", weight: 1.2, fillOpacity: 0.85
            }).addTo(map);
            marker.bindTooltip(node.label || ("Node " + id), { direction: "top", className: "network-tooltip" });
            marker.on("click", function (ev) {
                L.DomEvent.stopPropagation(ev); // don't also trigger map-click snap
                setOrigin(id);
            });
            nodeMarkers[id] = marker;
        });

        if (lats.length > 0) {
            map.fitBounds([[Math.min.apply(null, lats), Math.min.apply(null, lngs)],
                           [Math.max.apply(null, lats), Math.max.apply(null, lngs)]], { padding: [24, 24] });
        }
    }

    // ── Nearest-junction snap ──────────────────────────────────────────────
    function nearestJunction(lat, lng) {
        var best = null, bestD = Infinity;
        for (var i = 0; i < junctionIds.length; i++) {
            var n = nodeById[junctionIds[i]];
            var dLat = n.latitude - lat, dLng = (n.longitude - lng) * Math.cos(lat * Math.PI / 180);
            var d = dLat * dLat + dLng * dLng;
            if (d < bestD) { bestD = d; best = n.id; }
        }
        return best;
    }

    // ── Origin selection ───────────────────────────────────────────────────
    function setOrigin(id) {
        originNodeId = id;
        startSelect.value = id;
        mapHint.classList.add("d-none");

        junctionIds.forEach(function (jid) {
            if (nodeMarkers[jid]) nodeMarkers[jid].setStyle({ fillColor: "#64748b", radius: 5 });
        });
        if (nodeMarkers[id]) nodeMarkers[id].setStyle({ fillColor: "#2563eb", radius: 8 }).bringToFront();

        clearRoute();
    }

    // ── Find route ────────────────────────────────────────────────────────
    function findRoute() {
        if (!graph) return;
        var start = parseInt(startSelect.value, 10);
        if (isNaN(start) || start === graph.destinationNodeId) {
            showAlert("Please pick an origin — click the map or choose an intersection.");
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
                lastResult = result;
                renderExplored(result);
                renderRoute(result);
                updateKpis(result);
                updateTurnByTurn(result);
                setNativeStatus(result.nativeAccelerated);
            })
            .catch(function (err) { showAlert(err.message || "Route calculation failed."); })
            .finally(function () { setBusy(false); });
    }

    // ── Service area ───────────────────────────────────────────────────────
    function computeServiceArea() {
        if (!graph) return;
        var origin = parseInt(startSelect.value, 10);
        if (isNaN(origin)) { showAlert("Please select an origin first."); return; }

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
                kpiExplored.textContent = (result.reachableNodeIds || []).length;
                kpiAlgo.textContent = "area";
                setNativeStatus(result.nativeAccelerated);
            })
            .catch(function (err) { showAlert(err.message || "Service-area calculation failed."); })
            .finally(function () { setBusy(false); });
    }

    // ── Explored-nodes overlay (the A* vs Dijkstra story) ──────────────────
    function renderExplored(result) {
        if (exploredLayer) { map.removeLayer(exploredLayer); exploredLayer = null; }
        if (showExplored && !showExplored.checked) return;
        var ids = result.exploredNodeIds || [];
        if (ids.length === 0) return;

        var color = result.algorithmUsed === "astar" ? "#0d9488" : "#f59e0b";
        var group = L.layerGroup();
        for (var i = 0; i < ids.length; i++) {
            var n = nodeById[ids[i]];
            if (!n) continue;
            L.circleMarker([n.latitude, n.longitude], {
                pane: "explored", radius: 3, stroke: false,
                fillColor: color, fillOpacity: 0.5, interactive: false
            }).addTo(group);
        }
        group.addTo(map);
        exploredLayer = group;
    }

    // ── Route rendering ────────────────────────────────────────────────────
    function renderRoute(result) {
        if (!result.found || !result.path || result.path.length < 2) {
            showAlert("No route found between selected nodes.");
            return;
        }
        var latlngs = result.path.map(function (c) { return [c.y, c.x]; });
        routePolyline = L.polyline(latlngs, {
            color: "#2563eb", weight: 5, opacity: 0.95,
            lineJoin: "round", lineCap: "round", className: "network-route-line"
        }).addTo(map);

        (result.nodeIds || []).forEach(function (id) {
            if (nodeMarkers[id] && id !== graph.destinationNodeId && id !== originNodeId)
                nodeMarkers[id].setStyle({ fillColor: "#2563eb", radius: 6 });
        });

        map.fitBounds(routePolyline.getBounds(), { padding: [50, 50] });
    }

    function renderServiceArea(reachableIds) {
        var idSet = {};
        reachableIds.forEach(function (id) { idSet[id] = true; });
        junctionIds.forEach(function (id) {
            if (!nodeMarkers[id]) return;
            var inArea = idSet[id];
            nodeMarkers[id].setStyle({
                fillColor: inArea ? "#f59e0b" : "#64748b",
                radius: inArea ? 7 : 5,
                fillOpacity: inArea ? 0.95 : 0.5
            });
        });
    }

    function clearRoute() {
        if (routePolyline) { map.removeLayer(routePolyline); routePolyline = null; }
        if (serviceAreaLayer) { map.removeLayer(serviceAreaLayer); serviceAreaLayer = null; }
        if (exploredLayer) { map.removeLayer(exploredLayer); exploredLayer = null; }

        junctionIds.forEach(function (id) {
            if (id === originNodeId) return;
            if (nodeMarkers[id]) nodeMarkers[id].setStyle({ fillColor: "#64748b", radius: 5, fillOpacity: 0.85 });
        });

        routeStepsList.classList.add("d-none");
        routeStepsList.innerHTML = "";
        routeStepsEmpty.style.display = "";
    }

    // ── KPIs and turn-by-turn ──────────────────────────────────────────────
    function updateKpis(result) {
        if (result.distanceKm != null) {
            var mi = (result.distanceKm * 0.621371).toFixed(2);
            kpiDistance.textContent = result.distanceKm.toFixed(2) + " / " + mi + " mi";
        } else {
            kpiDistance.textContent = "—";
        }
        kpiTime.textContent     = result.estimatedMinutes != null ? result.estimatedMinutes.toFixed(1) : "—";
        kpiExplored.textContent = result.exploredNodes != null ? result.exploredNodes : "—";
        kpiAlgo.textContent     = result.algorithmUsed ? result.algorithmUsed.toUpperCase() : "—";
    }

    // Turn-by-turn lists only meaningful junctions (skips mid-curve shape vertices).
    function updateTurnByTurn(result) {
        if (!result.nodeIds || result.nodeIds.length === 0) return;
        routeStepsList.innerHTML = "";
        var ids = result.nodeIds;

        ids.forEach(function (id, idx) {
            var isEnd = idx === 0 || idx === ids.length - 1;
            if (!isEnd && !isJunction(id)) return;   // skip shape vertices
            var node = nodeById[id];
            var label = (node && node.label) ? node.label : ("Node " + id);
            var prefix = idx === 0 ? "🟦 " : idx === ids.length - 1 ? "🏁 " : "";
            var li = document.createElement("li");
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

    // ── Helpers ────────────────────────────────────────────────────────────
    function setBusy(busy) {
        routeBtn.disabled       = busy;
        serviceAreaBtn.disabled = busy;
        loadingSpinner.classList.toggle("d-none", !busy);
    }
    function showAlert(msg) { alertBox.textContent = msg; alertBox.classList.remove("d-none"); }
    function hideAlert() { alertBox.classList.add("d-none"); alertBox.textContent = ""; }

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
