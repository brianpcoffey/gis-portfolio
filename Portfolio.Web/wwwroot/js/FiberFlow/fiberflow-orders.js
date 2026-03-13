/**
 * FiberFlow — Orders
 * Depends on: jQuery, DataTables, ArcGIS JS API 4.30
 * API endpoints: GET /api/fiberorders, POST /api/fiberorders, PUT /api/fiberorders/{id}, DELETE /api/fiberorders/{id}
 */

(function() {
    $(function() {
        initOrdersTable();
    });

    function statusBadge(status) {
        const map = {
            "Draft": "fiber-badge-draft",
            "Confirmed": "fiber-badge-confirmed",
            "In Production": "fiber-badge-inproduction",
            "Shipped": "fiber-badge-shipped",
            "Delivered": "fiber-badge-delivered"
        };
        return `<span class="fiber-status-badge ${map[status] || ''}">${status}</span>`;
    }

    function formatDate(date) {
        if (!date) return '—';
        const d = new Date(date);
        return d.toLocaleDateString();
    }

    function initOrdersTable() {
        if ($.fn.DataTable.isDataTable('#fiberOrdersGrid')) {
            $('#fiberOrdersGrid').DataTable().destroy();
            $('#fiberOrdersGrid').empty();
        }
        const table = $('<table id="ordersTable" class="display" style="width:100%"></table>');
        $('#fiberOrdersGrid').append(table);
        const dt = table.DataTable({
            ajax: {
                url: '/api/fiberorders',
                dataSrc: ''
            },
            columns: [
                { data: 'orderNumber', title: 'Order #' },
                { data: 'clientName', title: 'Client' },
                { data: 'productName', title: 'Product' },
                { data: 'quantity', title: 'Quantity' },
                { data: 'unitPrice', title: 'Unit Price', render: d => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(d) },
                { data: 'status', title: 'Status', render: statusBadge },
                { data: 'orderDate', title: 'Order Date', render: formatDate },
                { data: 'shipDate', title: 'Ship Date', render: formatDate },
                { data: null, title: 'Actions', orderable: false, render: function(data, type, row) {
                    return `<button class="btn btn-sm btn-primary fiber-edit-btn" data-id="${row.id}">Edit</button> <button class="btn btn-sm btn-danger fiber-delete-btn" data-id="${row.id}">Delete</button>`;
                }}
            ],
            destroy: true
        });
        // Row click for drawer
        $('#ordersTable tbody').on('click', 'tr', function() {
            const rowData = dt.row(this).data();
            if (rowData) openOrderDrawer(rowData);
        });
        // Actions
        $('#fiberOrdersGrid').off('click').on('click', '.fiber-edit-btn', function(e) {
            e.stopPropagation();
            const id = $(this).data('id');
            editOrderDialog(id, dt);
        });
        $('#fiberOrdersGrid').on('click', '.fiber-delete-btn', function(e) {
            e.stopPropagation();
            const id = $(this).data('id');
            deleteOrder(id, dt);
        });
    }

    // CRUD
    async function editOrderDialog(id, dt) {
        // For brevity, not implemented here. Should open a modal for editing/creating orders and call the API, then dt.ajax.reload().
        alert('Order editing not implemented in this demo.');
    }
    async function deleteOrder(id, dt) {
        if (!confirm('Delete this order?')) return;
        try {
            const res = await fetch(`/api/fiberorders/${id}`, { method: 'DELETE' });
            if (!res.ok) throw new Error('Failed to delete order');
            dt.ajax.reload();
        } catch (err) {
            alert(err.message);
        }
    }

    // Drawer
    function openOrderDrawer(order) {
        const $drawer = $('#fiberOrderDrawer');
        $drawer.empty();
        $drawer.append(`
            <div style="padding:24px">
                <h4>Order #${order.orderNumber}</h4>
                <div><b>Client:</b> ${order.clientName}</div>
                <div><b>Product:</b> ${order.productName}</div>
                <div><b>Quantity:</b> ${order.quantity}</div>
                <div><b>Unit Price:</b> ${new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(order.unitPrice)}</div>
                <div><b>Total:</b> ${new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(order.unitPrice * order.quantity)}</div>
                <div><b>Status:</b> ${statusBadge(order.status)}</div>
                <div><b>Order Date:</b> ${formatDate(order.orderDate)}</div>
                <div><b>Ship Date:</b> ${formatDate(order.shipDate)}</div>
                <div id="orderMiniMap" style="margin-top:16px;"></div>
                <button class="btn btn-secondary mt-3" id="drawerCloseBtn">Close</button>
            </div>
        `);
        $drawer.addClass('open');
        $drawer.on('click', '#drawerCloseBtn', function() { $drawer.removeClass('open'); });
        // ArcGIS mini-map
        if (order.clientLat && order.clientLng) {
            loadMiniMap(order.clientLat, order.clientLng);
        }
    }

    function loadMiniMap(lat, lng) {
        require([
            "esri/Map", "esri/views/MapView", "esri/Graphic", "esri/symbols/SimpleMarkerSymbol"
        ], function(Map, MapView, Graphic, SimpleMarkerSymbol) {
            const map = new Map({ basemap: "dark-gray-vector" });
            const view = new MapView({
                container: "orderMiniMap",
                map: map,
                center: [lng, lat],
                zoom: 10,
                ui: { components: [] }
            });
            const marker = new Graphic({
                geometry: { type: "point", longitude: lng, latitude: lat },
                symbol: new SimpleMarkerSymbol({ style: "circle", color: [59, 130, 246], size: 14, outline: { color: [0,0,0], width: 1 } })
            });
            view.graphics.add(marker);
        });
    }

})();
            '<div><b>Ship Date:</b> ' + (dataItem.shipDate ? kendo.toString(new Date(dataItem.shipDate), "MM/dd/yyyy") : "—") + '</div>' +
            '<div id="fiberOrderMiniMap"></div>'
        ).addClass("open");
        // Esri map
        require(["esri/Map", "esri/views/MapView", "esri/Graphic", "esri/symbols/SimpleMarkerSymbol", "esri/PopupTemplate"],
        function(Map, MapView, Graphic, SimpleMarkerSymbol, PopupTemplate) {
            if (!$drawer.data("mapView")) {
                var map = new Map({ basemap: "dark-gray-vector" });
                var view = new MapView({
                    container: "fiberOrderMiniMap",
                    map: map,
                    center: [dataItem.clientLng, dataItem.clientLat],
                    zoom: 10
                });
                var marker = new Graphic({
                    geometry: { type: "point", longitude: dataItem.clientLng, latitude: dataItem.clientLat },
                    symbol: new SimpleMarkerSymbol({ style: "circle", color: [59,130,246], size: 12, outline: { color: [255,255,255], width: 2 } }),
                    popupTemplate: new PopupTemplate({ title: dataItem.clientName })
                });
                view.graphics.add(marker);
                $drawer.data("mapView", view);
            } else {
                var view = $drawer.data("mapView");
                view.goTo({ target: [dataItem.clientLng, dataItem.clientLat], zoom: 10 });
            }
        });
        $drawer.find(".drawer-close").on("click", function() { $drawer.removeClass("open"); });
    }

    $(function() {
        var grid = $("#fiberOrdersGrid").kendoGrid({
            dataSource: {
                transport: {
                    read: { url: "/api/fiberorders", type: "GET" },
                    create: { url: "/api/fiberorders", type: "POST" },
                    update: { url: function(e) { return "/api/fiberorders/" + e.id; }, type: "PUT" },
                    destroy: { url: function(e) { return "/api/fiberorders/" + e.id; }, type: "DELETE" }
                },
                schema: {
                    model: {
                        id: "id",
                        fields: {
                            id: { type: "number", editable: false },
                            clientId: { type: "number" },
                            clientName: { type: "string" },
                            productName: { type: "string" },
                            productSku: { type: "string" },
                            quantity: { type: "number" },
                            unitPrice: { type: "number" },
                            totalValue: { type: "number" },
                            status: { type: "string" },
                            orderDate: { type: "date" },
                            shipDate: { type: "date" },
                            clientLat: { type: "number" },
                            clientLng: { type: "number" }
                        }
                    }
                }
            },
            columns: [
                { field: "id", title: "Order #", width: 80 },
                { field: "clientName", title: "Client" },
                { field: "productName", title: "Product" },
                { field: "quantity", title: "Qty", width: 70 },
                { field: "unitPrice", title: "Unit Price", format: "{0:c2}", width: 110 },
                { field: "totalValue", title: "Total", format: "{0:c2}", width: 110 },
                { field: "status", title: "Status", width: 130, template: statusTemplate },
                { field: "orderDate", title: "Order Date", format: "{0:MM/dd/yyyy}", width: 110 },
                { field: "shipDate", title: "Ship Date", width: 110, template: shipDateTemplate },
                { command: [
                    { name: "edit", text: "Edit", click: function(e) {
                        e.preventDefault();
                        var dataItem = this.dataItem($(e.currentTarget).closest("tr"));
                        openOrderWindow(true, dataItem, grid.data("kendoGrid"));
                    }},
                    { name: "destroy", text: "Delete", click: function(e) {
                        e.preventDefault();
                        var dataItem = this.dataItem($(e.currentTarget).closest("tr"));
                        openDeleteDialog(dataItem, grid.data("kendoGrid"));
                    }}
                ], title: "", width: 160 }
            ],
            filterable: true,
            sortable: true,
            pageable: { pageSize: 10 },
            toolbar: ["create", { name: "excel", text: "Export to Excel" }],
            excel: { fileName: "FiberFlow-Orders.xlsx", allPages: true },
            editable: false,
            dataBound: function() {
                var grid = this;
                grid.tbody.find("tr").on("click", function(e) {
                    if ($(e.target).closest(".k-button").length) return;
                    var dataItem = grid.dataItem($(this));
                    openDrawer(dataItem);
                });
            }
        }).data("kendoGrid");
        // Create button
        $(document).on("click", ".k-grid-add", function(e) {
            e.preventDefault();
            openOrderWindow(false, null, grid);
        });
    });
})();
