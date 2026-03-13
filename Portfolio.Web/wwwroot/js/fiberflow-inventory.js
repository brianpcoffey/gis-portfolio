/**
 * FiberFlow — Inventory
 * Depends on: jQuery, Kendo UI
 * API endpoints used: GET /api/fibermaterials, POST /api/fibermaterials/{id}/receive
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
        loadInventory();
    });

    async function loadInventory() {
        try {
            const res = await fetch("/api/fibermaterials");
            if (!res.ok) throw new Error("Failed to load inventory");
            const data = await res.json();
            updateSummaryBar(data);
            initGrid(data);
        } catch (err) {
            notification && notification.show(err.message, "error");
        }
    }

    function updateSummaryBar(data) {
        var totalMaterials = data.length;
        var totalInventoryValue = data.reduce(function(sum, m) { return sum + (m.totalValue || 0); }, 0);
        var lowStockCount = data.filter(function(m) { return m.isLowStock; }).length;
        $("#fiberInventorySummary").html(
            '<span>Total Materials: <b>' + totalMaterials + '</b></span> '
            + '<span>Total Value: <b>' + totalInventoryValue.toLocaleString("en-US", { style: "currency", currency: "USD" }) + '</b></span> '
            + '<span>Low Stock: <b>' + lowStockCount + '</b></span>'
        );
    }

    function receiveStockDialog(dataItem) {
        var $dialog = $("<div/>");
        $dialog.append('<div>Current Qty: <b>' + dataItem.qtyOnHand + '</b></div>');
        $dialog.append('<input id="receiveQtyInput" type="number" min="0.01" step="0.01" class="k-textbox" placeholder="Quantity" />');
        $dialog.append('<textarea id="receiveNotesInput" class="k-textbox" placeholder="Notes (optional)"></textarea>');
        $dialog.kendoDialog({
            title: "Receive Stock — " + dataItem.name,
            actions: [
                { text: "Cancel" },
                { text: "Receive", primary: true, action: async function() {
                    var qty = parseFloat($("#receiveQtyInput").val());
                    var notes = $("#receiveNotesInput").val();
                    if (!qty || qty <= 0) {
                        $("#receiveQtyInput").addClass("k-invalid");
                        return false;
                    }
                    try {
                        var res = await fetch("/api/fibermaterials/" + dataItem.id + "/receive", {
                            method: "POST",
                            headers: {
                                "Content-Type": "application/json",
                                "RequestVerificationToken": getAntiForgeryToken()
                            },
                            body: JSON.stringify({ quantity: qty, notes: notes || null })
                        });
                        if (!res.ok) throw new Error("Failed to receive stock");
                        notification && notification.show("Stock updated — " + dataItem.name, "success");
                        loadInventory();
                    } catch (err) {
                        notification && notification.show(err.message, "error");
                    }
                }}
            ],
            modal: true
        }).data("kendoDialog").open();
    }

    function initGrid(data) {
        $("#fiberInventoryGrid").kendoGrid({
            dataSource: { data: data },
            schema: { model: {
                id: "id",
                fields: {
                    id: { type: "number" },
                    sku: { type: "string" },
                    name: { type: "string" },
                    unitOfMeasure: { type: "string" },
                    qtyOnHand: { type: "number" },
                    reorderPoint: { type: "number" },
                    unitCost: { type: "number" },
                    totalValue: { type: "number" },
                    isLowStock: { type: "boolean" },
                    supplier: { type: "string" },
                    warehouseLocation: { type: "string" }
                }
            }},
            columns: [
                { field: "sku", title: "SKU", width: 110 },
                { field: "name", title: "Material" },
                { field: "unitOfMeasure", title: "UOM", width: 70 },
                { field: "qtyOnHand", title: "Qty On Hand", width: 110 },
                { field: "reorderPoint", title: "Reorder Pt", width: 100 },
                { field: "unitCost", title: "Unit Cost", format: "{0:c2}", width: 100 },
                { field: "totalValue", title: "Total Value", format: "{0:c2}", width: 120 },
                { field: "supplier", title: "Supplier" },
                { field: "warehouseLocation", title: "Location", width: 90 },
                { command: [{ name: "receive", text: "Receive Stock", click: function(e) {
                    e.preventDefault();
                    var dataItem = this.dataItem($(e.currentTarget).closest("tr"));
                    receiveStockDialog(dataItem);
                }}], title: "", width: 130 }
            ],
            sortable: true,
            filterable: true,
            pageable: { pageSize: 15 },
            dataBound: function() {
                var grid = this;
                grid.tbody.find("tr").each(function() {
                    var dataItem = grid.dataItem($(this));
                    if (dataItem && dataItem.isLowStock) {
                        $(this).addClass("fiber-low-stock");
                    }
                });
            }
        });
    }
})();
