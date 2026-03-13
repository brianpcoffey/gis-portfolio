/**
 * FiberFlow — Dashboard
 * Depends on: jQuery, Kendo UI, ArcGIS JS API 4.30
 * API endpoints used: GET /api/fiberdashboard/stats, GET /api/fibershipments
 */

(function() {
    // Utility: Get anti-forgery token
    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : undefined;
    }

    // Kendo Notification
    var notification;
    $(function() {
        notification = $("<span/>").kendoNotification({
            position: { pinned: true, top: 20, right: 20 },
            stacking: "down",
            autoHideAfter: 4000
        }).data("kendoNotification");
    });

    // KPI Cards
    async function loadKpiCards() {
        try {
            const res = await fetch("/api/fiberdashboard/stats");
            if (!res.ok) throw new Error("Failed to load dashboard stats");
            const stats = await res.json();
            $("#fiber-kpi-active-shipments").text(stats.activeShipments);
            $("#fiber-kpi-open-orders").text(stats.openOrders);
            $("#fiber-kpi-low-stock").text(stats.lowStockAlerts);
            $("#fiber-kpi-mtd-revenue").text(stats.mtdRevenue.toLocaleString("en-US", { style: "currency", currency: "USD" }));
            if (stats.lowStockAlerts > 0) {
                $("#fiber-kpi-low-stock").addClass("fiber-kpi-pulse");
            }
            // Charts
            initCharts(stats);
        } catch (err) {
            notification && notification.show(err.message, "error");
        }
    }

    // Kendo Charts
    function initCharts(stats) {
        // Revenue by Month
        $("#fiber-chart-revenue").kendoChart({
            dataSource: { data: stats.revenueByMonth },
            series: [{ type: "column", field: "revenue", categoryField: "month", color: "#f97316" }],
            background: "transparent",
            legend: { labels: { color: "#e2e8f0" } },
            categoryAxis: { labels: { color: "#e2e8f0" } },
            valueAxis: { labels: { color: "#e2e8f0" } }
        });
        // Orders by Status
        $("#fiber-chart-status").kendoChart({
            dataSource: { data: stats.ordersByStatus },
            series: [{ type: "donut", field: "count", categoryField: "status", colorField: "color", color: function(e){return e.dataItem.color;} }],
            palette: ["#64748b","#3b82f6","#eab308","#f97316","#22c55e"],
            background: "transparent",
            legend: { labels: { color: "#e2e8f0" } },
            categoryAxis: { labels: { color: "#e2e8f0" } },
            valueAxis: { labels: { color: "#e2e8f0" } }
        });
        // Top 5 Clients
        $("#fiber-chart-topclients").kendoChart({
            dataSource: { data: stats.topClients },
            series: [{ type: "bar", field: "totalValue", categoryField: "name", color: "#14b8a6" }],
            background: "transparent",
            legend: { labels: { color: "#e2e8f0" } },
            categoryAxis: { labels: { color: "#e2e8f0" } },
            valueAxis: { labels: { color: "#e2e8f0" } }
        });
    }

    // Esri Map
    function initMap() {
        require([
            "esri/Map", "esri/views/MapView", "esri/Graphic", "esri/layers/GraphicsLayer", "esri/geometry/Polyline", "esri/symbols/SimpleLineSymbol", "esri/symbols/SimpleMarkerSymbol", "esri/PopupTemplate"
        ], function(Map, MapView, Graphic, GraphicsLayer, Polyline, SimpleLineSymbol, SimpleMarkerSymbol, PopupTemplate) {
            var map = new Map({ basemap: "dark-gray-vector" });
            var view = new MapView({
                container: "fiberDashboardMap",
                map: map,
                center: [-95.3698, 29.7604],
                zoom: 5
            });
            var graphicsLayer = new GraphicsLayer();
            map.add(graphicsLayer);
            // Plant marker
            var plantMarker = new Graphic({
                geometry: { type: "point", longitude: -95.3698, latitude: 29.7604 },
                symbol: new SimpleMarkerSymbol({ style: "diamond", color: [249, 115, 22], size: 18, outline: { color: [0,0,0], width: 1 } }),
                popupTemplate: { title: "FiberFlow Plant", content: "Houston, TX — Manufacturing Facility" }
            });
            graphicsLayer.add(plantMarker);
            // Fetch shipments
            fetch("/api/fibershipments").then(function(res) {
                if (!res.ok) throw new Error("Failed to load shipments");
                return res.json();
            }).then(function(shipments) {
                var clientSet = {};
                shipments.forEach(function(s) {
                    // Polyline
                    var color = s.status === "Delivered" ? [34,197,94] : s.status === "In Transit" ? [249,115,22] : [239,68,68];
                    var polyline = new Polyline({ paths: [ [ [s.originLng, s.originLat], [s.destinationLng, s.destinationLat] ] ] });
                    var line = new Graphic({
                        geometry: polyline,
                        symbol: new SimpleLineSymbol({ color: color, width: 2 }),
                        popupTemplate: new PopupTemplate({ title: s.trackingNumber, content: "Carrier: " + s.carrier + "<br>Status: " + s.status + "<br>ETA: " + (s.eta ? new Date(s.eta).toLocaleDateString() : "") })
                    });
                    graphicsLayer.add(line);
                    // Destination marker
                    var destMarker = new Graphic({
                        geometry: { type: "point", longitude: s.destinationLng, latitude: s.destinationLat },
                        symbol: new SimpleMarkerSymbol({ style: "circle", color: [255,255,255], size: 8, outline: { color: color, width: 2 } }),
                        popupTemplate: new PopupTemplate({ title: s.trackingNumber, content: "Carrier: " + s.carrier + "<br>Status: " + s.status + "<br>ETA: " + (s.eta ? new Date(s.eta).toLocaleDateString() : "") })
                    });
                    graphicsLayer.add(destMarker);
                    // Client marker (dedup by clientName)
                    if (!clientSet[s.clientName]) {
                        clientSet[s.clientName] = true;
                        var clientMarker = new Graphic({
                            geometry: { type: "point", longitude: s.destinationLng, latitude: s.destinationLat },
                            symbol: new SimpleMarkerSymbol({ style: "circle", color: [59,130,246], size: 10, outline: { color: [255,255,255], width: 1 } }),
                            popupTemplate: new PopupTemplate({ title: s.clientName, content: s.destinationCity })
                        });
                        graphicsLayer.add(clientMarker);
                    }
                });
            }).catch(function(err) {
                notification && notification.show(err.message, "error");
            });
        });
    }

    document.addEventListener("DOMContentLoaded", function() {
        loadKpiCards();
        initMap();
    });
})();
