/**
 * FiberFlow — Shipments
 * Depends on: jQuery, Kendo UI, ArcGIS JS API 4.30
 * API endpoints used: GET /api/fibershipments, PUT /api/fibershipments/{id}/status
 */

(function() {
    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : undefined;
    }

    var notification;
    var shipmentData = [];
    var routeGraphics = {};
    var mapView, graphicsLayer;

    $(function() {
        notification = $("<span/>").kendoNotification({
            position: { pinned: true, top: 20, right: 20 },
            stacking: "down",
            autoHideAfter: 4000
        }).data("kendoNotification");
        loadShipments();
    });

    async function loadShipments() {
        try {
            const res = await fetch("/api/fibershipments");
            if (!res.ok) throw new Error("Failed to load shipments");
            shipmentData = await res.json();
            initGrid();
            initMap();
        } catch (err) {
            notification && notification.show(err.message, "error");
        }
    }

    function statusClass(status) {
        if (status === "Delivered") return "fiber-badge-delivered";
        if (status === "In Transit") return "fiber-badge-intransit";
        if (status === "Delayed") return "fiber-badge-delayed";
        return "";
    }

    function initGrid() {
        $("#fiberShipmentsGrid").kendoGrid({
            dataSource: { data: shipmentData },
            schema: { model: {
                id: "id",
                fields: {
                    id: { type: "number" },
                    trackingNumber: { type: "string" },
                    carrierName: { type: "string" },
                    destinationCity: { type: "string" },
                    destinationState: { type: "string" },
                    status: { type: "string" },
                    estimatedArrival: { type: "date" },
                    destinationLat: { type: "number" },
                    destinationLng: { type: "number" }
                }
            }},
            columns: [
                { field: "trackingNumber", title: "Tracking #" },
                { field: "carrierName", title: "Carrier" },
                { title: "Destination", template: "#= destinationCity #, #= destinationState #" },
                { field: "status", title: "Status", template: function(d){ return '<span class="' + statusClass(d.status) + '">' + d.status + '</span>'; } },
                { field: "estimatedArrival", title: "ETA", format: "{0:MM/dd/yy}" },
                { command: [{ name: "update", text: "Update", click: function(e) {
                    e.preventDefault();
                    var dataItem = this.dataItem($(e.currentTarget).closest("tr"));
                    openUpdateDialog(dataItem);
                }}], title: "", width: 100 }
            ],
            selectable: "row",
            scrollable: true,
            height: "100%",
            change: function() {
                var grid = this;
                var selected = grid.dataItem(grid.select());
                if (selected && routeGraphics[selected.id] && mapView) {
                    var s = selected;
                    var extent = {
                        type: "extent",
                        xmin: Math.min(s.originLng, s.destinationLng) - 1,
                        ymin: Math.min(s.originLat, s.destinationLat) - 1,
                        xmax: Math.max(s.originLng, s.destinationLng) + 1,
                        ymax: Math.max(s.originLat, s.destinationLat) + 1,
                        spatialReference: { wkid: 4326 }
                    };
                    mapView.goTo({ target: extent });
                }
            }
        });
    }

    function openUpdateDialog(dataItem) {
        var $dialog = $("<div/>");
        var $select = $('<input id="shipmentStatusSelect" />');
        $dialog.append("<label>Status</label>").append($select);
        $select.kendoDropDownList({
            dataSource: ["In Transit", "Delivered", "Delayed"],
            value: dataItem.status
        });
        $dialog.kendoDialog({
            title: "Update Status — " + dataItem.trackingNumber,
            content: $dialog,
            actions: [
                { text: "Cancel" },
                { text: "Update", primary: true, action: async function() {
                    var newStatus = $select.data("kendoDropDownList").value();
                    try {
                        var res = await fetch("/api/fibershipments/" + dataItem.id + "/status", {
                            method: "PUT",
                            headers: {
                                "Content-Type": "application/json",
                                "RequestVerificationToken": getAntiForgeryToken()
                            },
                            body: JSON.stringify({ status: newStatus })
                        });
                        if (!res.ok) throw new Error("Failed to update status");
                        dataItem.status = newStatus;
                        $("#fiberShipmentsGrid").data("kendoGrid").dataSource.read();
                        renderMapGraphics();
                        notification && notification.show("Status updated", "success");
                    } catch (err) {
                        notification && notification.show(err.message, "error");
                    }
                }}
            ],
            modal: true
        }).data("kendoDialog").open();
    }

    function initMap() {
        require([
            "esri/Map", "esri/views/MapView", "esri/Graphic", "esri/layers/GraphicsLayer", "esri/geometry/Polyline", "esri/symbols/SimpleLineSymbol", "esri/symbols/SimpleMarkerSymbol", "esri/PopupTemplate"
        ], function(Map, MapViewCtor, Graphic, GraphicsLayerCtor, Polyline, SimpleLineSymbol, SimpleMarkerSymbol, PopupTemplate) {
            var map = new Map({ basemap: "dark-gray-vector" });
            mapView = new MapViewCtor({
                container: "fiberShipmentMap",
                map: map,
                center: [-90, 32],
                zoom: 5
            });
            graphicsLayer = new GraphicsLayerCtor();
            map.add(graphicsLayer);
            renderMapGraphics();
        });
    }

    function renderMapGraphics() {
        if (!graphicsLayer) return;
        graphicsLayer.removeAll();
        routeGraphics = {};
        shipmentData.forEach(function(s) {
            var color = s.status === "Delivered" ? [34,197,94] : s.status === "In Transit" ? [249,115,22] : [239,68,68];
            var polyline = new window.require("esri/geometry/Polyline")({ paths: [ [ [s.originLng, s.originLat], [s.destinationLng, s.destinationLat] ] ] });
            var line = new window.require("esri/Graphic")({
                geometry: polyline,
                symbol: new window.require("esri/symbols/SimpleLineSymbol")({ color: color, width: 3 }),
                popupTemplate: new window.require("esri/PopupTemplate")({ title: s.trackingNumber, content: "Carrier: " + s.carrierName + "<br>Status: " + s.status + "<br>ETA: " + (s.estimatedArrival ? new Date(s.estimatedArrival).toLocaleDateString() : "") })
            });
            graphicsLayer.add(line);
            routeGraphics[s.id] = line;
            // Destination marker
            var destMarker = new window.require("esri/Graphic")({
                geometry: { type: "point", longitude: s.destinationLng, latitude: s.destinationLat },
                symbol: new window.require("esri/symbols/SimpleMarkerSymbol")({ style: "circle", color: [255,255,255], size: 8, outline: { color: color, width: 2 } }),
                popupTemplate: new window.require("esri/PopupTemplate")({ title: s.trackingNumber, content: "Carrier: " + s.carrierName + "<br>Status: " + s.status + "<br>ETA: " + (s.estimatedArrival ? new Date(s.estimatedArrival).toLocaleDateString() : "") })
            });
            graphicsLayer.add(destMarker);
        });
    }
})();
