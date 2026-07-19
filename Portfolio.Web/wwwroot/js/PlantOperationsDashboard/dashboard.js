// Plant Operations Dashboard - Dashboard JS
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

// Provide legacy alias used by existing Plant Ops scripts
function plantOpsToast(message, type) {
    const mapped = type === 'error' ? 'danger' : type;
    showToast(message, mapped);
}
window.plantOpsToast = plantOpsToast;

// Escapes a value for safe interpolation into HTML markup strings — covers both
// element text and double/single-quoted attribute contexts (DataTables cells
// rendered via innerHTML, and modal `value="..."` inputs). Prevents stored XSS
// from API/user-entered fields. Returns '' for null/undefined. Loaded before
// orders.js/inventory.js/shipments.js, so it is available to all of them.
function plantOpsEscape(value) {
    if (value === null || value === undefined) return '';
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}
window.plantOpsEscape = plantOpsEscape;



// Ensure loadPlantOpsMap is always defined to avoid ReferenceError
// Implement map loader: initializes ArcGIS map and plots shipments
if (typeof loadPlantOpsMap !== 'function') {
    let plantOpsMapView = null;
    let plantOpsMapInstance = null;
    let plantOpsGraphicsLayer = null;

    async function initPlantOpsMap() {
        const mapContainer = document.getElementById('plantOpsMap');
        if (!mapContainer || typeof require !== 'function') {
            plantOpsToast('Map container or ArcGIS library not loaded. Please check your network and script includes.', 'danger');
            return;
        }
        const spinner = mapContainer.querySelector('.plant-ops-spinner');
        if (spinner) spinner.remove();

        return new Promise((resolve, reject) => {
            require([
                'esri/Map',
                'esri/views/MapView',
                'esri/layers/GraphicsLayer'
            ], (Map, MapView, GraphicsLayer) => {
                // Use the same basemap options as HomeFinder
                const basemapSelect = document.getElementById('plantOpsBasemapSelect');
                const initialBasemap = basemapSelect ? basemapSelect.value : 'dark-gray-vector';
                plantOpsMapInstance = new Map({ basemap: initialBasemap });
                plantOpsGraphicsLayer = new GraphicsLayer();
                plantOpsMapInstance.add(plantOpsGraphicsLayer);

                // Center for North America (approximate)
                plantOpsMapView = new MapView({
                    container: 'plantOpsMap',
                    map: plantOpsMapInstance,
                    center: [-98.5795, 39.8283], // Center of North America (USA)
                    zoom: 4
                });

                // Wire up basemap switcher (same as HomeFinder)
                if (basemapSelect) {
                    basemapSelect.addEventListener('change', () => {
                        plantOpsMapInstance.basemap = basemapSelect.value;
                    });
                }

                const timeout = setTimeout(() => reject(new Error('Map timed out')), 15000);
                plantOpsMapView.when(() => {
                    clearTimeout(timeout);
                    resolve();
                }, err => {
                    clearTimeout(timeout);
                    reject(err);
                });
            });
        });
    }

    function plotPlantOpsShipments(shipments) {
        require([
            'esri/Graphic',
            'esri/geometry/Point',
            'esri/symbols/SimpleMarkerSymbol',
            'esri/PopupTemplate'
        ], (Graphic, Point, SimpleMarkerSymbol, PopupTemplate) => {
            plantOpsGraphicsLayer.removeAll();
            shipments.forEach(s => {
                const lon = Number(s.destinationLng);
                const lat = Number(s.destinationLat);
                if (isNaN(lat) || isNaN(lon) || (lat === 0 && lon === 0)) return;

                const color = s.status?.toLowerCase().includes('in transit')
                    ? [76, 175, 80, 0.9]
                    : s.status?.toLowerCase().includes('deliv')
                        ? [33, 150, 243, 0.9]
                        : [255, 193, 7, 0.9];

                plantOpsGraphicsLayer.add(new Graphic({
                    geometry: new Point({ longitude: lon, latitude: lat }),
                    symbol: new SimpleMarkerSymbol({
                        color, size: 12,
                        outline: { color: [255, 255, 255], width: 1 }
                    }),
                    attributes: s,
                    popupTemplate: new PopupTemplate({
                        title: `${s.trackingNumber || ''} — ${s.carrierName || ''}`,
                        content: `Status: ${s.status || ''}<br/>Dest: ${s.destinationCity || ''}, ${s.destinationState || ''}`
                    })
                }));
            });
            const points = plantOpsGraphicsLayer.graphics.toArray();
            if (points.length) {
                plantOpsMapView.goTo(points, { padding: 50 });
            } else {
                plantOpsToast('No shipments with coordinates found.', 'warning');
            }
        });
    }

    window.loadPlantOpsMap = function () {
        initPlantOpsMap()
            .then(() => window.apiFetch(window.PortfolioApi.routes.fiber.shipments))
            .then(res => res.ok ? res.json() : Promise.reject('Failed to load shipments'))
            .then(shipments => plotPlantOpsShipments(shipments))
            .catch(err => {
                console.error(err);
                plantOpsToast('Map failed to load', 'danger');
            });
    };
}


