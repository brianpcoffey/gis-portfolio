// Plant Operations Dashboard - Inventory JS
// Handles DataTable for inventory and low stock alerts

let inventoryTable;


// Use shared toast helper (global showToast from toasts.js; types: success, danger, warning, info)


$(document).ready(function () {
    loadInventoryTable();
    $(document).on('click', '#btnNewMaterial', function () {
        showMaterialModal();
    });
    // Row and detail-modal actions (delegated; replaces inline onclick handlers)
    $(document).on('click', '#plantOpsInventoryTable [data-action], #plantOpsModals [data-action]', function () {
        const id = Number($(this).attr('data-id'));
        switch ($(this).attr('data-action')) {
            case 'edit': showMaterialModal(id); break;
            case 'receive': showReceiveStockModal(id); break;
            case 'delete': deleteMaterial(id); break;
        }
    });
});

function loadInventoryTable() {
    const loadError = plantOpsAlert('inventoryTableError');
    loadError.hide();
    $('#inventoryTableSpinner').removeClass('d-none');
    window.apiFetch(window.PortfolioApi.routes.fiber.materials)
        .then(plantOpsJson)
        .then(data => {
            if (inventoryTable) {
                inventoryTable.clear().rows.add(data).draw();
                showToast('Inventory loaded', 'success');
                return;
            }
            inventoryTable = $('#plantOpsInventoryTable').DataTable({
                data: data,
                columns: [
                    { title: 'Material', data: 'name', render: $.fn.dataTable.render.text() },
                    { title: 'SKU', data: 'sku', render: $.fn.dataTable.render.text() },
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
                            // title= alone gave every row the same accessible name ("Edit"),
                            // so the row identity is carried by aria-label; title stays purely
                            // as the mouse tooltip.
                            let rowLabel = PortfolioDom.escapeHtml(row.name || row.sku || row.id);
                            return `
                                <button class="btn btn-sm btn-outline-primary me-1" data-action="edit" data-id="${Number(row.id)}" title="Edit" aria-label="Edit material ${rowLabel}"><i class='fa fa-edit' aria-hidden="true"></i></button>
                                <button class="btn btn-sm btn-outline-success me-1" data-action="receive" data-id="${Number(row.id)}" title="Receive Stock" aria-label="Receive stock for material ${rowLabel}"><i class='fa fa-arrow-down' aria-hidden="true"></i></button>
                                <button class="btn btn-sm btn-outline-danger me-1" data-action="delete" data-id="${Number(row.id)}" title="Delete" aria-label="Delete material ${rowLabel}"><i class='fa fa-trash' aria-hidden="true"></i></button>
                                ${row.isLowStock ? '<span class="badge bg-danger plant-ops-low-stock">Low</span>' : ''}
                            `;
                        }
                    }
                ],
                rowCallback: function (row, data) {
                    if (data.isLowStock) {
                        $(row).addClass('plant-ops-low-stock');
                    }
                },
                order: [[2, 'asc']],
                responsive: true,
                autoWidth: false,
                language: { emptyTable: 'No inventory found.' }
            });
            showToast('Inventory loaded', 'success');
        })
        .catch(err => {
            const message = plantOpsFailure('Failed to load inventory', err);
            loadError.show(message);
            showToast(message, 'danger');
        })
        .finally(() => $('#inventoryTableSpinner').addClass('d-none'));
}

function showMaterialDetailModal(material) {
    let modalId = 'plantOpsMaterialDetailModal';
    let $modals = $('#plantOpsModals');
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
          <dt class="col-5">Name</dt><dd class="col-7">${PortfolioDom.escapeHtml(material.name)}</dd>
          <dt class="col-5">SKU</dt><dd class="col-7">${PortfolioDom.escapeHtml(material.sku)}</dd>
          <dt class="col-5">Qty On Hand</dt><dd class="col-7">${material.qtyOnHand}</dd>
          <dt class="col-5">Unit Cost</dt><dd class="col-7">$${material.unitCost.toFixed(2)}</dd>
          <dt class="col-5">Total Value</dt><dd class="col-7">$${material.totalValue.toFixed(2)}</dd>
          <dt class="col-5">Reorder Point</dt><dd class="col-7">${material.reorderPoint}</dd>
        </dl>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
        <button type="button" class="btn btn-accent" data-action="receive" data-id="${Number(material.id)}">Receive Stock</button>
      </div>
    </div>
  </div>
</div>`;
    $modals.html(modalHtml);
    let modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();
}

window.showReceiveStockModal = function (materialId) {
    let modalId = 'plantOpsReceiveStockModal';
    let $modals = $('#plantOpsModals');
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
        <button type="submit" class="btn btn-accent">Receive</button>
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
        window.apiFetch(`${window.PortfolioApi.routes.fiber.materials}/${materialId}/receive`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        })
        .then(plantOpsJson)
        .then(() => {
            modal.hide();
            showToast('Stock received', 'success');
            loadInventoryTable();
        })
        .catch(err => showToast(plantOpsFailure('Failed to receive stock', err), 'danger'));
    });
};

function showMaterialModal(materialId) {
    let isEdit = !!materialId;
    let modalId = 'plantOpsMaterialModal';
    let $modals = $('#plantOpsModals');
    $modals.empty();
    let material = null;
    if (isEdit) {
        window.apiFetch(`${window.PortfolioApi.routes.fiber.materials}/${materialId}`)
            .then(plantOpsJson)
            .then(data => {
                material = data;
                renderMaterialModal(material, isEdit, modalId, $modals);
            })
            .catch(err => showToast(plantOpsFailure('Failed to load material', err), 'danger'));
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
          <input type="text" class="form-control" name="name" value="${PortfolioDom.escapeHtml(material.name)}" required />
        </div>
        <div class="mb-3">
          <label class="form-label">SKU</label>
          <input type="text" class="form-control" name="sku" value="${PortfolioDom.escapeHtml(material.sku)}" required />
        </div>
        <div class="mb-3">
          <label class="form-label">Category</label>
          <input type="text" class="form-control" name="category" value="${PortfolioDom.escapeHtml(material.category)}" list="materialCategoryOptions" placeholder="e.g. Reinforcements, Resins" />
          <datalist id="materialCategoryOptions">
            <option value="Reinforcements"></option>
            <option value="Resins"></option>
            <option value="Coatings"></option>
            <option value="Hardware"></option>
            <option value="Consumables"></option>
          </datalist>
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
        <button type="submit" class="btn btn-accent">${isEdit ? 'Update' : 'Create'}</button>
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
        let url = isEdit ? `${window.PortfolioApi.routes.fiber.materials}/${material.id}` : window.PortfolioApi.routes.fiber.materials;
        window.apiFetch(url, {
            method: method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        })
        .then(plantOpsJson)
        .then(() => {
            modal.hide();
            showToast(`Material ${isEdit ? 'updated' : 'created'}`, 'success');
            loadInventoryTable();
        })
        .catch(err => showToast(plantOpsFailure('Failed to save material', err), 'danger'));
    });
}
window.renderMaterialModal = renderMaterialModal;

async function deleteMaterial(materialId) {
    if (!await confirmDialog('Are you sure you want to delete this material?')) return;
    window.apiFetch(`${window.PortfolioApi.routes.fiber.materials}/${materialId}`, {
        method: 'DELETE'
    })
    .then(plantOpsOk)
    .then(() => {
        showToast('Material deleted', 'success');
        loadInventoryTable();
    })
    .catch(err => showToast(plantOpsFailure('Failed to delete material', err), 'danger'));
}
window.deleteMaterial = deleteMaterial;
