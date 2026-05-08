// Live Location Stream Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

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
    var alertBox = document.getElementById("geoStreamAlert");
    var stage = document.getElementById("geoStage");
    var aggregateList = document.getElementById("geoAggregateList");
    var kpiTotal = document.getElementById("geoKpiTotal");
    var kpiValid = document.getElementById("geoKpiValid");
    var kpiAnomalies = document.getElementById("geoKpiAnomalies");
    var kpiNative = document.getElementById("geoKpiNative");

    sampleJson.value = JSON.stringify(sampleEvents, null, 2);
    runBtn.addEventListener("click", processTelemetry);

    function processTelemetry() {
        hideAlert();
        var events;
        try {
            events = JSON.parse(sampleJson.value);
        } catch (err) {
            showAlert("Sample events must be valid JSON.");
            return;
        }

        var request = {
            gridSizeDegrees: Number(gridSize.value),
            anomalySpeedThresholdMetersPerSecond: Number(anomalySpeed.value),
            events: events
        };

        runBtn.disabled = true;
        apiPost(window.PortfolioApi.routes.spatialCompute.geostream.events, request)
            .then(renderResult)
            .catch(function (err) {
                showAlert(err.message || "Failed to process telemetry.");
            })
            .finally(function () {
                runBtn.disabled = false;
            });
    }

    function renderResult(result) {
        kpiTotal.textContent = result.totalEvents;
        kpiValid.textContent = result.validEvents;
        kpiAnomalies.textContent = result.anomalyCount;
        kpiNative.textContent = result.nativeAccelerated ? "Yes" : "No";

        stage.innerHTML = "";
        aggregateList.innerHTML = "";

        result.aggregates.forEach(function (aggregate) {
            var point = document.createElement("div");
            point.className = "spatial-point" + (aggregate.anomalyCount > 0 ? " anomaly" : "");
            point.style.left = normalize(aggregate.centerLongitude, -117.22, -117.16) + "%";
            point.style.top = (100 - normalize(aggregate.centerLatitude, 34.04, 34.08)) + "%";
            point.title = aggregate.count + " events, avg " + aggregate.averageSpeedMetersPerSecond + " m/s";
            stage.appendChild(point);

            var item = document.createElement("div");
            item.className = "geo-cell-item" + (aggregate.anomalyCount > 0 ? " geo-cell-anomaly" : "");
            item.innerHTML = "<strong>Cell " + aggregate.cellX + ", " + aggregate.cellY + "</strong>" +
                "<div class='geo-cell-meta'>" + aggregate.count + " events · avg " + aggregate.averageSpeedMetersPerSecond +
                " m/s · max " + aggregate.maxSpeedMetersPerSecond + " m/s · anomalies " + aggregate.anomalyCount + "</div>";
            aggregateList.appendChild(item);
        });

        if (window.showToast) {
            window.showToast("Telemetry batch processed.", "success");
        }
    }

    function normalize(value, min, max) {
        return Math.max(5, Math.min(95, ((value - min) / (max - min)) * 100));
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
