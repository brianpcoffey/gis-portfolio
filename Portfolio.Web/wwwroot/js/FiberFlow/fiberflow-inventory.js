/**
 * FiberFlow — Inventory
 * Depends on: jQuery, DataTables
 * API endpoints: GET /api/fibermaterials, POST /api/fibermaterials/{id}/receive
 */

(function() {
    $(function() {
        loadInventory();
    });

    async function loadInventory() {
        try {
            const res = await fetch("/api/fibermaterials");
            if (!res.ok) throw new Error("Failed to load inventory");
            const data = await res.json();
            updateSummaryBar(data);
            renderTable(data);
        } catch (err) {
            alert(err.message);
        }
    }

    function updateSummaryBar(data) {
        var totalMaterials = data.length;
        var totalInventoryValue = data.reduce((sum, m) => sum + (m.totalValue || 0), 0);
        var lowStockCount = data.filter(m => m.isLowStock).length;
        $("#fiberInventorySummary").html(
            `<span>Total Materials: <b>${totalMaterials}</b></span> ` +
            `<span>Total Value: <b>${new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(totalInventoryValue)}</b></span> ` +
            `<span>Low Stock: <b>${lowStockCount}</b></span>`
        );
    }

    function renderTable(data) {
        if ($.fn.DataTable.isDataTable('#fiberInventoryGrid')) {
            $('#fiberInventoryGrid').DataTable().destroy();
            $('#fiberInventoryGrid').empty();
        }
        const table = $('<table class="display" style="width:100%"></table>');
        $('#fiberInventoryGrid').append(table);
        table.DataTable({
            data: data,
            columns: [
                { data: 'name', title: 'Material' },
                { data: 'sku', title: 'SKU' },
                { data: 'qtyOnHand', title: 'Quantity' },
                { data: 'unitCost', title: 'Unit Cost', render: d => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(d) },
                { data: 'totalValue', title: 'Total Value', render: d => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(d) },
                { data: null, title: 'Actions', orderable: false, render: function(data, type, row) {
                    return `<button class="btn btn-sm btn-primary fiber-receive-btn" data-id="${row.id}">Receive</button>`;
                }}
            ],
            rowCallback: function(row, rowData) {
                if (rowData.isLowStock) {
                    $(row).addClass('fiber-low-stock');
                }
            },
            destroy: true
        });
        // Attach receive button handler
        $('#fiberInventoryGrid').off('click').on('click', '.fiber-receive-btn', function() {
            const id = $(this).data('id');
            const item = data.find(m => m.id === id);
            receiveStockDialog(item);
        });
    }

    function receiveStockDialog(dataItem) {
        const $dialog = $('<div class="fiber-receive-dialog"></div>');
        $dialog.append(`<div>Current Qty: <b>${dataItem.qtyOnHand}</b></div>`);
        $dialog.append('<input id="receiveQtyInput" type="number" min="0.01" step="0.01" class="form-control mb-2" placeholder="Quantity" />');
        $dialog.append('<textarea id="receiveNotesInput" class="form-control mb-2" placeholder="Notes (optional)"></textarea>');
        $dialog.append('<button id="receiveSubmitBtn" class="btn btn-success">Receive</button> <button id="receiveCancelBtn" class="btn btn-secondary">Cancel</button>');
        $("body").append($dialog);
        $dialog.css({ position: 'fixed', top: '30%', left: '50%', transform: 'translate(-50%, -30%)', background: '#fff', padding: '24px', borderRadius: '8px', zIndex: 2000, boxShadow: '0 2px 16px #0002' });
        $dialog.on('click', '#receiveCancelBtn', function() { $dialog.remove(); });
        $dialog.on('click', '#receiveSubmitBtn', async function() {
            const qty = parseFloat($('#receiveQtyInput').val());
            const notes = $('#receiveNotesInput').val();
            if (!qty || qty <= 0) {
                $('#receiveQtyInput').addClass('is-invalid');
                return;
            }
            try {
                const res = await fetch(`/api/fibermaterials/${dataItem.id}/receive`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ quantity: qty, notes: notes || null })
                });
                if (!res.ok) throw new Error('Failed to receive stock');
                $dialog.remove();
                loadInventory();
            } catch (err) {
                alert(err.message);
            }
        });
    }

})();