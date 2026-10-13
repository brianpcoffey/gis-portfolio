// window.PortfolioMap — one Leaflet map factory. Load only after leaflet.js.
(function (g) {
  "use strict";

  // create("networkMap", { center, zoom, label }) -> L.Map with an OSM tile layer and attribution.
  function create(id, o) {
    if (!g.L) { throw new Error("PortfolioMap requires Leaflet (load leaflet.js first)."); }
    o = o || {};
    var map = g.L.map(id, {
      center: o.center || [34.0565, -117.1850],
      zoom: o.zoom === undefined ? 13 : o.zoom,
      zoomControl: true,
      preferCanvas: true
    });
    g.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      maxZoom: 19
    }).addTo(map);
    // Leaflet keeps its built-in keyboard pan/zoom; give the container an accessible name.
    if (o.label) { map.getContainer().setAttribute("aria-label", o.label); }
    return map;
  }

  g.PortfolioMap = { create: create };
}(window));
