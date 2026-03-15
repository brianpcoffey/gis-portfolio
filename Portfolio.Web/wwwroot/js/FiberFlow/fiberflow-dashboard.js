
// FiberFlow Dashboard JS
// Handles dashboard stats, D3 charts, and ArcGIS map



// Shared toast helper (matches HomeFinder implementation)
function showToast(message, type = "success") {
    const container = document.getElementById("toastContainer") || createToastContainer();
    const iconMap = {
        success: "fa-circle-check",
        danger: "fa-circle-xmark",
        warning: "fa-triangle-exclamation",
        info: "fa-circle-info"
    };
    const icon = iconMap[type] || iconMap.info;

    const toast = document.createElement("div");
    toast.className = `toast align-items-center text-bg-${type} border-0 show`;
    toast.setAttribute("role", "alert");
    toast.setAttribute("aria-live", "assertive");
    toast.setAttribute("aria-atomic", "true");
    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                <i class="fa-solid ${icon} me-1"></i>${message}
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto"
                    data-bs-dismiss="toast" aria-label="Close"></button>
        </div>`;
    container.appendChild(toast);

    // Animate in
    requestAnimationFrame(() => toast.classList.add("toast-slide-in"));

    setTimeout(() => {
        toast.classList.add("toast-slide-out");
        toast.addEventListener("transitionend", () => toast.remove(), { once: true });
        // Fallback removal if transition doesn't fire
        setTimeout(() => toast.remove(), 500);
    }, 4000);
}

function createToastContainer() {
    const c = document.createElement("div");
    c.id = "toastContainer";
    c.className = "toast-container position-fixed bottom-0 end-0 p-3";
    c.style.zIndex = "1090";
    document.body.appendChild(c);
    return c;
}

// Provide legacy alias used by existing FiberFlow scripts
window.fiberflowToast = function (message, type) {
    const mapped = type === 'error' ? 'danger' : type;
    showToast(message, mapped);
};


// Ensure loadFiberflowMap is always defined to avoid ReferenceError
if (typeof loadFiberflowMap !== 'function') {
    window.loadFiberflowMap = function () {};
}

$(document).ready(function () {
    loadDashboardStats();
    loadFiberflowMap();
});

function loadDashboardStats() {
    $('#fiberflowRevenueChart .fiberflow-spinner').removeClass('d-none');
    fetch('/api/FiberDashboard/stats')
        .then(r => r.json())
        .then(data => {
            updateDashboardBadges(data);
            renderRevenueChart(data.RevenueByMonth || []);
            fiberflowToast('Dashboard loaded', 'success');
        })
        .catch(() => {
            $('#fiberflowRevenueChart').html('<div class="text-danger small">Failed to load dashboard data.</div>');
            fiberflowToast('Failed to load dashboard', 'error');
        })
        .finally(() => {
            $('#fiberflowRevenueChart .fiberflow-spinner').addClass('d-none');
        });
}

function updateDashboardBadges(data) {
    $('#badgeActiveShipments').text(`${data.ActiveShipments ?? 0} Active Shipments`);
    $('#badgeOpenOrders').text(`${data.OpenOrders ?? 0} Open Orders`);
    $('#badgeLowStock').text(`${data.LowStockAlerts ?? 0} Low Stock`);
}

function renderRevenueChart(revenueByMonth) {
    const container = d3.select('#fiberflowRevenueChart');
    container.selectAll('*:not(.fiberflow-spinner)').remove();
    if (!revenueByMonth.length) {
        container.append('div').attr('class', 'text-muted small').text('No revenue data.');
        return;
    }
    // D3 bar chart
    const margin = { top: 24, right: 24, bottom: 40, left: 60 };
    const width = container.node().clientWidth - margin.left - margin.right;
    const height = 320 - margin.top - margin.bottom;
    const svg = container.append('svg')
        .attr('width', width + margin.left + margin.right)
        .attr('height', height + margin.top + margin.bottom)
        .append('g')
        .attr('transform', `translate(${margin.left},${margin.top})`);

    const months = revenueByMonth.map(d => d.Month);
    const values = revenueByMonth.map(d => d.Revenue);
    const x = d3.scaleBand().domain(months).range([0, width]).padding(0.2);
    const y = d3.scaleLinear().domain([0, d3.max(values) * 1.1]).range([height, 0]);

    svg.append('g')
        .attr('transform', `translate(0,${height})`)
        .call(d3.axisBottom(x))
        .selectAll('text')
        .attr('class', 'small')
        .attr('fill', 'var(--text-muted)');

    svg.append('g')
        .call(d3.axisLeft(y).ticks(6).tickFormat(d3.format('$,.0f')))
        .selectAll('text')
        .attr('class', 'small')
        .attr('fill', 'var(--text-muted)');

    svg.selectAll('.bar')
        .data(revenueByMonth)
        .enter()
        .append('rect')
        .attr('class', 'bar')
        .attr('x', d => x(d.Month))
        .attr('y', d => y(d.Revenue))
        .attr('width', x.bandwidth())
        .attr('height', d => height - y(d.Revenue))
        .attr('fill', 'var(--accent)');

    svg.append('text')
        .attr('x', width / 2)
        .attr('y', height + margin.bottom - 5)
        .attr('text-anchor', 'middle')
        .attr('fill', 'var(--text-muted)')
        .attr('class', 'small')
        .text('Month');
    svg.append('text')
        .attr('transform', 'rotate(-90)')
        .attr('y', -margin.left + 16)
        .attr('x', -height / 2)
        .attr('text-anchor', 'middle')
        .attr('fill', 'var(--text-muted)')
        .attr('class', 'small')
        .text('Revenue');
}
