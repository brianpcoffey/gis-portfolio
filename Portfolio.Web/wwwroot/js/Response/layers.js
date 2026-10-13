// layers.js — Emergency Response Coverage Optimizer: Leaflet map layers.
// Demand circles, assignment lines, station markers, the isochrone band layer and the map fit,
// plus the green -> amber -> red colour ramp they share. Publishes its entry points on
// window.ResponsePage (see context.js).
(function () {
    "use strict";

    var R = window.ResponsePage;
    var charts = window.PortfolioCharts;
    var fmt = window.PortfolioFormat;
    var el = window.PortfolioDom.el;

    var map = R.map;
    var state = R.state;
    var ISOCHRONE_BANDS = R.ISOCHRONE_BANDS;
    var showIsochrone = R.dom.showIsochrone;
    var isochroneBtn = R.dom.isochroneBtn;

    // Response-time ramp used for the demand circles, in minutes.
    var RAMP_MAX_MINUTES = 12;

    // The OpenStreetMap raster tiles stay light in both themes, so the neutral marker colour
    // drawn over them is fixed; every coloured series uses the (theme-independent) status fills.
    var SLATE = "#64748b";

    function drawDemand(assignments) {
        if (state.demandLayer) { map.removeLayer(state.demandLayer); state.demandLayer = null; }
        if (!state.scenario) { return; }

        var minutesById = {};
        if (assignments) {
            assignments.forEach(function (a) { minutesById[a.demandPointId] = a.responseMinutes; });
        }

        var maxVolume = 1;
        state.scenario.demandPoints.forEach(function (d) {
            if (d.callVolume > maxVolume) { maxVolume = d.callVolume; }
        });

        var ramp = responseRamp();
        var group = L.layerGroup();
        state.scenario.demandPoints.forEach(function (point) {
            var minutes = minutesById[point.id];
            var radius = 2.5 + Math.sqrt(point.callVolume / maxVolume) * 7;
            var marker = L.circleMarker([point.latitude, point.longitude], {
                radius: radius,
                stroke: false,
                fillColor: minutes == null ? SLATE : responseColor(minutes, ramp),
                fillOpacity: minutes == null ? 0.45 : 0.8
            });
            marker.bindTooltip(point.callVolume + " calls/yr" +
                (minutes == null ? "" : " · " + fmt.unit(minutes, "min")),
                { direction: "top" });
            marker.addTo(group);
        });

        group.addTo(map);
        state.demandLayer = group;
    }

    function drawAssignments(assignments) {
        if (state.assignmentLayer) { map.removeLayer(state.assignmentLayer); state.assignmentLayer = null; }
        if (!assignments) { return; }

        // A faint line from each demand point to its first-due station. The starburst
        // makes the districting legible without drawing any polygon boundary.
        var group = L.layerGroup();
        assignments.forEach(function (a) {
            var demand = state.demandById[a.demandPointId];
            var station = state.candidateById[a.assignedCandidateId];
            if (!demand || !station) { return; }
            L.polyline([[demand.latitude, demand.longitude], [station.latitude, station.longitude]], {
                color: SLATE,
                opacity: 0.35,
                weight: 1,
                interactive: false
            }).addTo(group);
        });

        group.addTo(map);
        state.assignmentLayer = group;
    }

    function drawStations(chosenIds) {
        Object.keys(state.stationMarkers).forEach(function (id) { map.removeLayer(state.stationMarkers[id]); });
        state.stationMarkers = {};
        if (!state.scenario) { return; }

        var chosen = {};
        (chosenIds || []).forEach(function (id) { chosen[id] = true; });
        var chosenFill = charts.status("info");
        var outline = charts.token("on-dark");

        state.scenario.candidates.forEach(function (candidate) {
            var isChosen = !!chosen[candidate.id];
            var marker = L.circleMarker([candidate.latitude, candidate.longitude], {
                radius: isChosen ? 10 : candidate.isExisting ? 7 : 4,
                fillColor: isChosen ? chosenFill : candidate.isExisting ? SLATE : "transparent",
                fillOpacity: isChosen ? 0.95 : candidate.isExisting ? 0.85 : 0,
                color: isChosen || candidate.isExisting ? outline : SLATE,
                weight: isChosen ? 2.5 : 1.2
            }).addTo(map);

            var tip = el("span", {}, el("strong", { text: candidate.label }));
            if (candidate.isExisting) { tip.append(document.createElement("br"), "In service today"); }
            if (isChosen) { tip.append(document.createElement("br"), "Selected by the optimizer"); }
            marker.bindTooltip(tip, { direction: "top" });

            marker.on("click", function () {
                if (showIsochrone.checked) { R.requestIsochrone(candidate, isochroneBtn); }
            });

            state.stationMarkers[candidate.id] = marker;
        });
    }

    function drawIsochrone(result, candidate) {
        clearIsochrone();

        var ramp = responseRamp();
        var group = L.layerGroup();
        result.nodes.forEach(function (node) {
            L.circleMarker([node.latitude, node.longitude], {
                pane: "isochrone",
                radius: 3,
                stroke: false,
                fillColor: bandColor(Math.min(node.bandIndex, ISOCHRONE_BANDS.length), ramp),
                fillOpacity: 0.55,
                interactive: false
            }).addTo(group);
        });

        group.addTo(map);
        state.isochroneLayer = group;

        var legend = ISOCHRONE_BANDS.map(function (band, i) {
            return "≤" + band + " min: " + result.bandCounts[i];
        }).join(" · ");
        R.setComparison([
            el("strong", { text: "Isochrone from " + candidate.label + ":" }),
            " " + legend + " · beyond " + ISOCHRONE_BANDS[ISOCHRONE_BANDS.length - 1] + " min: " +
                result.bandCounts[ISOCHRONE_BANDS.length] + " · unreachable: " + result.unreachableNodes
        ]);
    }

    function clearIsochrone() {
        if (state.isochroneLayer) { map.removeLayer(state.isochroneLayer); state.isochroneLayer = null; }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // Green -> amber -> red from the status fills (resolved once per draw).
    function responseRamp() {
        return { low: charts.status("success"), mid: charts.status("warning"), high: charts.status("danger") };
    }

    function responseColor(minutes, ramp) {
        var t = Math.max(0, Math.min(1, minutes / RAMP_MAX_MINUTES));
        if (t < 0.5) {
            return charts.mix(ramp.low, ramp.mid, t / 0.5);
        }
        return charts.mix(ramp.mid, ramp.high, (t - 0.5) / 0.5);
    }

    // Isochrone band i of n bands (plus one "beyond" band) along the same ramp.
    function bandColor(index, ramp) {
        return responseColor(index / ISOCHRONE_BANDS.length * RAMP_MAX_MINUTES, ramp);
    }

    function fitToScenario() {
        var lats = [], lngs = [];
        state.scenario.demandPoints.forEach(function (d) { lats.push(d.latitude); lngs.push(d.longitude); });
        state.scenario.candidates.forEach(function (c) { lats.push(c.latitude); lngs.push(c.longitude); });
        if (lats.length === 0) { return; }
        map.fitBounds([[Math.min.apply(null, lats), Math.min.apply(null, lngs)],
                       [Math.max.apply(null, lats), Math.max.apply(null, lngs)]], { padding: [24, 24] });
    }

    R.drawDemand = drawDemand;
    R.drawAssignments = drawAssignments;
    R.drawStations = drawStations;
    R.drawIsochrone = drawIsochrone;
    R.clearIsochrone = clearIsochrone;
    R.fitToScenario = fitToScenario;
}());
