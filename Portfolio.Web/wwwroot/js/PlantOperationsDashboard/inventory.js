// FiberFlow Inventory JS
// Handles DataTable for inventory and low stock alerts

let inventoryTable;


// Use shared toast helper (window.fiberflowToast provided by dashboard script)


$(document).ready(function () {
    loadInventoryTable();
    $(document).on('click', '#btnNewMaterial', function () {
        showMaterialModal();
    });
    // Row click for details removed to prevent double modal issue
    // $('#fiberflowInventoryTable').on('click', 'tbody tr', function () {
    //     const data = inventoryTable.row(this).data();
    //     if (data) showMaterialDetailModal(data);
    // });
});

function loadInventoryTable() {
    $('#inventoryTableSpinner').removeClass('d-none');
    fetchWithAuth('/api/FiberMaterials')
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
                    { title: 'Material', data: 'name' },
                    { title: 'SKU', data: 'sku' },
                    { title: 'Qty On Hand', data: 'qtyOnHand', className: 'text-end' },
                    { title: 'Unit Cost', data: 'unitCost', render: $.fn.dataTable.render.number(',', '.', 2, '$'), className: 'text-end' },
                    { title: 'Total Value', data: 'totalValue', render: $.fn.dataTable.render.number(',', '.', 2, '$'), className: 'text-end' },
                    { title: 'Reorder Point', data: 'reorderPoint', className: 'text-end' },
                    {
                        title: '',
                        data: null,
                        orderable: false,
                        className: 'text-end',
                        render: function (data, type, row) {
                            return `
                                <button class="btn btn-sm btn-outline-primary me-1" onclick="showMaterialModal(${row.id})" title="Edit"><i class='fa fa-edit'></i></button>
                                <button class="btn btn-sm btn-outline-success me-1" onclick="showReceiveStockModal(${row.id})" title="Receive Stock"><i class='fa fa-arrow-down'></i></button>
                                <button class="btn btn-sm btn-outline-danger me-1" onclick="deleteMaterial(${row.id})" title="Delete"><i class='fa fa-trash'></i></button>
                                ${row.isLowStock ? '<span class="badge bg-danger fiberflow-low-stock">Low</span>' : ''}
                            `;
                        }
                    }
                ],
                rowCallback: function (row, data) {
                    if (data.isLowStock) {
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
          <dt class="col-5">Name</dt><dd class="col-7">${material.name}</dd>
          <dt class="col-5">SKU</dt><dd class="col-7">${material.sku}</dd>
          <dt class="col-5">Qty On Hand</dt><dd class="col-7">${material.qtyOnHand}</dd>
          <dt class="col-5">Unit Cost</dt><dd class="col-7">$${material.unitCost.toFixed(2)}</dd>
          <dt class="col-5">Total Value</dt><dd class="col-7">$${material.totalValue.toFixed(2)}</dd>
          <dt class="col-5">Reorder Point</dt><dd class="col-7">${material.reorderPoint}</dd>
        </dl>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
        <button type="button" class="btn btn-accent theme-btn" onclick="showReceiveStockModal(${material.id})">Receive Stock</button>
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
          <input type="number" class="form-control" name="quantity" min="0.01" step="0.01" required />
        </div>
        <div class="mb-3">
          <label class="form-label">Notes (optional)</label>
          <textarea class="form-control" name="notes" rows="2"></textarea>
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
        formData.quantity = parseFloat(formData.quantity);
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

function showMaterialModal(materialId) {
    let isEdit = !!materialId;
    let modalId = 'fiberflowMaterialModal';
    let $modals = $('#fiberflowModals');
    $modals.empty();
    let material = null;
    if (isEdit) {
        fetch(`/api/FiberMaterials/${materialId}`)
            .then(r => r.json())
            .then(data => {
                material = data;
                renderMaterialModal(material, isEdit, modalId, $modals);
            });
    } else {
        renderMaterialModal({}, false, modalId, $modals);
    }
}
window.showMaterialModal = showMaterialModal;
function renderMaterialModal(material, isEdit, modalId, $modals) {
    let modalHtml = `
<div class="modal fade" id="${modalId}" tabindex="-1" aria-labelledby="${modalId}Label" aria-modal="true" role="dialog">
  <div class="modal-dialog">
    <div class="modal-content theme-surface">
      <div class="modal-header">
        <h5 class="modal-title" id="${modalId}Label">${isEdit ? 'Edit' : 'New'} Material</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
      </div>
      <form id="materialForm">
      <div class="modal-body">
        <div class="mb-3">
          <label class="form-label">Name</label>
          <input type="text" class="form-control" name="name" value="${material.name || ''}" required />
        </div>
        <div class="mb-3">
          <label class="form-label">SKU</label>
          <input type="text" class="form-control" name="sku" value="${material.sku || ''}" required />
        </div>
        <div class="mb-3">
          <label class="form-label">Qty On Hand</label>
          <input type="number" class="form-control" name="qtyOnHand" value="${material.qtyOnHand || ''}" step="any" required />
        </div>
        <div class="mb-3">
          <label class="form-label">Unit Cost</label>
          <input type="number" class="form-control" name="unitCost" value="${material.unitCost || ''}" step="any" required />
        </div>
        <div class="mb-3">
          <label class="form-label">Reorder Point</label>
          <input type="number" class="form-control" name="reorderPoint" value="${material.reorderPoint || ''}" step="any" required />
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
        <button type="submit" class="btn btn-accent theme-btn">${isEdit ? 'Update' : 'Create'}</button>
      </div>
      </form>
    </div>
  </div>
</div>`;
    $modals.html(modalHtml);
    let modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();

    $('#materialForm').on('submit', function (e) {
        e.preventDefault();
        let formData = Object.fromEntries(new FormData(this).entries());
        formData.qtyOnHand = parseFloat(formData.qtyOnHand);
        formData.unitCost = parseFloat(formData.unitCost);
        formData.reorderPoint = parseFloat(formData.reorderPoint);
        let method = isEdit ? 'PUT' : 'POST';
        let url = isEdit ? `/api/FiberMaterials/${material.id}` : '/api/FiberMaterials';
        fetch(url, {
            method: method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        })
        .then(r => {
            if (!r.ok) throw new Error('Failed to save material');
            return r.json();
        })
        .then(() => {
            modal.hide();
            fiberflowToast(`Material ${isEdit ? 'updated' : 'created'}`, 'success');
            loadInventoryTable();
        })
        .catch(() => fiberflowToast('Failed to save material', 'error'));
    });
}
window.renderMaterialModal = renderMaterialModal;

// Delete material by ID
function deleteMaterial(materialId) {
    if (!confirm('Are you sure you want to delete this material?')) return;
    fetch(`/api/FiberMaterials/${materialId}`, {
        method: 'DELETE'
    })
    .then(r => {
        if (!r.ok) throw new Error('Failed to delete material');
        fiberflowToast('Material deleted', 'success');
        loadInventoryTable();
    })
    .catch(() => fiberflowToast('Failed to delete material', 'error'));
}
window.deleteMaterial = deleteMaterial;
