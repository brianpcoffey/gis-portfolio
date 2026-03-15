
// FiberFlow Shipments JS
// Handles DataTable for shipments and GIS routing map

let shipmentsTable;


// Toast utility (shared with dashboard)
function fiberflowToast(message, type = 'info') {
    const id = 'toast' + Date.now();
    const icon = type === 'success' ? 'fa-check-circle' : type === 'error' ? 'fa-exclamation-triangle' : 'fa-info-circle';
    const bg = type === 'success' ? 'bg-success' : type === 'error' ? 'bg-danger' : 'bg-primary';
    const html = `<div id="${id}" class="toast align-items-center text-white ${bg} border-0 mb-2" role="alert" aria-live="assertive" aria-atomic="true">
        <div class="d-flex">
            <div class="toast-body"><i class="fa ${icon} me-2"></i>${message}</div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
    </div>`;
    $('#fiberflowToastContainer').append(html);
    const toast = new bootstrap.Toast(document.getElementById(id), { delay: 3500 });
    toast.show();
    toast._element.addEventListener('hidden.bs.toast', function () { $(this).remove(); });
}

$(document).ready(function () {
    loadShipmentsTable();
});

function loadShipmentsTable() {
    if (typeof $.fn.DataTable !== 'function') {
        fiberflowToast('DataTables library is not loaded. Please check your script order.', 'error');
        return;
    }
    fetch('/api/FiberShipments')
        .then(r => r.json())
        .then(data => {
            if (shipmentsTable) {
                shipmentsTable.clear().rows.add(data).draw();
                return;
            }
            shipmentsTable = $('#fiberflowShipmentsTable').DataTable({
                data: data,
                columns: [
                    { title: 'Tracking #', data: 'TrackingNumber' },
                    { title: 'Carrier', data: 'CarrierName' },
                    { title: 'Status', data: 'Status' },
                    { title: 'Destination', data: null, render: d => `${d.DestinationCity}, ${d.DestinationState}` },
                    { title: 'ETA', data: 'EstimatedArrival', render: d => d ? new Date(d).toLocaleString() : '', className: 'text-nowrap' },
                    {
                        title: '',
                        data: null,
                        orderable: false,
                        className: 'text-end',
                        render: function (data, type, row) {
                            return `<button class="btn btn-sm btn-outline-primary" onclick="showShipmentStatusModal(${row.Id}, '${row.Status}')"><i class='fa fa-edit'></i></button>`;
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
        <select class="form-select" name="Status" required>
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
