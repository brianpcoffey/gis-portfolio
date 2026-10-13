// map.js - Smart Home Finder: the ArcGIS map.
// Creates the map and its graphics layer (stored in HomeFinder.state), wires the basemap select, and
// plots search results as ranked markers with popups. Publishes its entry points on window.HomeFinder.
(function (HF) {
    const state = HF.state;

    // Esri widget theme (light/dark, follows the site toggle) before the map exists.
    window.PortfolioEsri.loadTheme();

    async function initMap() {
        await window.PortfolioEsri.load();
        await new Promise((resolve) => {
            require([
                "esri/Map",
                "esri/views/MapView",
                "esri/layers/GraphicsLayer"
            ], (Map, MapView, GraphicsLayer) => {
                state.mapInstance = new Map({ basemap: "dark-gray-vector" });
                state.graphicsLayer = new GraphicsLayer();
                state.mapInstance.add(state.graphicsLayer);

                state.mapView = new MapView({
                    container: "homeFinderMap",
                    map: state.mapInstance,
                    center: [-117.1825, 34.0556],
                    zoom: 13
                });

                state.mapView.when(() => {
                    // ArcGIS nests its own role="application" surface inside the container; name that one.
                    const surface = state.mapView.container.querySelector(".esri-view-surface");
                    const label = document.getElementById("homeFinderMap")?.dataset.mapLabel;
                    if (surface && label) surface.setAttribute("aria-label", label);
                    resolve();
                });
            });
        });
    }

    function wireBasemapSelect() {
        const select = document.getElementById("basemapSelect");
        if (!select || !state.mapInstance) return;
        select.addEventListener("change", () => {
            state.mapInstance.basemap = select.value;
        });
    }

    function plotResults(results) {
        require([
            "esri/Graphic",
            "esri/geometry/Point",
            "esri/symbols/SimpleMarkerSymbol",
            "esri/PopupTemplate"
        ], (Graphic, Point, SimpleMarkerSymbol, PopupTemplate) => {
            state.graphicsLayer.removeAll();

            results.forEach((p) => {
                const point = new Point({ longitude: p.longitude, latitude: p.latitude });

                const color = p.rank <= 3 ? [76, 175, 80, 0.9]
                    : p.rank <= 7 ? [255, 193, 7, 0.9]
                        : [244, 67, 54, 0.8];

                const symbol = new SimpleMarkerSymbol({
                    color: color,
                    size: 14 - p.rank * 0.3,
                    outline: { color: [255, 255, 255], width: 1 }
                });

                const graphic = new Graphic({
                    geometry: point,
                    symbol: symbol,
                    attributes: p,
                    popupTemplate: new PopupTemplate({
                        title: `#${p.rank} \u2014 ${PortfolioDom.escapeHtml(p.street)}`,
                        content: `
                        <b>Score:</b> ${p.compositeScore}/100<br>
                        <b>Price:</b> $${p.price.toLocaleString()} (${HF.pricePerSqft(p)}/sqft)<br>
                        <b>Monthly:</b> $${p.estimatedMonthlyCost.toLocaleString()}<br>
                        <b>Type:</b> ${PortfolioDom.escapeHtml(p.propertyType)} \u2014 ${p.stories}-story<br>
                        <b>Bed/Bath:</b> ${p.bedrooms}/${p.bathrooms}<br>
                        <b>Sq Ft:</b> ${p.lotSqft.toLocaleString()}<br>
                        <b>Built:</b> ${p.yearBuilt}<br>
                        <b>Garage:</b> ${p.garageSpaces} car${p.garageSpaces === 1 ? "" : "s"}${p.hasPool ? " \u2014 Pool" : ""}<br>
                        <b>School rating:</b> ${p.schoolRating}/100<br>
                        <b>HOA:</b> ${p.hoaFee > 0 ? "$" + p.hoaFee.toLocaleString() + "/mo" : "None"}<br>
                        <b>Zip:</b> ${PortfolioDom.escapeHtml(p.zipCode)} \u2014 ${p.daysOnMarket} days on market<br>
                        <span class="theme-text-muted">Listed by ${PortfolioDom.escapeHtml(p.brokeredBy || "")}</span>
                    `
                    })
                });

                state.graphicsLayer.add(graphic);
            });

            if (results.length > 0) {
                state.mapView.goTo(state.graphicsLayer.graphics.toArray(), { padding: 50 });
            }
        });
    }

    Object.assign(HF, { initMap, wireBasemapSelect, plotResults });
}(window.HomeFinder));
