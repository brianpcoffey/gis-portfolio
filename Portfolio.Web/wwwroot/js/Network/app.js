// Route Planner — Redlands, CA  |  Leaflet + /api/v1/network
// Dense real-OpenStreetMap network: real intersections + curve vertices.
// Uses the shared modules: api-fetch (apiGet/apiPost), PortfolioMap, PortfolioUi, PortfolioFormat, PortfolioCharts.
(function () {
    "use strict";

    var Fmt = window.PortfolioFormat;
    var Charts = window.PortfolioCharts;

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
    var routeStepsRegion = document.getElementById("routeStepsRegion");
    var routeStepsEmpty  = document.getElementById("routeStepsEmpty");
    var nativeStatus     = document.getElementById("nativeStatus");
    var loadingSpinner   = document.getElementById("loadingSpinner");
    var mapHint          = document.getElementById("mapOverlayHint");
    var algoHint         = document.getElementById("algoHint");
    var showExplored     = document.getElementById("showExplored"); // optional checkbox
    var alertUi          = PortfolioUi.inlineAlert(alertBox);

    var graph            = null;   // RoadGraphDto
    var nodeById         = {};     // id -> node
    var degree           = {};     // id -> degree (from edges)
    var junctionIds      = [];     // ids of routable junction/endpoint nodes (deg !== 2)
    var originNodeId     = null;
    var nodeMarkers      = {};     // id -> L.CircleMarker (junctions only)
    var edgeGroup        = null;   // L.FeatureGroup of the street polylines
    var routePolyline    = null;
    var serviceAreaLayer = null;
    var exploredLayer    = null;   // L.LayerGroup of settled-node dots
    var lastResult       = null;
    var C                = readColors();

    // Canvas/Leaflet need concrete colours, so resolve the tokens once. Every mark sits over the
    // OpenStreetMap tiles, which stay light in both themes, so they use the theme-independent
    // map tokens (--map-series-N, --map-muted), the white --on-dark halo and the status fills:
    // the colours never change with the theme and nothing needs restyling when it flips.
    function readColors() {
        var pal = Charts.mapPalette(8);
        return {
            node:     Charts.token("map-muted"),
            outline:  Charts.token("on-dark"),
            origin:   pal[0],                    // --map-series-1
            route:    pal[0],
            astar:    pal[4],                    // --map-series-5
            dijkstra: Charts.status("warning"),
            area:     Charts.status("warning"),
            dest:     Charts.status("success")
        };
    }

    // PortfolioMap adds the OSM tile layer + attribution; preferCanvas keeps thousands of edges smooth.
    var map = PortfolioMap.create("networkMap", { center: [34.0565, -117.1850], zoom: 14, label: "Route map" });

    // dedicated pane so explored dots sit under the route line and markers
    map.createPane("explored");
    map.getPane("explored").style.zIndex = 380; // between tiles(200) and overlay/markers(400+)

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

    map.on("click", function (e) {
        var id = nearestJunction(e.latlng.lat, e.latlng.lng);
        if (id != null) setOrigin(id);
    });

    fetchGraph();

    function fetchGraph() {
        apiGet(window.PortfolioApi.routes.spatialCompute.network.graph)
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
                alertUi.show("Could not load road graph: " + err.message);
            });
    }

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

    function renderGraphOnMap() {
        var lats = [], lngs = [];

        // Edges first — these trace the true (curved) street geometry.
        edgeGroup = L.featureGroup().addTo(map);
        graph.edges.forEach(function (edge) {
            var a = nodeById[edge.fromNodeId];
            var b = nodeById[edge.toNodeId];
            if (!a || !b) return;
            L.polyline([[a.latitude, a.longitude], [b.latitude, b.longitude]], {
                color: C.node,
                opacity: 0.35,
                weight: 2,
                interactive: false
            }).addTo(edgeGroup);
        });

        graph.nodes.forEach(function (node) {
            lats.push(node.latitude); lngs.push(node.longitude);
        });

        var hq = nodeById[graph.destinationNodeId];
        if (hq) {
            var hqMarker = L.circleMarker([hq.latitude, hq.longitude], {
                radius: 10, fillColor: C.dest, color: C.outline, weight: 2.5, fillOpacity: 0.95
            }).addTo(map);
            hqMarker.bindTooltip(PortfolioDom.el("strong", { text: "🏁 " + (hq.label || "Esri HQ") }),
                { direction: "top", className: "network-tooltip" });
            nodeMarkers[hq.id] = hqMarker;
        }

        // Junction markers only (curve vertices are drawn by the edges above).
        junctionIds.forEach(function (id) {
            var node = nodeById[id];
            var marker = L.circleMarker([node.latitude, node.longitude], {
                radius: 5, fillColor: C.node, color: C.outline, weight: 1.2, fillOpacity: 0.85
            }).addTo(map);
            marker.bindTooltip(PortfolioDom.el("span", { text: node.label || ("Node " + id) }), { direction: "top", className: "network-tooltip" });
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

    function setOrigin(id) {
        originNodeId = id;
        startSelect.value = id;
        mapHint.classList.add("d-none");

        junctionIds.forEach(function (jid) {
            if (nodeMarkers[jid]) nodeMarkers[jid].setStyle({ fillColor: C.node, radius: 5 });
        });
        if (nodeMarkers[id]) nodeMarkers[id].setStyle({ fillColor: C.origin, radius: 8 }).bringToFront();

        clearRoute();
    }

    // runAction keeps focus on the trigger (aria-disabled, not disabled), shows errors in the inline
    // alert and announces results. The other button is guarded for the duration so runs can't overlap.
    function run(trigger, other, request, render, summary) {
        var doneOther = PortfolioUi.busy({ buttons: [other] });
        loadingSpinner.classList.remove("d-none");
        return PortfolioUi.runAction({
            trigger: trigger,
            alert: alertUi,
            region: document.querySelector(".network-panel"),
            request: request,
            render: render,
            summary: summary
        }).then(function () {
            doneOther();
            loadingSpinner.classList.add("d-none");
        });
    }

    function findRoute() {
        if (!graph) return;
        var start = parseInt(startSelect.value, 10);
        if (isNaN(start) || start === graph.destinationNodeId) {
            alertUi.show("Please pick an origin — click the map or choose an intersection.");   // role="alert": speaks itself
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

        clearRoute();

        run(routeBtn, serviceAreaBtn,
            function () { return apiPost(window.PortfolioApi.routes.spatialCompute.network.route, request); },
            function (result) {
                lastResult = result;
                renderExplored(result);
                renderRoute(result);
                updateKpis(result);
                updateTurnByTurn(result);
                setNativeStatus(result.nativeAccelerated);
            },
            function (result) {
                // No route: the inline alert (role="alert") shown by renderRoute already says so; announcing it too would speak it twice.
                if (!hasRoute(result)) return "";
                return "Route found: " + Fmt.unit(result.distanceKm, "km") + ", " + Fmt.unit(result.estimatedMinutes, "min") +
                    ", " + algoLabel(result.algorithmUsed) + " explored " + Fmt.number(result.exploredNodes) + " nodes.";
            });
    }

    function computeServiceArea() {
        if (!graph) return;
        var origin = parseInt(startSelect.value, 10);
        if (isNaN(origin)) {
            alertUi.show("Please select an origin first.");   // role="alert": speaks itself
            return;
        }

        var request = {
            nodes: graph.nodes,
            edges: graph.edges,
            originNodeId: origin,
            maxCost: parseFloat(maxCostInput.value) || 1.5
        };

        clearRoute();

        run(serviceAreaBtn, routeBtn,
            function () { return apiPost(window.PortfolioApi.routes.spatialCompute.network.serviceArea, request); },
            function (result) {
                var ids = result.reachableNodeIds || [];
                renderServiceArea(ids);
                kpiExplored.textContent = Fmt.number(ids.length);
                kpiAlgo.textContent = "Area";
                setNativeStatus(result.nativeAccelerated);
            },
            function (result) {
                return "Service area: " + Fmt.number((result.reachableNodeIds || []).length) + " intersections reachable.";
            });
    }

    function renderExplored(result) {
        if (exploredLayer) { map.removeLayer(exploredLayer); exploredLayer = null; }
        if (showExplored && !showExplored.checked) return;
        var ids = result.exploredNodeIds || [];
        if (ids.length === 0) return;

        var color = result.algorithmUsed === "astar" ? C.astar : C.dijkstra;
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

    function hasRoute(result) {
        return !!(result.found && result.path && result.path.length >= 2);
    }

    function renderRoute(result) {
        if (!hasRoute(result)) {
            alertUi.show("No route found between selected nodes.");
            return;
        }
        var latlngs = result.path.map(function (c) { return [c.y, c.x]; });
        routePolyline = L.polyline(latlngs, {
            color: C.route, weight: 5, opacity: 0.95,
            lineJoin: "round", lineCap: "round", className: "network-route-line"
        }).addTo(map);

        highlightRouteNodes(result);

        map.fitBounds(routePolyline.getBounds(), { padding: [50, 50] });
    }

    function highlightRouteNodes(result) {
        (result.nodeIds || []).forEach(function (id) {
            if (nodeMarkers[id] && id !== graph.destinationNodeId && id !== originNodeId)
                nodeMarkers[id].setStyle({ fillColor: C.route, radius: 6 });
        });
    }

    function renderServiceArea(reachableIds) {
        var idSet = {};
        reachableIds.forEach(function (id) { idSet[id] = true; });
        junctionIds.forEach(function (id) {
            if (!nodeMarkers[id]) return;
            var inArea = idSet[id];
            nodeMarkers[id].setStyle({
                fillColor: inArea ? C.area : C.node,
                radius: inArea ? 7 : 5,
                fillOpacity: inArea ? 0.95 : 0.5
            });
        });
    }

    function clearRoute() {
        if (routePolyline) { map.removeLayer(routePolyline); routePolyline = null; }
        if (serviceAreaLayer) { map.removeLayer(serviceAreaLayer); serviceAreaLayer = null; }
        if (exploredLayer) { map.removeLayer(exploredLayer); exploredLayer = null; }
        lastResult = null;

        junctionIds.forEach(function (id) {
            if (id === originNodeId) return;
            if (nodeMarkers[id]) nodeMarkers[id].setStyle({ fillColor: C.node, radius: 5, fillOpacity: 0.85 });
        });

        routeStepsRegion.classList.add("d-none");
        routeStepsList.replaceChildren();
        routeStepsEmpty.style.display = "";
    }

    function algoLabel(algo) {
        if (algo === "astar") return "A*";
        if (algo === "dijkstra") return "Dijkstra";
        return algo || "—";
    }

    function updateKpis(result) {
        if (result.distanceKm != null) {
            var mi = result.distanceKm * 0.621371;
            kpiDistance.textContent = Fmt.number(result.distanceKm, { decimals: 1 }) + " / " + Fmt.number(mi, { decimals: 1 }) + " mi";
        } else {
            kpiDistance.textContent = "—";
        }
        kpiTime.textContent     = result.estimatedMinutes != null ? Fmt.number(result.estimatedMinutes, { decimals: 1 }) : "—";
        kpiExplored.textContent = result.exploredNodes != null ? Fmt.number(result.exploredNodes) : "—";
        kpiAlgo.textContent     = result.algorithmUsed ? algoLabel(result.algorithmUsed) : "—";
    }

    // Turn-by-turn lists only meaningful junctions (skips mid-curve shape vertices).
    function updateTurnByTurn(result) {
        if (!result.nodeIds || result.nodeIds.length === 0) return;
        routeStepsList.replaceChildren();
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

        routeStepsRegion.classList.remove("d-none");
        routeStepsEmpty.style.display = "none";
    }

    function setNativeStatus(native) {
        nativeStatus.textContent = "Native: " + Fmt.yesNo(native);
        nativeStatus.className   = native ? "badge bg-success" : "badge bg-secondary";
    }
}());