$(document).ready(function () {
    loadDashboardStats();
    loadPlantOpsMap();
    // DataTable length menu styling fix
    $('link[href$="dataTables.bootstrap5.min.css"]').after('<link rel="stylesheet" href="/css/plantoperationsdashboard.css">');
});

function loadDashboardStats() {
    $('#plantOpsRevenueChart .plant-ops-spinner').removeClass('d-none');
    $('#plantOpsOrdersChart .plant-ops-spinner').removeClass('d-none');
    $('#plantOpsInventoryChart .plant-ops-spinner').removeClass('d-none');
    window.fetchWithAuth(window.PortfolioApi.routes.fiber.dashboard)
        .then(r => r.json())
        .then(data => {
            updateDashboardBadges(data);
            renderRevenueChart(data.revenueByMonth || []);
            renderOrdersChart(data.ordersByStatus || []);
            renderInventoryChart(data.inventoryByCategory || []);
            plantOpsToast('Dashboard loaded', 'success');
        })
        .catch(() => {
            $('#plantOpsRevenueChart').html('<div class="text-danger small">Failed to load dashboard data.</div>');
            $('#plantOpsOrdersChart').html('<div class="text-danger small">Failed to load dashboard data.</div>');
            $('#plantOpsInventoryChart').html('<div class="text-danger small">Failed to load dashboard data.</div>');
            plantOpsToast('Failed to load dashboard', 'error');
        })
        .finally(() => {
            $('#plantOpsRevenueChart .plant-ops-spinner').addClass('d-none');
            $('#plantOpsOrdersChart .plant-ops-spinner').addClass('d-none');
            $('#plantOpsInventoryChart .plant-ops-spinner').addClass('d-none');
        });
}

function updateDashboardBadges(data) {
    $('#badgeActiveShipments').text(`${data.activeShipments ?? 0} Active Shipments`);
    $('#badgeOpenOrders').text(`${data.openOrders ?? 0} Open Orders`);
    $('#badgeLowStock').text(`${data.lowStockAlerts ?? 0} Low Stock`);
}

