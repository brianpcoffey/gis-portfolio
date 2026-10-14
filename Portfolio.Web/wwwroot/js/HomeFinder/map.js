// map.js - Smart Home Finder: the ArcGIS map.
// Creates the map and its graphics layer (stored in HomeFinder.state), wires the basemap select, and
// plots search results as ranked markers with popups. Publishes its entry points on window.HomeFinder.
(function (HF) {
    const state = HF.state;

    // Esri widget theme (light/dark, follows the site toggle) before the map exists.
    window.PortfolioEsri.loadTheme();

    // How long the map gets, once the ArcGIS API script has loaded, to fetch its modules and become ready.
    // $arcgis.import may never settle when a module fetch fails (it neither resolves nor rejects), which would
    // leave the wait below pending and the rest of the page unwired; this turns that into the normal failure path.
    const MAP_READY_TIMEOUT_MS = 15000;

    // Resolves true once the map is ready, false when it could not be loaded (the ArcGIS API failed to
    // load, the view failed to start, or it was not ready within MAP_READY_TIMEOUT_MS). The page does not
    // depend on the map: the failure is shown in the map container and the caller wires everything else
    // regardless.
    async function initMap() {
        let timer = null;
        try {
            await window.PortfolioEsri.load();
            let timedOut = false;
            const timeout = new Promise((_, reject) => {
                timer = window.PortfolioEsri.visibleTimeout(MAP_READY_TIMEOUT_MS, () => {
                    timedOut = true;
                    reject(new Error("Map timed out"));
                });
            });
            // If the timeout wins, buildMap carries on in the background; racing it also marks its late rejection
            // as handled, and its `timedOut` checks keep it from building or touching state after that.
            await Promise.race([buildMap(() => timedOut), timeout]);
            return true;
        } catch (err) {
            console.error("Map failed to load:", err);
            // The view may be half-built (its ready promise rejected, or it timed out): release it, and the
            // WebGL context and listeners it holds, before the notice replaces its container. initMap never
            // rejects, so a failing destroy is logged rather than thrown.
            try {
                state.mapView?.destroy();
            } catch (destroyErr) {
                console.warn("Map view cleanup failed:", destroyErr);
            }
            state.mapView = null;
            state.mapInstance = null;
            state.graphicsLayer = null;
            document.getElementById("mapToolbar")?.classList.add("d-none");
            window.PortfolioEsri.showLoadError(
                document.getElementById("homeFinderMap"),
                "You can still search and save searches; results are listed but not plotted.");
            return false;
        } finally {
            timer?.clear();
        }
    }

    // Loads the map modules, builds the map and its graphics layer into HomeFinder.state, and resolves once the
    // view is ready. `abandoned()` turns true when initMap gives up on the timeout.
    async function buildMap(abandoned) {
        const [Map, MapView, GraphicsLayer] = await window.$arcgis.import([
            "@arcgis/core/Map.js",
            "@arcgis/core/views/MapView.js",
            "@arcgis/core/layers/GraphicsLayer.js"
        ]);
        // Modules arrived after the timeout: the error notice is up and the page is already
        // wired without a map, so do not build one into that container now.
        if (abandoned()) return;
        state.mapInstance = new Map({ basemap: "dark-gray-vector" });
        state.graphicsLayer = new GraphicsLayer();
        state.mapInstance.add(state.graphicsLayer);

        state.mapView = new MapView({
            container: "homeFinderMap",
            map: state.mapInstance,
            center: [-117.1825, 34.0556],
            zoom: 13
        });

        await state.mapView.when();
        if (abandoned()) return; // the view was given up on (and cleared from state) meanwhile
        // ArcGIS nests its own role="application" surface inside the container; name that one.
        const surface = state.mapView.container.querySelector(".esri-view-surface");
        const label = document.getElementById("homeFinderMap")?.dataset.mapLabel;
        if (surface && label) surface.setAttribute("aria-label", label);
    }

    function wireBasemapSelect() {
        const select = document.getElementById("basemapSelect");
        if (!select || !state.mapInstance) return;
        select.addEventListener("change", () => {
            state.mapInstance.basemap = select.value;
        });
    }

    async function plotResults(results) {
        // No map (the ArcGIS API failed to load): the results panel still lists the matches.
        if (!state.graphicsLayer || !state.mapView) return;
        const [Graphic, Point, SimpleMarkerSymbol, PopupTemplate] = await window.$arcgis.import([
            "@arcgis/core/Graphic.js",
            "@arcgis/core/geometry/Point.js",
            "@arcgis/core/symbols/SimpleMarkerSymbol.js",
            "@arcgis/core/PopupTemplate.js"
        ]);
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
    }

    Object.assign(HF, { initMap, wireBasemapSelect, plotResults });
}(window.HomeFinder));
