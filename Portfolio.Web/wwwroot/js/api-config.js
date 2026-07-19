/**
 * api-config.js
 * Single source of truth for all frontend API versioning and route paths.
 * Loaded globally via _Layout.cshtml before any page-level scripts.
 *
 * Usage:
 *   const url = window.PortfolioApi.routes.fiber.orders;
 *   // -> "/api/v1/fiber/orders"
 */
(function (global) {
    "use strict";

    var VERSION = "v1";
    var BASE    = "/api/" + VERSION;

    global.PortfolioApi = {
        version : VERSION,
        base    : BASE,

        routes: {
            // ── Geocoding ──────────────────────────────────────────────────────
            geocoding: {
                batch        : BASE + "/geocoding/batch",
                batchSync    : BASE + "/geocoding/batch/sync",
                reverse      : BASE + "/geocoding/reverse"
            },

            // ── Address Standardization ────────────────────────────────────────
            addresses: {
                parse    : BASE + "/addresses/parse",
                validate : BASE + "/addresses/validate"
            },

            // ── ArcGIS Features ────────────────────────────────────────────────
            features: {
                query : BASE + "/features",
                saved : BASE + "/features/saved"
            },

            // ── Collections ────────────────────────────────────────────────────
            collections: BASE + "/collections",

            // ── Profile ────────────────────────────────────────────────────────
            profile: BASE + "/profile",

            // ── User Profiles ──────────────────────────────────────────────────
            users: BASE + "/users",

            // ── Home Finder ────────────────────────────────────────────────────
            homeFinder: {
                search   : BASE + "/homefinder/search",
                property : BASE + "/homefinder/property",  // + /{id}
                searches : BASE + "/homefinder/searches"   // + /{id} for GET/DELETE
            },

            // ── Fiber Operations ───────────────────────────────────────────────
            fiber: {
                orders    : BASE + "/fiber/orders",
                materials : BASE + "/fiber/materials",
                shipments : BASE + "/fiber/shipments",
                dashboard : BASE + "/fiber/dashboard/stats"
            },

            // ── Native Spatial Compute MVPs ───────────────────────────────────
            spatialCompute: {
                geostream: {
                    events: BASE + "/geostream/events"
                },
                geometry: {
                    triangulate: BASE + "/geometry/triangulate",
                    clip       : BASE + "/geometry/clip"
                },
                raster: {
                    hillshade: BASE + "/raster/hillshade",
                    heatmap  : BASE + "/raster/heatmap"
                },
                network: {
                    graph      : BASE + "/network/graph",
                    route      : BASE + "/network/route",
                    serviceArea: BASE + "/network/service-area"
                },
                clustering: {
                    dbscan: BASE + "/clustering/dbscan"
                },
                viewshed: {
                    compute: BASE + "/viewshed/compute"
                },
                overlay: {
                    spatialJoin: BASE + "/overlay/spatial-join"
                },
                catRisk: {
                    book        : BASE + "/catrisk/book",
                    accumulation: BASE + "/catrisk/accumulation",
                    simulate    : BASE + "/catrisk/simulate"
                },
                fleet: {
                    scenario: BASE + "/fleet/scenario",
                    optimize: BASE + "/fleet/optimize"
                }
            }
        }
    };

}(window));
