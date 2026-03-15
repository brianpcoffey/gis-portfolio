
// FiberFlow Orders JS
// Handles DataTable for orders and CRUD modals

let ordersTable;


// Use shared toast helper (window.fiberflowToast provided by dashboard script)

$(document).ready(function () {
    loadOrdersTable();
    $('#btnNewOrder').on('click', function () {
        showOrderModal();
    });
});

function formatCurrency(val) {
    if (typeof val !== 'number') val = parseFloat(val);
    if (isNaN(val)) return '';
    return '$' + val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function loadOrdersTable() {
    if (typeof $.fn.DataTable !== 'function') {
        fiberflowToast('DataTables library is not loaded. Please check your script order.', 'error');
        return;
    }
    fetch('/api/FiberOrders')
        .then(r => r.json())
        .then(data => {
            if (ordersTable) {
                ordersTable.clear().rows.add(data).draw();
                return;
            }
            ordersTable = $('#fiberflowOrdersTable').DataTable({
                data: data,
                columns: [
                    { title: 'Order #', data: 'OrderNumber' },
                    { title: 'Client', data: 'ClientName' },
                    { title: 'Product', data: 'ProductName' },
                    { title: 'Qty', data: 'Quantity', className: 'text-end' },
                    { title: 'Unit Price', data: 'UnitPrice', render: function(d) { return formatCurrency(d); }, className: 'text-end' },
                    { title: 'Status', data: 'Status' },
                    { title: 'Order Date', data: 'OrderDate', render: d => d ? new Date(d).toLocaleDateString() : '', className: 'text-nowrap' },
                    { title: 'Ship Date', data: 'ShipDate', render: d => d ? new Date(d).toLocaleDateString() : '', className: 'text-nowrap' },
                    {
                        title: '',
                        data: null,
                        orderable: false,
                        className: 'text-end',
                        render: function (data, type, row) {
                            return `<button class="btn btn-sm btn-outline-primary me-1" onclick="showOrderModal(${row.Id})"><i class='fa fa-edit'></i></button>
                                    <button class="btn btn-sm btn-outline-danger" onclick="deleteOrder(${row.Id})"><i class='fa fa-trash'></i></button>`;
                        }
                    }
                ],
                order: [[6, 'desc']],
                responsive: true,
                autoWidth: false,
                language: { emptyTable: 'No orders found.' }
            });
        });
}

// Show modal for create/edit order
window.showOrderModal = function (orderId) {
    let isEdit = !!orderId;
    let modalId = 'fiberflowOrderModal';
    let $modals = $('#fiberflowModals');
    $modals.empty();
    let order = null;
    if (isEdit) {
        // Fetch order details
        fetch(`/api/FiberOrders/${orderId}`)
            .then(r => r.json())
            .then(data => {
                order = data;
                renderOrderModal(order, isEdit, modalId, $modals);
            });
    } else {
        renderOrderModal({}, false, modalId, $modals);
    }
};

function renderOrderModal(order, isEdit, modalId, $modals) {
    let modalHtml = `
<div class="modal fade" id="${modalId}" tabindex="-1" aria-labelledby="${modalId}Label" aria-modal="true" role="dialog">
  <div class="modal-dialog modal-lg">
    <div class="modal-content theme-surface">
      <div class="modal-header">
        <h5 class="modal-title" id="${modalId}Label">${isEdit ? 'Edit' : 'New'} Order</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
      </div>
      <form id="orderForm">
      <div class="modal-body">
        <div class="row g-3">
          <div class="col-md-6">
            <label class="form-label">Client Name</label>
            <input type="text" class="form-control" name="ClientName" value="${order.ClientName || ''}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Product Name</label>
            <input type="text" class="form-control" name="ProductName" value="${order.ProductName || ''}" required />
          </div>
          <div class="col-md-4">
            <label class="form-label">Quantity</label>
            <input type="number" class="form-control" name="Quantity" value="${order.Quantity || ''}" min="1" required />
          </div>
          <div class="col-md-4">
            <label class="form-label">Unit Price</label>
            <input type="number" class="form-control" name="UnitPrice" value="${order.UnitPrice || ''}" min="0" step="0.01" required />
          </div>
          <div class="col-md-4">
            <label class="form-label">Status</label>
            <select class="form-select" name="Status" required>
              ${['Draft','Confirmed','In Production','Shipped','Delivered'].map(s => `<option value="${s}"${order.Status === s ? ' selected' : ''}>${s}</option>`).join('')}
            </select>
          </div>
          <div class="col-md-6">
            <label class="form-label">Order Date</label>
            <input type="date" class="form-control" name="OrderDate" value="${order.OrderDate ? order.OrderDate.substring(0,10) : ''}" required />
          </div>
          <div class="col-md-6">
            <label class="form-label">Ship Date</label>
            <input type="date" class="form-control" name="ShipDate" value="${order.ShipDate ? order.ShipDate.substring(0,10) : ''}" required />
          </div>
        </div>
      </div>
      <div class="modal-footer">
        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
        <button type="submit" class="btn btn-accent theme-btn">${isEdit ? 'Save Changes' : 'Create Order'}</button>
      </div>
      </form>
    </div>
  </div>
</div>`;
    $modals.html(modalHtml);
    let modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();

    $('#orderForm').on('submit', function (e) {
        e.preventDefault();
        let formData = Object.fromEntries(new FormData(this).entries());
        formData.Quantity = parseInt(formData.Quantity);
        formData.UnitPrice = parseFloat(formData.UnitPrice);
        formData.OrderDate = formData.OrderDate;
        formData.ShipDate = formData.ShipDate;
        let method = isEdit ? 'PUT' : 'POST';
        let url = isEdit ? `/api/FiberOrders/${order.Id}` : '/api/FiberOrders';
        fetch(url, {
            method: method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        })
        .then(r => {
            if (!r.ok) throw new Error('Failed to save order');
            return r.json();
        })
        .then(() => {
            modal.hide();
            fiberflowToast(isEdit ? 'Order updated' : 'Order created', 'success');
            loadOrdersTable();
        })
        .catch(() => {
            fiberflowToast('Failed to save order', 'error');
        });
    });
}

// Delete order
window.deleteOrder = function (orderId) {
    if (!confirm('Delete this order?')) return;
    fetch(`/api/FiberOrders/${orderId}`, { method: 'DELETE' })
        .then(r => {
            if (!r.ok) throw new Error('Failed to delete order');
            fiberflowToast('Order deleted', 'success');
            loadOrdersTable();
        })
        .catch(() => fiberflowToast('Failed to delete order', 'error'));
};
