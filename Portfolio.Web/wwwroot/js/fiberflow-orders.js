/**
 * FiberFlow — Orders
 * Depends on: jQuery, Kendo UI, ArcGIS JS API 4.30
 * API endpoints used: GET/POST /api/fiberorders, PUT/DELETE /api/fiberorders/{id}
 */

(function() {
    function getAntiForgeryToken() {
        var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : undefined;
    }

    var notification;
    $(function() {
        notification = $("<span/>").kendoNotification({
            position: { pinned: true, top: 20, right: 20 },
            stacking: "down",
            autoHideAfter: 4000
        }).data("kendoNotification");
    });

    var statusClassMap = {
        "Draft": "fiber-badge-draft",
        "Confirmed": "fiber-badge-confirmed",
        "In Production": "fiber-badge-inproduction",
        "Shipped": "fiber-badge-shipped",
        "Delivered": "fiber-badge-delivered"
    };

    function statusTemplate(data) {
        var cls = statusClassMap[data.status] || "";
        return '<span class="' + cls + '">' + data.status + '</span>';
    }

    function shipDateTemplate(data) {
        return data.shipDate ? kendo.toString(new Date(data.shipDate), "MM/dd/yyyy") : "—";
    }

    function toolbarTemplate() {
        return '<button class="k-button k-primary k-grid-add">New Order</button>' +
               '<button class="k-button k-grid-excel">Export to Excel</button>';
    }

    function openOrderWindow(mode, dataItem, grid) {
        var isEdit = !!dataItem;
        var windowTitle = isEdit ? "Edit Order" : "New Order";
        var order = dataItem || {
            clientId: null, productName: "", productSku: "", quantity: 1, unitPrice: 0, status: "Draft", orderDate: new Date(), shipDate: null
        };
        var $form = $('<form id="fiberOrderForm">' +
            '<label>Client</label><input id="orderClient" required />' +
            '<label>Product Name</label><input id="orderProductName" required />' +
            '<label>Product SKU</label><input id="orderProductSku" required />' +
            '<label>Quantity</label><input id="orderQuantity" type="number" min="1" required />' +
            '<label>Unit Price</label><input id="orderUnitPrice" type="number" min="0" step="0.01" required />' +
            '<label>Status</label><input id="orderStatus" required />' +
            '<label>Order Date</label><input id="orderOrderDate" required />' +
            '<label>Ship Date</label><input id="orderShipDate" />' +
            '</form>');
        var wnd = $("<div/>").kendoWindow({
            title: windowTitle,
            modal: true,
            visible: false,
            resizable: false,
            width: 400,
            close: function() { wnd.destroy(); }
        }).data("kendoWindow");
        wnd.content($form);
        // Populate dropdowns
        $.getJSON("/api/fiberorders", function(orders) {
            var clients = [];
            var seen = {};
            orders.forEach(function(o) {
                if (!seen[o.clientId]) {
                    clients.push({ id: o.clientId, name: o.clientName });
                    seen[o.clientId] = true;
                }
            });
            $("#orderClient").kendoDropDownList({
                dataTextField: "name",
                dataValueField: "id",
                dataSource: clients,
                value: order.clientId
            });
        });
        $("#orderStatus").kendoDropDownList({
            dataSource: ["Draft","Confirmed","In Production","Shipped","Delivered"],
            value: order.status
        });
        $("#orderOrderDate").kendoDatePicker({ value: order.orderDate });
        $("#orderShipDate").kendoDatePicker({ value: order.shipDate });
        $("#orderProductName").val(order.productName);
        $("#orderProductSku").val(order.productSku);
        $("#orderQuantity").val(order.quantity);
        $("#orderUnitPrice").val(order.unitPrice);
        // Save handler
        var saveBtn = $('<button class="k-button k-primary" type="submit">Save</button>');
        $form.append(saveBtn);
        $form.on("submit", async function(e) {
            e.preventDefault();
            var dto = {
                clientId: $("#orderClient").data("kendoDropDownList").value(),
                productName: $("#orderProductName").val(),
                productSku: $("#orderProductSku").val(),
                quantity: parseInt($("#orderQuantity").val()),
                unitPrice: parseFloat($("#orderUnitPrice").val()),
                status: $("#orderStatus").data("kendoDropDownList").value(),
                orderDate: $("#orderOrderDate").data("kendoDatePicker").value(),
                shipDate: $("#orderShipDate").data("kendoDatePicker").value() || null
            };
            try {
                var url = isEdit ? "/api/fiberorders/" + dataItem.id : "/api/fiberorders";
                var method = isEdit ? "PUT" : "POST";
                var res = await fetch(url, {
                    method: method,
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": getAntiForgeryToken()
                    },
                    body: JSON.stringify(dto)
                });
                if (!res.ok) throw new Error("Failed to save order");
                wnd.close();
                grid.dataSource.read();
                notification && notification.show("Order saved", "success");
            } catch (err) {
                notification && notification.show(err.message, "error");
            }
        });
        wnd.center().open();
    }

    function openDeleteDialog(dataItem, grid) {
        $("<div/>").kendoDialog({
            title: "Delete Order",
            content: "Delete this order? This cannot be undone.",
            actions: [
                { text: "Cancel" },
                { text: "Delete", primary: true, action: async function() {
                    try {
                        var res = await fetch("/api/fiberorders/" + dataItem.id, {
                            method: "DELETE",
                            headers: { "RequestVerificationToken": getAntiForgeryToken() }
                        });
                        if (!res.ok) throw new Error("Failed to delete order");
                        grid.dataSource.read();
                        notification && notification.show("Order deleted", "success");
                    } catch (err) {
                        notification && notification.show(err.message, "error");
                    }
                }}
            ],
            modal: true
        }).data("kendoDialog").open();
    }

    function openDrawer(dataItem) {
        var $drawer = $("#fiberOrderDrawer");
        $drawer.html(
            '<div class="drawer-header"><h4>Order #' + dataItem.id + '</h4><button class="drawer-close">&times;</button></div>' +
            '<div><b>Client:</b> ' + dataItem.clientName + '</div>' +
            '<div><b>Product:</b> ' + dataItem.productName + ' (' + dataItem.productSku + ')</div>' +
            '<div><b>Quantity:</b> ' + dataItem.quantity + '</div>' +
            '<div><b>Status:</b> ' + dataItem.status + '</div>' +
            '<div><b>Order Date:</b> ' + kendo.toString(new Date(dataItem.orderDate), "MM/dd/yyyy") + '</div>' +
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
