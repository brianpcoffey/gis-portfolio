// Live Location Stream Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var fmt = window.PortfolioFormat;

    var sampleEvents = [
        { entityId: 101, timestampUtc: new Date().toISOString(), latitude: 34.054, longitude: -117.182, speedMetersPerSecond: 12, headingDegrees: 90 },
        { entityId: 102, timestampUtc: new Date().toISOString(), latitude: 34.057, longitude: -117.185, speedMetersPerSecond: 35, headingDegrees: 105 },
        { entityId: 103, timestampUtc: new Date().toISOString(), latitude: 34.062, longitude: -117.193, speedMetersPerSecond: 18, headingDegrees: 120 },
        { entityId: 104, timestampUtc: new Date().toISOString(), latitude: 34.071, longitude: -117.204, speedMetersPerSecond: 42, headingDegrees: 88 },
        { entityId: 105, timestampUtc: new Date().toISOString(), latitude: 34.049, longitude: -117.174, speedMetersPerSecond: 9, headingDegrees: 270 }
    ];

    var gridSize = document.getElementById("geoGridSize");
    var anomalySpeed = document.getElementById("geoAnomalySpeed");
    var sampleJson = document.getElementById("geoSampleJson");
    var runBtn = document.getElementById("geoRunBtn");
    var alertBox = PortfolioUi.inlineAlert(document.getElementById("geoStreamAlert"));
    var stage = document.getElementById("geoStage");
    var aggregateList = document.getElementById("geoAggregateList");
    var resultsPanel = stage.closest(".card");
    var kpiTotal = document.getElementById("geoKpiTotal");
    var kpiValid = document.getElementById("geoKpiValid");
    var kpiAnomalies = document.getElementById("geoKpiAnomalies");
    var kpiNative = document.getElementById("geoKpiNative");

    sampleJson.value = JSON.stringify(sampleEvents, null, 2);
    runBtn.addEventListener("click", processTelemetry);

    function processTelemetry() {
        alertBox.hide();
        var events;
        try {
            events = JSON.parse(sampleJson.value);
        } catch (err) {
            alertBox.show("Sample events must be valid JSON.");
            return;
        }

        var request = {
            gridSizeDegrees: Number(gridSize.value),
            anomalySpeedThresholdMetersPerSecond: Number(anomalySpeed.value),
            events: events
        };

        PortfolioUi.runAction({
            trigger: runBtn,
            alert: alertBox,
            region: resultsPanel,
            request: function () { return apiPost(window.PortfolioApi.routes.spatialCompute.geostream.events, request); },
            render: renderResult,
            summary: function (result) {
                return "Telemetry processed: " + fmt.number(result.validEvents) + " valid events in " +
                    fmt.number(result.aggregates.length) + " cells, " + fmt.number(result.anomalyCount) + " anomalies.";
            }
        });
    }

    function renderResult(result) {
        kpiTotal.textContent = fmt.number(result.totalEvents);
        kpiValid.textContent = fmt.number(result.validEvents);
        kpiAnomalies.textContent = fmt.number(result.anomalyCount);
        kpiNative.textContent = fmt.yesNo(result.nativeAccelerated);

        stage.replaceChildren();
        aggregateList.replaceChildren();

        result.aggregates.forEach(function (aggregate) {
            var avg = fmt.unit(aggregate.averageSpeedMetersPerSecond, "m/s");
            var point = PortfolioDom.el("div", {
                class: "spatial-point" + (aggregate.anomalyCount > 0 ? " anomaly" : ""),
                title: aggregate.count + " events, avg " + avg
            });
            point.style.left = normalize(aggregate.centerLongitude, -117.22, -117.16) + "%";
            point.style.top = (100 - normalize(aggregate.centerLatitude, 34.04, 34.08)) + "%";
            stage.appendChild(point);

            aggregateList.appendChild(PortfolioDom.el("div",
                { class: "geo-cell-item" + (aggregate.anomalyCount > 0 ? " geo-cell-anomaly" : "") },
                PortfolioDom.el("strong", { text: "Cell " + aggregate.cellX + ", " + aggregate.cellY }),
                PortfolioDom.el("div", {
                    class: "geo-cell-meta",
                    text: aggregate.count + " events · avg " + avg + " · max " +
                        fmt.unit(aggregate.maxSpeedMetersPerSecond, "m/s") + " · anomalies " + aggregate.anomalyCount
                })));
        });

        // result.anomalyCount counts anomaly events, not cells, so count the flagged cells for "with anomalies".
        var anomalyCells = result.aggregates.filter(function (a) { return a.anomalyCount > 0; }).length;
        stage.setAttribute("aria-label", "Telemetry aggregate map: " + result.aggregates.length + " cells, " +
            anomalyCells + " with anomalies");

        if (window.showToast) {
            window.showToast("Telemetry batch processed.", "success");
        }
    }

    function normalize(value, min, max) {
        return Math.max(5, Math.min(95, ((value - min) / (max - min)) * 100));
    }
}());
