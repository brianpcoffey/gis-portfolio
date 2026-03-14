/**
 * FiberFlow — Dashboard
 * Depends on: jQuery, D3.js, ArcGIS JS API 4.30
 * API endpoints:
 *   GET /api/fiberdashboard/stats
 *   GET /api/fibershipments
 */

(function() {
    // KPI Cards
    async function loadKpiCards() {
        try {
            const res = await fetch("/api/fiberdashboard/stats");
            if (!res.ok) throw new Error("Failed to load dashboard stats");
            const stats = await res.json();
            $("#fiber-kpi-active-shipments").text(stats.activeShipments);
            $("#fiber-kpi-open-orders").text(stats.openOrders);
            $("#fiber-kpi-low-stock").text(stats.lowStockAlerts);
            $("#fiber-kpi-mtd-revenue").text(new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(stats.mtdRevenue));
            if (stats.lowStockAlerts > 0) {
                $("#fiber-kpi-low-stock").addClass("fiber-kpi-pulse");
            }
            // D3 Analytics
            renderRevenueChart(stats.revenueByMonth);
            renderStatusChart(stats.ordersByStatus);
            renderTopClientsChart(stats.topClients);
        } catch (err) {
            // Optionally show error
            console.error(err);
        }
    }

    // D3.js Revenue By Month (Bar Chart)
    function renderRevenueChart(data) {
        const el = d3.select("#revenueChart");
        el.selectAll("*").remove();
        const margin = {top: 32, right: 24, bottom: 48, left: 64}, width = el.node().clientWidth - margin.left - margin.right, height = 350 - margin.top - margin.bottom;
        const svg = el.append("svg").attr("width", width + margin.left + margin.right).attr("height", height + margin.top + margin.bottom)
            .append("g").attr("transform", `translate(${margin.left},${margin.top})`);
        const x = d3.scaleBand().domain(data.map(d => d.month)).range([0, width]).padding(0.2);
        const y = d3.scaleLinear().domain([0, d3.max(data, d => d.revenue)]).nice().range([height, 0]);
        svg.append("g").attr("transform", `translate(0,${height})`).call(d3.axisBottom(x)).selectAll("text").attr("dy", "1em");
        svg.append("g").call(d3.axisLeft(y).ticks(6).tickFormat(d3.format("$,.0f")));
        svg.append("g").attr("class", "grid").call(d3.axisLeft(y).tickSize(-width).tickFormat(""));
        svg.selectAll(".bar").data(data).enter().append("rect")
            .attr("class", "bar")
            .attr("x", d => x(d.month))
            .attr("y", d => y(d.revenue))
            .attr("width", x.bandwidth())
            .attr("height", d => height - y(d.revenue))
            .attr("fill", "#f97316");
        svg.append("text").attr("x", width/2).attr("y", height+40).attr("text-anchor", "middle").text("Month");
        svg.append("text").attr("transform", "rotate(-90)").attr("x", -height/2).attr("y", -48).attr("text-anchor", "middle").text("Revenue");
    }

    // D3.js Orders By Status (Donut Chart)
    function renderStatusChart(data) {
        const el = d3.select("#statusChart");
        el.selectAll("*").remove();
        const width = el.node().clientWidth, height = 350, radius = Math.min(width, height) / 2 - 10;
        const svg = el.append("svg").attr("width", width).attr("height", height)
            .append("g").attr("transform", `translate(${width/2},${height/2})`);
        const color = d3.scaleOrdinal(["#64748b","#3b82f6","#eab308","#f97316","#22c55e"]);
        const pie = d3.pie().value(d => d.count);
        const arc = d3.arc().innerRadius(radius * 0.6).outerRadius(radius);
        const arcs = svg.selectAll("arc").data(pie(data)).enter().append("g");
        arcs.append("path").attr("d", arc).attr("fill", (d,i) => color(i));
        arcs.append("title").text(d => `${d.data.status}: ${d.data.count}`);
        // Tooltip on hover
        arcs.on("mouseover", function(e, d) {
            const [x, y] = d3.pointer(e);
            d3.select("body").append("div").attr("id","fiberflow-tooltip").style("position","absolute").style("left",`${e.pageX+10}px`).style("top",`${e.pageY-20}px`).style("background","#fff").style("padding","4px 8px").style("border-radius","4px").style("box-shadow","0 2px 8px #0002").style("z-index",9999).text(`${d.data.status}: ${d.data.count}`);
        }).on("mouseout", function() { d3.select("#fiberflow-tooltip").remove(); });
        // Legend
        svg.append("g").attr("transform", `translate(${-width/2+20},${-height/2+20})`).selectAll("rect").data(data).enter().append("rect")
            .attr("x", 0).attr("y", (d,i) => i*22).attr("width", 16).attr("height", 16).attr("fill", (d,i) => color(i));
        svg.append("g").attr("transform", `translate(${-width/2+40},${-height/2+32})`).selectAll("text").data(data).enter().append("text")
            .attr("x", 0).attr("y", (d,i) => i*22+12).text(d => d.status).style("font-size","13px");
    }

    // D3.js Top Clients (Horizontal Bar Chart)
    function renderTopClientsChart(data) {
        const el = d3.select("#topClientsChart");
        el.selectAll("*").remove();
        const margin = {top: 32, right: 24, bottom: 48, left: 120}, width = el.node().clientWidth - margin.left - margin.right, height = 350 - margin.top - margin.bottom;
        const svg = el.append("svg").attr("width", width + margin.left + margin.right).attr("height", height + margin.top + margin.bottom)
            .append("g").attr("transform", `translate(${margin.left},${margin.top})`);
        const y = d3.scaleBand().domain(data.map(d => d.name)).range([0, height]).padding(0.2);
        const x = d3.scaleLinear().domain([0, d3.max(data, d => d.revenue)]).nice().range([0, width]);
        svg.append("g").call(d3.axisLeft(y));
        svg.append("g").attr("transform", `translate(0,${height})`).call(d3.axisBottom(x).ticks(6).tickFormat(d3.format("$,.0f")));
        svg.append("g").attr("class", "grid").call(d3.axisBottom(x).tickSize(-height).tickFormat("")).attr("transform", `translate(0,${height})`);
        svg.selectAll(".bar").data(data).enter().append("rect")
            .attr("class", "bar")
            .attr("y", d => y(d.name))
            .attr("x", 0)
            .attr("height", y.bandwidth())
            .attr("width", d => x(d.revenue))
            .attr("fill", "#14b8a6");
        svg.append("text").attr("x", width/2).attr("y", height+40).attr("text-anchor", "middle").text("Revenue");
        svg.append("text").attr("transform", "rotate(-90)").attr("x", -height/2).attr("y", -100).attr("text-anchor", "middle").text("Client");
    }

    // ArcGIS Map (robust retry with max attempts)
    function initMap() {
        var maxAttempts = 50; // 5 seconds total
        var attempts = 0;

        function tryInit() {
            if (typeof require !== 'undefined') {
                require([
                    "esri/Map", "esri/views/MapView", "esri/Graphic",
                    "esri/layers/GraphicsLayer", "esri/geometry/Polyline",
                    "esri/symbols/SimpleLineSymbol", "esri/symbols/SimpleMarkerSymbol",
                    "esri/PopupTemplate"
                ], function(Map, MapView, Graphic, GraphicsLayer,
                            Polyline, SimpleLineSymbol, SimpleMarkerSymbol, PopupTemplate) {
                    var map = new Map({ basemap: "dark-gray-vector" });
                    var view = new MapView({
                        container: "fiberDashboardMap",
                        map: map,
                        center: [-95.3698, 29.7604],
                        zoom: 5
                    });
                    var graphicsLayer = new GraphicsLayer();
                    map.add(graphicsLayer);
                    // Plant marker
                    var plantMarker = new Graphic({
                        geometry: { type: "point", longitude: -95.3698, latitude: 29.7604 },
                        symbol: new SimpleMarkerSymbol({ style: "diamond", color: [249, 115, 22], size: 18, outline: { color: [0,0,0], width: 1 } }),
                        popupTemplate: { title: "FiberFlow Plant", content: "Houston, TX — Manufacturing Facility" }
                    });
                    graphicsLayer.add(plantMarker);
                    // Fetch shipments
                    fetch("/api/fibershipments").then(function(res) {
                        if (!res.ok) throw new Error("Failed to load shipments");
                        return res.json();
                    }).then(function(shipments) {
                        shipments.forEach(function(s) {
                            if (!s.route || !Array.isArray(s.route)) return;
                            var polyline = new Polyline({ paths: [s.route.map(pt => [pt.lng, pt.lat])] });
                            var lineSymbol = new SimpleLineSymbol({ color: [59, 130, 246], width: 3 });
                            var graphic = new Graphic({ geometry: polyline, symbol: lineSymbol, popupTemplate: new PopupTemplate({ title: s.trackingNumber, content: s.status }) });
                            graphicsLayer.add(graphic);
                        });
                    });
                });
            } else if (attempts++ < maxAttempts) {
                setTimeout(tryInit, 100);
            } else {
                console.warn("ArcGIS failed to load — map unavailable. " +
                             "Check if js.arcgis.com is being blocked by tracking prevention.");
            }
        }

        tryInit();
    }

    $(function () {
        loadKpiCards();
        initMap(); // just call it directly — tryInit handles the timing
    });

})();