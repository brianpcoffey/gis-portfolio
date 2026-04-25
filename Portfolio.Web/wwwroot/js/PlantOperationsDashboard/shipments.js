// FiberFlow Shipments JS
// Handles DataTable for shipments and GIS routing map

let shipmentsTable;


// Use shared toast helper (window.fiberflowToast provided by dashboard script)


$(document).ready(function () {
    loadShipmentsTable();
    $(document).on('click', '#btnNewShipment', function () {
        showShipmentModal();
    });
});

function loadShipmentsTable() {
    if (typeof $.fn.DataTable !== 'function') {
        fiberflowToast('DataTables library is not loaded. Please check your script order.', 'error');
        return;
    }
    fetchWithAuth('/api/FiberShipments')
        .then(r => r.json())
        .then(data => {
            if (shipmentsTable) {
                shipmentsTable.clear().rows.add(data).draw();
                return;
            }
            shipmentsTable = $('#fiberflowShipmentsTable').DataTable({
                data: data,
                columns: [
                    { title: 'Tracking #', data: 'trackingNumber' },
                    { title: 'Carrier', data: 'carrierName' },
                    { title: 'Status', data: 'status' },
                    { title: 'Destination', data: null, render: d => `${d.destinationCity}, ${d.destinationState}` },
                    { title: 'ETA', data: 'estimatedArrival', render: d => d ? new Date(d).toLocaleString() : '', className: 'text-nowrap' },
                    {
                        title: '',
                        data: null,
                        orderable: false,
                        className: 'text-end',
                        render: function (data, type, row) {
                            return `
                                <button class="btn btn-sm btn-outline-primary me-1" onclick="showShipmentModal(${row.id})"><i class='fa fa-edit'></i></button>
                                <button class="btn btn-sm btn-outline-danger me-1" onclick="deleteShipment(${row.id})"><i class='fa fa-trash'></i></button>
                                <button class="btn btn-sm btn-outline-secondary" onclick="showShipmentStatusModal(${row.id}, '${row.status}')"><i class='fa fa-truck'></i></button>
                            `;
                        }
                    }
                ],
                order: [[4, 'asc']],
                responsive: true,
                autoWidth: false,
                language: { emptyTable: 'No shipments found.' }
            });
        });
}

// Show modal for updating shipment status
window.showShipmentStatusModal = function (shipmentId, currentStatus) {
    let modalId = 'fiberflowShipmentStatusModal';
    let $modals = $('#fiberflowModals');
    $modals.empty();
    let modalHtml = `
<div class="modal fade" id="${modalId}" tabindex="-1" aria-labelledby="${modalId}Label" aria-modal="true" role="dialog">
  <div class="modal-dialog">
    <div class="modal-content theme-surface">
      <div class="modal-header">
        <h5 class="modal-title" id="${modalId}Label">Update Shipment Status</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
      </div>
      <form id="shipmentStatusForm">
      <div class="modal-body">
        <label class="form-label">Status</label>
        <select class="form-select" name="status" required>
          ${['Draft','Confirmed','In Production','Shipped','Delivered'].map(s => `<option value="${s}"${currentStatus === s ? ' selected' : ''}>${s}</option>`).join('')}
        </select>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
        <button type="submit" class="btn btn-accent theme-btn">Update Status</button>
      </div>
      </form>
    </div>
  </div>
</div>`;
    $modals.html(modalHtml);
    let modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();

    $('#shipmentStatusForm').on('submit', function (e) {
        e.preventDefault();
        let formData = Object.fromEntries(new FormData(this).entries());
        fetch(`/api/FiberShipments/${shipmentId}/status`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        })
        .then(r => {
            if (!r.ok) throw new Error('Failed to update status');
            return r.json();
        })
        .then(() => {
            modal.hide();
            fiberflowToast('Shipment status updated', 'success');
            loadShipmentsTable();
        })
        .catch(() => {
            fiberflowToast('Failed to update shipment status', 'error');
        });
    });
};