function renderRevenueChart(revenueByMonth) {
    const container = d3.select('#plantOpsRevenueChart');
    container.selectAll('*:not(.plant-ops-spinner)').remove();
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

function renderOrdersChart(ordersByStatus) {
    const container = d3.select('#plantOpsOrdersChart');
    container.selectAll('*:not(.plant-ops-spinner)').remove();
    if (!ordersByStatus.length) {
        container.append('div').attr('class', 'text-muted small').text('No order status data.');
        return;
    }
    const margin = { top: 24, right: 24, bottom: 40, left: 60 };
    const width = container.node().clientWidth - margin.left - margin.right;
    const height = 320 - margin.top - margin.bottom;
    const svg = container.append('svg')
        .attr('width', width + margin.left + margin.right)
        .attr('height', height + margin.top + margin.bottom)
        .append('g')
        .attr('transform', `translate(${margin.left},${margin.top})`);

    const statuses = ordersByStatus.map(d => d.status);
    const counts = ordersByStatus.map(d => d.count);
    const x = d3.scaleBand().domain(statuses).range([0, width]).padding(0.2);
    const y = d3.scaleLinear().domain([0, d3.max(counts) * 1.1]).range([height, 0]);

    svg.append('g')
        .attr('transform', `translate(0,${height})`)
        .call(d3.axisBottom(x))
        .selectAll('text')
        .attr('class', 'small')
        .attr('fill', 'var(--text-muted)');

    svg.append('g')
        .call(d3.axisLeft(y).ticks(6).tickFormat(d3.format('d')))
        .selectAll('text')
        .attr('class', 'small')
        .attr('fill', 'var(--text-muted)');

    svg.selectAll('.bar')
        .data(ordersByStatus)
        .enter()
        .append('rect')
        .attr('class', 'bar')
        .attr('x', d => x(d.status))
        .attr('y', d => y(d.count))
        .attr('width', x.bandwidth())
        .attr('height', d => height - y(d.count))
        .attr('fill', 'var(--accent)');

    svg.append('text')
        .attr('x', width / 2)
        .attr('y', height + margin.bottom - 5)
        .attr('text-anchor', 'middle')
        .attr('fill', 'var(--text-muted)')
        .attr('class', 'small')
        .text('Order Status');
    svg.append('text')
        .attr('transform', 'rotate(-90)')
        .attr('y', -margin.left + 16)
        .attr('x', -height / 2)
        .attr('text-anchor', 'middle')
        .attr('fill', 'var(--text-muted)')
        .attr('class', 'small')
        .text('Count');
}

function renderInventoryChart(inventoryByCategory) {
    const container = d3.select('#plantOpsInventoryChart');
    container.selectAll('*:not(.plant-ops-spinner)').remove();
    if (!inventoryByCategory.length) {
        container.append('div').attr('class', 'text-muted small').text('No inventory data.');
        return;
    }
    const margin = { top: 24, right: 24, bottom: 40, left: 60 };
    const width = container.node().clientWidth - margin.left - margin.right;
    const height = 320 - margin.top - margin.bottom;
    const svg = container.append('svg')
        .attr('width', width + margin.left + margin.right)
        .attr('height', height + margin.top + margin.bottom)
        .append('g')
        .attr('transform', `translate(${margin.left},${margin.top})`);

    const categories = inventoryByCategory.map(d => d.category);
    const counts = inventoryByCategory.map(d => d.count);
    const x = d3.scaleBand().domain(categories).range([0, width]).padding(0.2);
    const y = d3.scaleLinear().domain([0, d3.max(counts) * 1.1]).range([height, 0]);

    svg.append('g')
        .attr('transform', `translate(0,${height})`)
        .call(d3.axisBottom(x))
        .selectAll('text')
        .attr('class', 'small')
        .attr('fill', 'var(--text-muted)');

    svg.append('g')
        .call(d3.axisLeft(y).ticks(6).tickFormat(d3.format('d')))
        .selectAll('text')
        .attr('class', 'small')
        .attr('fill', 'var(--text-muted)');

    svg.selectAll('.bar')
        .data(inventoryByCategory)
        .enter()
        .append('rect')
        .attr('class', 'bar')
        .attr('x', d => x(d.category))
        .attr('y', d => y(d.count))
        .attr('width', x.bandwidth())
        .attr('height', d => height - y(d.count))
        .attr('fill', 'var(--accent)');

    svg.append('text')
        .attr('x', width / 2)
        .attr('y', height + margin.bottom - 5)
        .attr('text-anchor', 'middle')
        .attr('fill', 'var(--text-muted)')
        .attr('class', 'small')
        .text('Category');
    svg.append('text')
        .attr('transform', 'rotate(-90)')
        .attr('y', -margin.left + 16)
        .attr('x', -height / 2)
        .attr('text-anchor', 'middle')
        .attr('fill', 'var(--text-muted)')
        .attr('class', 'small')
        .text('Count');
}
