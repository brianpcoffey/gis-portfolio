// Plant Operations Dashboard - Shipments JS
// Handles DataTable for shipments and GIS routing map

let shipmentsTable;


// Use shared toast helper (global showToast from toasts.js; types: success, danger, warning, info)


$(document).ready(function () {
    loadShipmentsTable();
    $(document).on('click', '#btnNewShipment', function () {
        showShipmentModal();
    });
    // Row actions (delegated; replaces inline onclick handlers). The status is read back
    // from data-status, which was written with setAttribute, never concatenated into script.
    $(document).on('click', '#plantOpsShipmentsTable [data-action]', function () {
        const id = Number($(this).attr('data-id'));
        switch ($(this).attr('data-action')) {
            case 'edit': showShipmentModal(id); break;
            case 'delete': deleteShipment(id); break;
            case 'status': showShipmentStatusModal(id, $(this).attr('data-status')); break;
        }
    });
});

function loadShipmentsTable() {
    if (typeof $.fn.DataTable !== 'function') {
        showToast('DataTables library is not loaded. Please check your script order.', 'danger');
        return;
    }
    const loadError = plantOpsAlert('shipmentsTableError');
    loadError.hide();
    window.apiFetch(window.PortfolioApi.routes.fiber.shipments)
        .then(plantOpsJson)
        .then(data => {
            if (shipmentsTable) {
                shipmentsTable.clear().rows.add(data).draw();
                return;
            }
            shipmentsTable = $('#plantOpsShipmentsTable').DataTable({
                data: data,
                columns: [
                    { title: 'Tracking #', data: 'trackingNumber', render: $.fn.dataTable.render.text() },
                    { title: 'Carrier', data: 'carrierName', render: $.fn.dataTable.render.text() },
                    { title: 'Status', data: 'status', render: $.fn.dataTable.render.text() },
                    { title: 'Destination', data: null, render: d => `${PortfolioDom.escapeHtml(d.destinationCity)}, ${PortfolioDom.escapeHtml(d.destinationState)}` },
                    { title: 'ETA', data: 'estimatedArrival', render: d => d ? new Date(d).toLocaleString() : '', className: 'text-nowrap' },
                    {
                        title: '',
                        data: null,
                        orderable: false,
                        className: 'text-end',
                        render: function (data, type, row) {
                            // The icons carry no text, so each button needs a name that also
                            // identifies its row — otherwise every row reads as the same "Edit".
                            let rowLabel = PortfolioDom.escapeHtml(row.trackingNumber || row.id);
                            // The status is user-saved free text, so that button is built as a DOM
                            // node (data-status via setAttribute) and serialised, which escapes it.
                            const statusBtn = document.createElement('button');
                            statusBtn.className = 'btn btn-sm btn-outline-secondary';
                            statusBtn.setAttribute('data-action', 'status');
                            statusBtn.setAttribute('data-id', Number(row.id));
                            statusBtn.setAttribute('data-status', row.status == null ? '' : row.status);
                            statusBtn.setAttribute('aria-label', 'Update status of shipment ' + (row.trackingNumber || row.id));
                            statusBtn.append(PortfolioDom.el('i', { class: 'fa fa-truck', 'aria-hidden': 'true' }));
                            return `
                                <button class="btn btn-sm btn-outline-primary me-1" data-action="edit" data-id="${Number(row.id)}" aria-label="Edit shipment ${rowLabel}"><i class='fa fa-edit' aria-hidden="true"></i></button>
                                <button class="btn btn-sm btn-outline-danger me-1" data-action="delete" data-id="${Number(row.id)}" aria-label="Delete shipment ${rowLabel}"><i class='fa fa-trash' aria-hidden="true"></i></button>
                                ${statusBtn.outerHTML}
                            `;
                        }
                    }
                ],
                order: [[4, 'asc']],
                responsive: true,
                autoWidth: false,
                language: { emptyTable: 'No shipments found.' }
            });
        })
        .catch(err => loadError.show(plantOpsFailure('Failed to load shipments', err)));
}

// Show modal for updating shipment status
window.showShipmentStatusModal = function (shipmentId, currentStatus) {
    let modalId = 'plantOpsShipmentStatusModal';
    let $modals = $('#plantOpsModals');
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
          ${['Pending','In Transit','Delivered','Exception'].map(s => `<option value="${s}"${currentStatus === s ? ' selected' : ''}>${s}</option>`).join('')}
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
        window.apiFetch(`${window.PortfolioApi.routes.fiber.shipments}/${shipmentId}/status`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        })
        .then(plantOpsJson)
        .then(() => {
            modal.hide();
            showToast('Shipment status updated', 'success');
            loadShipmentsTable();
        })
        .catch(err => {
            showToast(plantOpsFailure('Failed to update shipment status', err), 'danger');
        });
    });
};

function showShipmentModal(shipmentId) {
    let isEdit = !!shipmentId;
    let modalId = 'plantOpsShipmentModal';
    let $modals = $('#plantOpsModals');
    $modals.empty();
    let shipment = null;
    if (isEdit) {
        window.apiFetch(`${window.PortfolioApi.routes.fiber.shipments}/${shipmentId}`)
            .then(plantOpsJson)
            .then(data => {
                shipment = data;
                renderShipmentModal(shipment, isEdit, modalId, $modals);
            })
            .catch(err => showToast(plantOpsFailure('Failed to load shipment', err), 'danger'));
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
            <input type="text" class="form-control" name="trackingNumber" value="${PortfolioDom.escapeHtml(shipment.trackingNumber)}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Carrier Name</label>
            <input type="text" class="form-control" name="carrierName" value="${PortfolioDom.escapeHtml(shipment.carrierName)}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Status</label>
            <select class="form-select" name="status" required>
              ${['Pending','In Transit','Delivered','Exception'].map(s => `<option value="${s}"${shipment.status === s ? ' selected' : ''}>${s}</option>`).join('')}
            </select>
          </div>
          <div class="col-md-6">
            <label class="form-label">Destination City</label>
            <input type="text" class="form-control" name="destinationCity" value="${PortfolioDom.escapeHtml(shipment.destinationCity)}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Destination State</label>
            <input type="text" class="form-control" name="destinationState" value="${PortfolioDom.escapeHtml(shipment.destinationState)}" required />
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
        let url = isEdit ? `${window.PortfolioApi.routes.fiber.shipments}/${shipment.id}` : window.PortfolioApi.routes.fiber.shipments;
        window.apiFetch(url, {
            method: method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        })
        .then(plantOpsJson)
        .then(() => {
            modal.hide();
            showToast(`Shipment ${isEdit ? 'updated' : 'created'}`, 'success');
            loadShipmentsTable();
        })
        .catch(err => showToast(plantOpsFailure('Failed to save shipment', err), 'danger'));
    });
}
window.renderShipmentModal = renderShipmentModal;

// Delete shipment by ID
async function deleteShipment(shipmentId) {
    if (!await confirmDialog('Are you sure you want to delete this shipment?')) return;
    window.apiFetch(`${window.PortfolioApi.routes.fiber.shipments}/${shipmentId}`, {
        method: 'DELETE'
    })
    .then(plantOpsOk)
    .then(() => {
        showToast('Shipment deleted', 'success');
        loadShipmentsTable();
    })
    .catch(err => showToast(plantOpsFailure('Failed to delete shipment', err), 'danger'));
}
window.deleteShipment = deleteShipment;
