/**
 * FiberFlow — Shipments
 * Depends on: jQuery, DataTables, ArcGIS JS API 4.30
 * API endpoints: GET /api/fibershipments, PUT /api/fibershipments/{id}/status
 */

(function() {
    let shipmentData = [];
    let mapView, graphicsLayer, routeGraphics = {};

    $(function () {
        loadShipments();
        var arcgisScript = document.querySelector('script[src*="js.arcgis.com"]');
        arcgisScript && arcgisScript.addEventListener('load', function () {
            if (typeof shipmentData !== 'undefined') initMap();
        });
        if (typeof require !== 'undefined') {
            if (typeof shipmentData !== 'undefined') initMap();
        }
    });

    async function loadShipments() {
        try {
            const res = await fetch('/api/fibershipments');
            if (!res.ok) throw new Error('Failed to load shipments');
            shipmentData = await res.json();
            renderTable();
        } catch (err) {
            alert(err.message);
        }
    }

    function statusBadge(status) {
        const map = {
            "Delivered": "fiber-badge-delivered",
            "In Transit": "fiber-badge-intransit",
            "Delayed": "fiber-badge-delayed"
        };
        return `<span class="fiber-status-badge ${map[status] || ''}">${status}</span>`;
    }

    function renderTable() {
        if ($.fn.DataTable.isDataTable('#fiberShipmentsGrid')) {
            $('#fiberShipmentsGrid').DataTable().destroy();
            $('#fiberShipmentsGrid').empty();
        }
        const table = $('<table id="shipmentsTable" class="display" style="width:100%"></table>');
        $('#fiberShipmentsGrid').append(table);
        const dt = table.DataTable({
            data: shipmentData,
            columns: [
                { data: 'trackingNumber', title: 'Tracking #' },
                { data: 'carrierName', title: 'Carrier' },
                { data: null, title: 'Destination', render: d => `${d.destinationCity}, ${d.destinationState}` },
                { data: 'status', title: 'Status', render: statusBadge },
                { data: 'estimatedArrival', title: 'ETA', render: d => d ? new Date(d).toLocaleDateString() : '—' },
                { data: null, title: 'Update', orderable: false, render: function(data, type, row) {
                    return `<button class="btn btn-sm btn-primary fiber-update-btn" data-id="${row.id}">Update</button>`;
                }}
            ],
            destroy: true
        });
        // Row selection triggers map zoom
        $('#shipmentsTable tbody').on('click', 'tr', function() {
            const rowData = dt.row(this).data();
            if (rowData) zoomToShipment(rowData);
        });
        // Update button
        $('#fiberShipmentsGrid').off('click').on('click', '.fiber-update-btn', function(e) {
            e.stopPropagation();
            const id = $(this).data('id');
            const item = shipmentData.find(s => s.id === id);
            openUpdateDialog(item, dt);
        });
    }

    function openUpdateDialog(dataItem, dt) {
        const $dialog = $('<div class="fiber-update-dialog"></div>');
        $dialog.append('<label>Status</label>');
        $dialog.append('<select id="shipmentStatusSelect" class="form-control mb-2">'
            + '<option>Delivered</option>'
            + '<option>In Transit</option>'
            + '<option>Delayed</option>'
            + '</select>');
        $dialog.append('<button id="updateStatusBtn" class="btn btn-success">Update</button> <button id="cancelStatusBtn" class="btn btn-secondary">Cancel</button>');
        $("body").append($dialog);
        $dialog.css({ position: 'fixed', top: '30%', left: '50%', transform: 'translate(-50%, -30%)', background: '#fff', padding: '24px', borderRadius: '8px', zIndex: 2000, boxShadow: '0 2px 16px #0002' });
        $dialog.on('click', '#cancelStatusBtn', function() { $dialog.remove(); });
        $dialog.on('click', '#updateStatusBtn', async function() {
            const status = $('#shipmentStatusSelect').val();
            try {
                const res = await fetch(`/api/fibershipments/${dataItem.id}/status`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ status })
                });
                if (!res.ok) throw new Error('Failed to update status');
                $dialog.remove();
                await loadShipments();
            } catch (err) {
                alert(err.message);
            }
        });
    }

    function zoomToShipment(s) {
        if (!mapView || !s) return;
        const extent = {
            type: "extent",
            xmin: Math.min(s.originLng, s.destinationLng) - 1,
            ymin: Math.min(s.originLat, s.destinationLat) - 1,
            xmax: Math.max(s.originLng, s.destinationLng) + 1,
            ymax: Math.max(s.originLat, s.destinationLat) + 1,
            spatialReference: { wkid: 4326 }
        };
        mapView.goTo({ target: extent });
    }

    // ArcGIS Map
    function initMap() {
        require([
            "esri/Map", "esri/views/MapView", "esri/Graphic", "esri/layers/GraphicsLayer", "esri/geometry/Polyline", "esri/symbols/SimpleLineSymbol", "esri/symbols/SimpleMarkerSymbol", "esri/PopupTemplate"
        ], function(EsriMap, MapView, Graphic, GraphicsLayer, Polyline, SimpleLineSymbol, SimpleMarkerSymbol, PopupTemplate) {
            const esriMap = new EsriMap({ basemap: "dark-gray-vector" });
            mapView = new MapView({
                container: "fiberShipmentMap",
                map: esriMap,
                center: [-95.3698, 29.7604],
                zoom: 5
            });
            graphicsLayer = new GraphicsLayer();
            esriMap.add(graphicsLayer);
            // Plant marker
            const plantMarker = new Graphic({
                geometry: { type: "point", longitude: -95.3698, latitude: 29.7604 },
                symbol: new SimpleMarkerSymbol({ style: "diamond", color: [249, 115, 22], size: 18, outline: { color: [0,0,0], width: 1 } }),
                popupTemplate: { title: "FiberFlow Plant", content: "Houston, TX — Manufacturing Facility" }
            });
            graphicsLayer.add(plantMarker);
            // Draw shipment routes
            graphicsLayer.removeAll();
            graphicsLayer.add(plantMarker);
            routeGraphics = {};
            shipmentData.forEach(function(s) {
                if (!s.route || !Array.isArray(s.route)) return;
                const polyline = new Polyline({ paths: [s.route.map(pt => [pt.lng, pt.lat])] });
                const lineSymbol = new SimpleLineSymbol({ color: [59, 130, 246], width: 3 });
                const graphic = new Graphic({ geometry: polyline, symbol: lineSymbol, popupTemplate: new PopupTemplate({ title: s.trackingNumber, content: s.status }) });
                graphicsLayer.add(graphic);
                routeGraphics[s.id] = graphic;
            });
        });
    }

})();
