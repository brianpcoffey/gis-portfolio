
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
function fiberflowToast(message, type) {
    const mapped = type === 'error' ? 'danger' : type;
    showToast(message, mapped);
}
window.fiberflowToast = fiberflowToast;

// Simple auth-aware fetch helper: if API responds 401, redirect to login flow
async function fetchWithAuth(url, options = {}) {
    const res = await fetch(url, options);
    if (res.status === 401) {
        const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
        window.location.href = `/Login?returnUrl=${returnUrl}`;
        return new Promise(() => {});
    }
    return res;
}


// Ensure loadFiberflowMap is always defined to avoid ReferenceError
// Implement map loader: initializes ArcGIS map and plots shipments
if (typeof loadFiberflowMap !== 'function') {
    window.loadFiberflowMap = async function () {
        const mapContainer = document.getElementById('fiberflowMap');
        if (!mapContainer || typeof require !== 'function') {
            return;
        }

        // show spinner (already present in markup)
        const spinner = mapContainer.querySelector('.fiberflow-spinner');
        if (spinner) spinner.classList.remove('d-none');

        await new Promise((resolve) => {
            require([
                'esri/Map',
                'esri/views/MapView',
                'esri/layers/GraphicsLayer',
                'esri/Graphic',
                'esri/geometry/Point',
                'esri/symbols/SimpleMarkerSymbol',
                'esri/PopupTemplate'
            ], async (Map, MapView, GraphicsLayer, Graphic, Point, SimpleMarkerSymbol, PopupTemplate) => {
                try {
                    const map = new Map({ basemap: 'dark-gray-vector' });
                    const graphicsLayer = new GraphicsLayer();
                    map.add(graphicsLayer);

                    const view = new MapView({
                        container: 'fiberflowMap',
                        map: map,
                        center: [-95.3698, 29.7604], // default to Houston
                        zoom: 6
                    });

                    // fetch shipments and plot
                    try {
                        const res = await fetch('/api/FiberShipments');
                        if (!res.ok) throw new Error('Failed to load shipments');
                        const shipments = await res.json();

                        graphicsLayer.removeAll();
                        shipments.forEach(s => {
                            // ensure numeric coords
                            const lon = Number(s.destinationLng ?? s.destinationLng ?? s.destinationLng);
                            const lat = Number(s.destinationLat ?? s.destinationLat ?? s.destinationLat);
                            const hasCoords = !Number.isNaN(lat) && !Number.isNaN(lon);
                            const point = hasCoords ? new Point({ longitude: lon, latitude: lat }) : null;

                            const color = s.status && s.status.toLowerCase().includes('in transit')
                                ? [76, 175, 80, 0.9]
                                : s.status && s.status.toLowerCase().includes('deliv')
                                    ? [33, 150, 243, 0.9]
                                    : [255, 193, 7, 0.9];

                            const symbol = new SimpleMarkerSymbol({
                                color: color,
                                size: 12,
                                outline: { color: [255, 255, 255], width: 1 }
                            });

                            const popup = new PopupTemplate({
                                title: `${s.trackingNumber || ''} — ${s.carrierName || ''}`,
                                content: `Status: ${s.status || ''}<br/>Dest: ${s.destinationCity || ''}, ${s.destinationState || ''}`
                            });

                            if (point) {
                                const graphic = new Graphic({ geometry: point, symbol: symbol, attributes: s, popupTemplate: popup });
                                graphicsLayer.add(graphic);
                            }
                        });

                        if (shipments.length > 0) {
                            const points = graphicsLayer.graphics.toArray();
                            if (points.length) view.goTo(points, { padding: 50 });
                        }
                    } catch (err) {
                        console.error('Failed to fetch/plot shipments', err);
                    }

                    view.when(() => resolve());
                } catch (err) {
                    console.error('ArcGIS init failed', err);
                    resolve();
                }
            });
        });

        // hide spinner if present
        const spinner2 = document.querySelector('#fiberflowMap .fiberflow-spinner');
        if (spinner2) spinner2.classList.add('d-none');
    };
}

$(document).ready(function () {
    loadDashboardStats();
    loadFiberflowMap();
});

function loadDashboardStats() {
    $('#fiberflowRevenueChart .fiberflow-spinner').removeClass('d-none');
    fetchWithAuth('/api/FiberDashboard/stats')
        .then(r => r.json())
        .then(data => {
            updateDashboardBadges(data);
            renderRevenueChart(data.revenueByMonth || []);
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
    $('#badgeActiveShipments').text(`${data.activeShipments ?? 0} Active Shipments`);
    $('#badgeOpenOrders').text(`${data.openOrders ?? 0} Open Orders`);
    $('#badgeLowStock').text(`${data.lowStockAlerts ?? 0} Low Stock`);
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

    const months = revenueByMonth.map(d => d.month);
    const values = revenueByMonth.map(d => d.revenue);
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
        .attr('x', d => x(d.month))
        .attr('y', d => y(d.revenue))
        .attr('width', x.bandwidth())
        .attr('height', d => height - y(d.revenue))
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
