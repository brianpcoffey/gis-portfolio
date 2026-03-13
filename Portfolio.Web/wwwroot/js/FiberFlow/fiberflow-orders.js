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