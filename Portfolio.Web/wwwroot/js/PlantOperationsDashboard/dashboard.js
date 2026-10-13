// Plant Operations Dashboard - Dashboard JS
// Handles dashboard stats, D3 charts, and ArcGIS map



// HTML escaping for DataTables cells and modal `value="..."` inputs is PortfolioDom.escapeHtml
// (dom-safe.js; covers text and quoted-attribute contexts, '' for null/undefined).

// API helpers shared by orders.js / shipments.js / inventory.js (this file loads first).
// plantOpsJson: parsed JSON on success; on a non-ok response it rejects with the server's own
// message (getApiError reads error || detail || title, so Fiber's 404/409/400 ProblemDetails show up).
function plantOpsJson(res) {
    if (res.ok) return res.json();
    return window.getApiError(res).then(msg => { throw new Error(msg); });
}
// plantOpsOk: same rejection for responses with no body (DELETE returns 204).
function plantOpsOk(res) {
    if (res.ok) return res;
    return window.getApiError(res).then(msg => { throw new Error(msg); });
}
// plantOpsFailure: "Failed to save order: <server message>" for toasts and inline alerts.
function plantOpsFailure(prefix, err) {
    const detail = err && err.message;
    return detail ? `${prefix}: ${detail}` : prefix;
}
// plantOpsAlert: PortfolioUi.inlineAlert for a table's error box (#ordersTableError etc.).
function plantOpsAlert(id) {
    return PortfolioUi.inlineAlert(document.getElementById(id));
}
window.plantOpsJson = plantOpsJson;
window.plantOpsOk = plantOpsOk;
window.plantOpsFailure = plantOpsFailure;
window.plantOpsAlert = plantOpsAlert;

// Focus after a modal closes. Every modal here is built fresh in #plantOpsModals, and a save re-renders
// the table that held its opener, so Bootstrap's own restore (data-api triggers only) finds nothing and
// focus is left on the hidden modal. Remember the opener on show; after hide, focus it if it is still in
// the page, else the active tab's "New ..." button (#btnNewOrder / #btnNewShipment / #btnNewMaterial).
let plantOpsModalOpener = null;
document.addEventListener('show.bs.modal', e => {
    if (e.target.closest('#plantOpsModals')) plantOpsModalOpener = document.activeElement;
});
document.addEventListener('hidden.bs.modal', e => {
    if (!e.target.closest('#plantOpsModals')) return;
    const opener = plantOpsModalOpener;
    plantOpsModalOpener = null;
    setTimeout(() => {
        const usable = opener && opener !== document.body && opener.isConnected;
        const target = usable ? opener : document.querySelector('#ordersShipmentsTabsContent .tab-pane.active .btn[id^="btnNew"]');
        if (target) target.focus();
    }, 0);
});



// Ensure loadPlantOpsMap is always defined to avoid ReferenceError
// Implement map loader: initializes ArcGIS map and plots shipments
if (typeof loadPlantOpsMap !== 'function') {
    let plantOpsMapView = null;
    let plantOpsMapInstance = null;
    let plantOpsGraphicsLayer = null;

    async function initPlantOpsMap() {
        const mapContainer = document.getElementById('plantOpsMap');
        if (!mapContainer || typeof require !== 'function') {
            showToast('Map container or ArcGIS library not loaded. Please check your network and script includes.', 'danger');
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
                    // ArcGIS nests its own role="application" surface in the container; name that one.
                    const surface = plantOpsMapView.container.querySelector('.esri-view-surface');
                    const label = mapContainer.dataset.mapLabel;
                    if (surface && label) surface.setAttribute('aria-label', label);
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

                // Status tokens (resolved for the current theme): in transit / delivered / other.
                const color = s.status?.toLowerCase().includes('in transit')
                    ? PortfolioCharts.status('success')
                    : s.status?.toLowerCase().includes('deliv')
                        ? PortfolioCharts.status('info')
                        : PortfolioCharts.status('warning');

                plantOpsGraphicsLayer.add(new Graphic({
                    geometry: new Point({ longitude: lon, latitude: lat }),
                    symbol: new SimpleMarkerSymbol({
                        color, size: 12,
                        outline: { color: [255, 255, 255], width: 1 }
                    }),
                    attributes: s,
                    popupTemplate: new PopupTemplate({
                        title: `${PortfolioDom.escapeHtml(s.trackingNumber)} — ${PortfolioDom.escapeHtml(s.carrierName)}`,
                        content: `Status: ${PortfolioDom.escapeHtml(s.status)}<br/>Dest: ${PortfolioDom.escapeHtml(s.destinationCity)}, ${PortfolioDom.escapeHtml(s.destinationState)}`
                    })
                }));
            });
            const points = plantOpsGraphicsLayer.graphics.toArray();
            if (points.length) {
                plantOpsMapView.goTo(points, { padding: 50 });
            } else {
                showToast('No shipments with coordinates found.', 'warning');
            }
        });
    }

    window.loadPlantOpsMap = function () {
        initPlantOpsMap()
            .then(() => window.apiFetch(window.PortfolioApi.routes.fiber.shipments))
            .then(plantOpsJson)
            .then(shipments => plotPlantOpsShipments(shipments))
            .catch(err => {
                console.error(err);
                showToast(plantOpsFailure('Map failed to load', err), 'danger');
            });
    };
}


$(document).ready(function () {
    // Esri widget theme CSS matching the site theme; esri-theme.js swaps it on themeChanged.
    PortfolioEsri.loadTheme();
    loadDashboardStats();
    loadPlantOpsMap();
});

function loadDashboardStats() {
    $('#plantOpsRevenueChart .plant-ops-spinner').removeClass('d-none');
    $('#plantOpsOrdersChart .plant-ops-spinner').removeClass('d-none');
    $('#plantOpsInventoryChart .plant-ops-spinner').removeClass('d-none');
    window.apiFetch(window.PortfolioApi.routes.fiber.dashboard)
        .then(plantOpsJson)
        .then(data => {
            updateDashboardBadges(data);
            renderRevenueChart(data.revenueByMonth || []);
            renderOrdersChart(data.ordersByStatus || []);
            renderInventoryChart(data.inventoryByCategory || []);
            showToast('Dashboard loaded', 'success');
        })
        .catch(err => {
            const message = plantOpsFailure('Failed to load dashboard data', err);
            ['#plantOpsRevenueChart', '#plantOpsOrdersChart', '#plantOpsInventoryChart'].forEach(sel => {
                $(sel).empty().append($('<div class="text-danger small" role="alert"></div>').text(message));
            });
            showToast(plantOpsFailure('Failed to load dashboard', err), 'danger');
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
