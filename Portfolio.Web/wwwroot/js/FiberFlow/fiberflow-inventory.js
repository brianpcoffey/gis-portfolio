
// FiberFlow Inventory JS
// Handles DataTable for inventory and low stock alerts

let inventoryTable;


// Use shared toast helper (window.fiberflowToast provided by dashboard script)


$(document).ready(function () {
    loadInventoryTable();
    // Row click for details
    $('#fiberflowInventoryTable').on('click', 'tbody tr', function () {
        const data = inventoryTable.row(this).data();
        if (data) showMaterialDetailModal(data);
    });
});

function loadInventoryTable() {
    $('#inventoryTableSpinner').removeClass('d-none');
    fetch('/api/FiberMaterials')
        .then(r => r.json())
        .then(data => {
            if (inventoryTable) {
                inventoryTable.clear().rows.add(data).draw();
                fiberflowToast('Inventory loaded', 'success');
                return;
            }
            inventoryTable = $('#fiberflowInventoryTable').DataTable({
                data: data,
                columns: [
                    { title: 'Material', data: 'Name' },
                    { title: 'SKU', data: 'Sku' },
                    { title: 'Qty On Hand', data: 'QtyOnHand', className: 'text-end' },
                    { title: 'Unit Cost', data: 'UnitCost', render: $.fn.dataTable.render.number(',', '.', 2, '$'), className: 'text-end' },
                    { title: 'Total Value', data: 'TotalValue', render: $.fn.dataTable.render.number(',', '.', 2, '$'), className: 'text-end' },
                    { title: 'Reorder Point', data: 'ReorderPoint', className: 'text-end' },
                    {
                        title: '',
                        data: null,
                        orderable: false,
                        className: 'text-end',
                        render: function (data, type, row) {
                            return row.IsLowStock ? '<span class="badge bg-danger fiberflow-low-stock">Low</span>' : '';
                        }
                    }
                ],
                rowCallback: function (row, data) {
                    if (data.IsLowStock) {
                        $(row).addClass('fiberflow-low-stock');
                    }
                },
                order: [[2, 'asc']],
                responsive: true,
                autoWidth: false,
                language: { emptyTable: 'No inventory found.' }
            });
            fiberflowToast('Inventory loaded', 'success');
        })
        .catch(() => fiberflowToast('Failed to load inventory', 'error'))
        .finally(() => $('#inventoryTableSpinner').addClass('d-none'));
}

// Show material detail modal
function showMaterialDetailModal(material) {
    let modalId = 'fiberflowMaterialDetailModal';
    let $modals = $('#fiberflowModals');
    $modals.empty();
    let modalHtml = `
<div class="modal fade" id="${modalId}" tabindex="-1" aria-labelledby="${modalId}Label" aria-modal="true" role="dialog">
  <div class="modal-dialog">
    <div class="modal-content theme-surface">
      <div class="modal-header">
        <h5 class="modal-title" id="${modalId}Label">Material Details</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
      </div>
      <div class="modal-body">
        <dl class="row mb-0">
          <dt class="col-5">Name</dt><dd class="col-7">${material.Name}</dd>
          <dt class="col-5">SKU</dt><dd class="col-7">${material.Sku}</dd>
          <dt class="col-5">Qty On Hand</dt><dd class="col-7">${material.QtyOnHand}</dd>
          <dt class="col-5">Unit Cost</dt><dd class="col-7">$${material.UnitCost.toFixed(2)}</dd>
          <dt class="col-5">Total Value</dt><dd class="col-7">$${material.TotalValue.toFixed(2)}</dd>
          <dt class="col-5">Reorder Point</dt><dd class="col-7">${material.ReorderPoint}</dd>
        </dl>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
        <button type="button" class="btn btn-accent theme-btn" onclick="showReceiveStockModal(${material.Id})">Receive Stock</button>
      </div>
    </div>
  </div>
</div>`;
    $modals.html(modalHtml);
    let modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();
}

// Show receive stock modal
window.showReceiveStockModal = function (materialId) {
    let modalId = 'fiberflowReceiveStockModal';
    let $modals = $('#fiberflowModals');
    $modals.empty();
    let modalHtml = `
<div class="modal fade" id="${modalId}" tabindex="-1" aria-labelledby="${modalId}Label" aria-modal="true" role="dialog">
  <div class="modal-dialog">
    <div class="modal-content theme-surface">
      <div class="modal-header">
        <h5 class="modal-title" id="${modalId}Label">Receive Stock</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
      </div>
      <form id="receiveStockForm">
      <div class="modal-body">
        <div class="mb-3">
          <label class="form-label">Quantity Received</label>
          <input type="number" class="form-control" name="Quantity" min="0.01" step="0.01" required />
        </div>
        <div class="mb-3">
          <label class="form-label">Notes (optional)</label>
          <textarea class="form-control" name="Notes" rows="2"></textarea>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
        <button type="submit" class="btn btn-accent theme-btn">Receive</button>
      </div>
      </form>
    </div>
  </div>
</div>`;
    $modals.html(modalHtml);
    let modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();

    $('#receiveStockForm').on('submit', function (e) {
        e.preventDefault();
        let formData = Object.fromEntries(new FormData(this).entries());
        formData.Quantity = parseFloat(formData.Quantity);
        fetch(`/api/FiberMaterials/${materialId}/receive`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        })
        .then(r => {
            if (!r.ok) throw new Error('Failed to receive stock');
            return r.json();
        })
        .then(() => {
            modal.hide();
            fiberflowToast('Stock received', 'success');
            loadInventoryTable();
        })
        .catch(() => fiberflowToast('Failed to receive stock', 'error'));
    });
};
