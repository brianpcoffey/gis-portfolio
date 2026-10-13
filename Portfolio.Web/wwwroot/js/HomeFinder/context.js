// context.js - Smart Home Finder: the shared window.HomeFinder namespace.
// The page script is split into classic deferred scripts that share this one object. Load order
// (set in Pages/Projects/HomeFinder/Index.cshtml): context, preferences, map, results, saved-searches, app.
// This file must load first. The others only read HomeFinder.* when they load and call each other
// through it at event time, after every script has run.
(function () {
    window.HomeFinder = {
        routes: window.PortfolioApi.routes.homeFinder,

        // Shared mutable state. map.js creates the map objects; app.js stores the latest search results.
        state: {
            mapView: null,
            mapInstance: null,
            graphicsLayer: null,
            currentResults: []
        },

        announce: (msg) => window.PortfolioUi.announce(msg)
    };
}());