function showShipmentModal(shipmentId) {
    let isEdit = !!shipmentId;
    let modalId = 'fiberflowShipmentModal';
    let $modals = $('#fiberflowModals');
    $modals.empty();
    let shipment = null;
    if (isEdit) {
        fetch(`/api/FiberShipments/${shipmentId}`)
            .then(r => r.json())
            .then(data => {
                shipment = data;
                renderShipmentModal(shipment, isEdit, modalId, $modals);
            });
    } else {
        renderShipmentModal({}, false, modalId, $modals);
    }
}
window.showShipmentModal = showShipmentModal;
function renderShipmentModal(shipment, isEdit, modalId, $modals) {
    let modalHtml = `
<div class="modal fade" id="${modalId}" tabindex="-1" aria-labelledby="${modalId}Label" aria-modal="true" role="dialog">
  <div class="modal-dialog modal-lg">
    <div class="modal-content theme-surface">
      <div class="modal-header">
        <h5 class="modal-title" id="${modalId}Label">${isEdit ? 'Edit' : 'New'} Shipment</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
      </div>
      <form id="shipmentForm">
      <div class="modal-body">
        <div class="row g-3">
          <div class="col-md-6">
            <label class="form-label">Tracking Number</label>
            <input type="text" class="form-control" name="trackingNumber" value="${shipment.trackingNumber || ''}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Carrier Name</label>
            <input type="text" class="form-control" name="carrierName" value="${shipment.carrierName || ''}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Status</label>
            <select class="form-select" name="status" required>
              ${['Draft','Confirmed','In Production','Shipped','Delivered'].map(s => `<option value="${s}"${shipment.status === s ? ' selected' : ''}>${s}</option>`).join('')}
            </select>
          </div>
          <div class="col-md-6">
            <label class="form-label">Destination City</label>
            <input type="text" class="form-control" name="destinationCity" value="${shipment.destinationCity || ''}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Destination State</label>
            <input type="text" class="form-control" name="destinationState" value="${shipment.destinationState || ''}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Estimated Arrival</label>
            <input type="datetime-local" class="form-control" name="estimatedArrival" value="${shipment.estimatedArrival ? new Date(shipment.estimatedArrival).toISOString().slice(0,16) : ''}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Origin Lat</label>
            <input type="number" class="form-control" name="originLat" value="${shipment.originLat || ''}" step="any" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Origin Lng</label>
            <input type="number" class="form-control" name="originLng" value="${shipment.originLng || ''}" step="any" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Destination Lat</label>
            <input type="number" class="form-control" name="destinationLat" value="${shipment.destinationLat || ''}" step="any" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Destination Lng</label>
            <input type="number" class="form-control" name="destinationLng" value="${shipment.destinationLng || ''}" step="any" required />
          </div>
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

    $('#shipmentForm').on('submit', function (e) {
        e.preventDefault();
        let formData = Object.fromEntries(new FormData(this).entries());
        formData.originLat = parseFloat(formData.originLat);
        formData.originLng = parseFloat(formData.originLng);
        formData.destinationLat = parseFloat(formData.destinationLat);
        formData.destinationLng = parseFloat(formData.destinationLng);
        formData.estimatedArrival = new Date(formData.estimatedArrival).toISOString();
        let method = isEdit ? 'PUT' : 'POST';
        let url = isEdit ? `/api/FiberShipments/${shipment.id}` : '/api/FiberShipments';
        fetch(url, {
            method: method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        })
        .then(r => {
            if (!r.ok) throw new Error('Failed to save shipment');
            return r.json();
        })
        .then(() => {
            modal.hide();
            fiberflowToast(`Shipment ${isEdit ? 'updated' : 'created'}`, 'success');
            loadShipmentsTable();
        })
        .catch(() => fiberflowToast('Failed to save shipment', 'error'));
    });
}
window.renderShipmentModal = renderShipmentModal;

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

// Delete shipment by ID
function deleteShipment(shipmentId) {
    if (!confirm('Are you sure you want to delete this shipment?')) return;
    fetch(`/api/FiberShipments/${shipmentId}`, {
        method: 'DELETE'
    })
    .then(r => {
        if (!r.ok) throw new Error('Failed to delete shipment');
        fiberflowToast('Shipment deleted', 'success');
        loadShipmentsTable();
    })
    .catch(() => fiberflowToast('Failed to delete shipment', 'error'));
}
window.deleteShipment = deleteShipment;
