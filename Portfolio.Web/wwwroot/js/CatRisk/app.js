// Catastrophe Risk Analyzer Page — self-contained IIFE, no ES module syntax.
// Depends on the deferred shared modules: PortfolioUi, PortfolioFormat, PortfolioCharts, PortfolioDom.
(function () {
    "use strict";

    var KM_PER_LAT_DEGREE = 111.32;
    var BENCHMARK_PERIODS = [10, 25, 50, 100, 250, 500];
    var charts = window.PortfolioCharts;
    var fmt = window.PortfolioFormat;
    var ui = window.PortfolioUi;

    // Cache DOM nodes up front.
    var alertBox = ui.inlineAlert(document.getElementById("catAlert"));
    var radiusInput = document.getElementById("catRadius");
    var limitInput = document.getElementById("catLimit");
    var alphaInput = document.getElementById("catAlpha");
    var alphaValue = document.getElementById("catAlphaValue");
    var catalogSelect = document.getElementById("catCatalog");
    var runBtn = document.getElementById("catRunBtn");
    var reloadBtn = document.getElementById("catReloadBtn");
    var communityTable = document.getElementById("catCommunityTable");
    var summary = document.getElementById("catSummary");
    var exposureSvg = document.getElementById("catExposureSvg");
    var curveSvg = document.getElementById("catCurveSvg");
    var resultsPanel = exposureSvg.closest(".card");
    var kpiTiv = document.getElementById("catKpiTiv");
    var kpiAal = document.getElementById("catKpiAal");
    var kpiPml = document.getElementById("catKpiPml");
    var kpiBreaches = document.getElementById("catKpiBreaches");

    // Module state.
    var book = null;
    var projection = null;
    var lastAccumulation = null;
    var lastSimulation = null;

    runBtn.addEventListener("click", run);
    reloadBtn.addEventListener("click", loadBook);
    alphaInput.addEventListener("input", function () {
        alphaValue.textContent = parseFloat(alphaInput.value).toFixed(1);
    });
    // Hazard colours are resolved tokens, so redraw when the theme flips.
    charts.onThemeChange(function () {
        if (!book) { return; }
        buildCommunityTable(book.communities);
        drawExposure(lastAccumulation, lastSimulation);
    });

    loadBook(true);

    // ── Data ────────────────────────────────────────────────────────────────

    // Runs a request with `primary` as the busy trigger and `other` also held busy.
    function runExclusive(primary, other, opts) {
        var releaseOther = ui.busy({ buttons: [other] });
        opts.trigger = primary;
        opts.alert = alertBox;
        opts.region = resultsPanel;
        return ui.runAction(opts).then(function (r) { releaseOther(); return r; });
    }

    // `onLoad === true` is the page-load call: it renders but does not announce. Reload Book passes the
    // click event, which is not `true`, so a visitor's own reload is still announced.
    function loadBook(onLoad) {
        return runExclusive(reloadBtn, runBtn, {
            request: function () { return apiGet(window.PortfolioApi.routes.spatialCompute.catRisk.book); },
            render: function (result) {
                book = result;
                projection = buildProjection(book.locations);
                lastAccumulation = null;
                lastSimulation = null;
                kpiTiv.textContent = fmt.money(book.totalInsuredValue, { compact: true });
                buildCommunityTable(book.communities);
                drawExposure(null);
                drawCurve(null);
                summary.textContent = JSON.stringify({
                    book: book.bookName,
                    locations: book.locationCount,
                    events: book.events.length
                }, null, 2);
            },
            summary: onLoad === true ? undefined : function (result) {
                return "Policy book loaded: " + result.locationCount + " locations, total insured value " +
                    fmt.money(result.totalInsuredValue, { compact: true }) + ".";
            }
        });
    }

    function run() {
        if (!book) {
            alertBox.show("The policy book has not loaded yet.");
            return;
        }

        var radiusKm = parseFloat(radiusInput.value);
        var concentrationLimit = parseFloat(limitInput.value) * 1000000;
        var alpha = parseFloat(alphaInput.value);
        var events = sampleCatalog(book.events, parseInt(catalogSelect.value, 10));

        runExclusive(runBtn, reloadBtn, {
            request: function () {
                var accumulation = null;
                return apiPost(window.PortfolioApi.routes.spatialCompute.catRisk.accumulation, {
                    locations: book.locations,
                    radiusKm: radiusKm,
                    concentrationLimit: concentrationLimit
                })
                    .then(function (result) {
                        accumulation = result;
                        return apiPost(window.PortfolioApi.routes.spatialCompute.catRisk.simulate, {
                            locations: book.locations,
                            events: events,
                            vulnerabilityAlpha: alpha
                        });
                    })
                    .then(function (simulation) { return { accumulation: accumulation, simulation: simulation }; });
            },
            render: function (r) { render(r.accumulation, r.simulation); },
            summary: function (r) {
                return "Analysis complete: average annual loss " + fmt.money(r.simulation.averageAnnualLoss, { compact: true }) +
                    ", probable maximum loss " + fmt.money(r.simulation.probableMaximumLoss, { compact: true }) +
                    ", " + r.accumulation.breachCount + " concentration breaches.";
            }
        });
    }

    // Subsamples the catalog while preserving total annual frequency, so shrinking the
    // catalog trades resolution for speed without changing the modelled hazard rate.
    function sampleCatalog(events, target) {
        if (!target || target >= events.length) {
            return events;
        }

        var i;
        var originalRate = 0;
        for (i = 0; i < events.length; i++) {
            originalRate += events[i].annualRate;
        }

        var step = events.length / target;
        var picked = [];
        for (i = 0; i < target; i++) {
            picked.push(events[Math.floor(i * step)]);
        }

        var pickedRate = 0;
        for (i = 0; i < picked.length; i++) {
            pickedRate += picked[i].annualRate;
        }
        var scale = pickedRate > 0 ? originalRate / pickedRate : 1;

        return picked.map(function (e) {
            return {
                id: e.id,
                latitude: e.latitude,
                longitude: e.longitude,
                intensity: e.intensity,
                radiusKm: e.radiusKm,
                annualRate: e.annualRate * scale
            };
        });
    }

    function render(accumulation, simulation) {
        lastAccumulation = accumulation;
        lastSimulation = simulation;
        kpiAal.textContent = fmt.money(simulation.averageAnnualLoss, { compact: true });
        kpiPml.textContent = fmt.money(simulation.probableMaximumLoss, { compact: true });
        kpiBreaches.textContent = accumulation.breachCount;

        drawExposure(accumulation, simulation);
        drawCurve(simulation);

        var benchmarks = {};
        simulation.returnPeriodLosses.forEach(function (r) {
            benchmarks["rp" + r.returnPeriod] = Math.round(r.loss);
        });

        summary.textContent = JSON.stringify({
            nativeAccelerated: simulation.nativeAccelerated,
            eventCount: simulation.eventCount,
            averageAnnualLoss: Math.round(simulation.averageAnnualLoss),
            probableMaximumLoss: Math.round(simulation.probableMaximumLoss),
            concentrationBreaches: accumulation.breachCount,
            worstRingTiv: Math.round(accumulation.worstRingTiv),
            returnPeriodLosses: benchmarks
        }, null, 2);
    }

    // ── Projection ──────────────────────────────────────────────────────────

    // Equirectangular projection with longitude scaled by cos(mid-latitude), fitted
    // uniformly so the map keeps its aspect ratio and a kilometre radius maps to a
    // circle rather than an ellipse.
    function buildProjection(locations) {
        var minLat = Infinity, maxLat = -Infinity, minLon = Infinity, maxLon = -Infinity;
        locations.forEach(function (l) {
            if (l.latitude < minLat) { minLat = l.latitude; }
            if (l.latitude > maxLat) { maxLat = l.latitude; }
            if (l.longitude < minLon) { minLon = l.longitude; }
            if (l.longitude > maxLon) { maxLon = l.longitude; }
        });

        var midLat = (minLat + maxLat) / 2;
        var cosLat = Math.cos(midLat * Math.PI / 180);
        var xSpan = (maxLon - minLon) * cosLat;
        var ySpan = maxLat - minLat;
        var pad = 6;
        var usable = 100 - pad * 2;
        var scale = usable / Math.max(xSpan, ySpan, 1e-9);

        return {
            minLat: minLat,
            minLon: minLon,
            cosLat: cosLat,
            scale: scale,
            offsetX: pad + (usable - xSpan * scale) / 2,
            offsetY: pad + (usable - ySpan * scale) / 2,
            ySpanScaled: ySpan * scale
        };
    }

    function projectX(longitude) {
        return projection.offsetX + (longitude - projection.minLon) * projection.cosLat * projection.scale;
    }

    function projectY(latitude) {
        // Latitude increases north, SVG y increases downward.
        return projection.offsetY + projection.ySpanScaled - (latitude - projection.minLat) * projection.scale;
    }

    function kmToSvg(km) {
        return (km / KM_PER_LAT_DEGREE) * projection.scale;
    }

    // ── Exposure map ────────────────────────────────────────────────────────

    function drawExposure(accumulation, simulation) {
        charts.svg.clear(exposureSvg);
        if (!book || !projection) {
            return;
        }

        var maxTiv = 1;
        book.locations.forEach(function (l) {
            if (l.insuredValue > maxTiv) { maxTiv = l.insuredValue; }
        });

        // Worst event footprint sits underneath the exposure so markers stay readable.
        if (simulation && simulation.worstEvent) {
            exposureSvg.appendChild(charts.svg.el("circle", {
                cx: projectX(simulation.worstEvent.longitude).toFixed(2),
                cy: projectY(simulation.worstEvent.latitude).toFixed(2),
                r: Math.max(kmToSvg(simulation.worstEvent.radiusKm), 0.5).toFixed(2),
                fill: "var(--status-danger-fill)",
                "fill-opacity": "0.13",
                stroke: "var(--status-danger-fill)",
                "stroke-width": "0.35",
                "stroke-dasharray": "1.4 1"
            }));
        }

        var breachedById = {};
        if (accumulation) {
            accumulation.rings.forEach(function (r) {
                if (r.breached) { breachedById[r.locationId] = true; }
            });
        }

        var ramp = hazardRamp();
        book.locations.forEach(function (location) {
            var radius = 0.4 + Math.sqrt(location.insuredValue / maxTiv) * 1.2;
            var circle = charts.svg.el("circle", {
                cx: projectX(location.longitude).toFixed(2),
                cy: projectY(location.latitude).toFixed(2),
                r: radius.toFixed(2),
                fill: hazardColor(location.siteHazard, ramp),
                "fill-opacity": "0.82"
            });

            if (breachedById[location.id]) {
                circle.setAttribute("stroke", "var(--text)");
                circle.setAttribute("stroke-width", "0.28");
            }

            var title = charts.svg.el("title");
            title.textContent = location.name + " — " + fmt.money(location.insuredValue, { compact: true }) +
                " TIV, hazard " + location.siteHazard.toFixed(2);
            circle.appendChild(title);

            exposureSvg.appendChild(circle);
        });

        charts.describe(exposureSvg, "Exposure map of " + book.locations.length + " insured locations; marker size is " +
            "insured value and colour is site hazard." +
            (accumulation ? " " + accumulation.breachCount + " rings exceed the concentration limit." : ""));
    }

    // Green -> amber -> red ramp over site hazard, from the theme's status fills.
    function hazardRamp() {
        return { low: charts.status("success"), mid: charts.status("warning"), high: charts.status("danger") };
    }

    function hazardColor(hazard, ramp) {
        var h = Math.max(0, Math.min(1, hazard));
        ramp = ramp || hazardRamp();
        if (h < 0.5) {
            return charts.mix(ramp.low, ramp.mid, h / 0.5);
        }
        return charts.mix(ramp.mid, ramp.high, (h - 0.5) / 0.5);
    }

    // ── Exceedance probability curve ────────────────────────────────────────

    function drawCurve(simulation) {
        charts.svg.clear(curveSvg);

        var left = 13, right = 97, top = 5, bottom = 48;
        var svg = charts.svg;

        svg.line(curveSvg, left, top, left, bottom, { width: 0.3 });
        svg.line(curveSvg, left, bottom, right, bottom, { width: 0.3 });
        svg.text(curveSvg, (left + right) / 2, 58, "Return Period (years)", { size: 2.6, anchor: "middle" });
        svg.text(curveSvg, 3.2, (top + bottom) / 2, "Loss", { size: 2.6, anchor: "middle", rotate: -90 });

        if (!simulation || !simulation.exceedanceCurve || simulation.exceedanceCurve.length < 2) {
            svg.text(curveSvg, (left + right) / 2, (top + bottom) / 2,
                "Run an analysis to build the curve", { size: 3, anchor: "middle" });
            charts.describe(curveSvg, "Occurrence exceedance probability curve. No analysis has been run yet.");
            return;
        }

        var curve = simulation.exceedanceCurve;
        var minRp = Math.max(curve[0].returnPeriod, 1);
        var maxRp = curve[curve.length - 1].returnPeriod;
        var maxLoss = 0;
        curve.forEach(function (p) { if (p.loss > maxLoss) { maxLoss = p.loss; } });
        if (maxLoss <= 0) { maxLoss = 1; }
        if (maxRp <= minRp) { maxRp = minRp * 10; }

        var xFor = charts.scale.log([minRp, maxRp], [left, right]);
        var yFor = charts.scale.linear([0, maxLoss], [bottom, top]);

        // Y gridlines at quarter steps of the maximum loss.
        var q;
        for (q = 1; q <= 4; q++) {
            var gy = yFor(maxLoss * q / 4);
            svg.line(curveSvg, left, gy, right, gy, { stroke: "var(--border)", width: 0.18 });
            svg.text(curveSvg, left - 1.2, gy + 0.9, fmt.money(maxLoss * q / 4, { compact: true }), { size: 2.2, anchor: "end" });
        }

        // X ticks at the benchmark return periods that fall inside the plotted range.
        BENCHMARK_PERIODS.forEach(function (rp) {
            if (rp < minRp || rp > maxRp) { return; }
            var tx = xFor(rp);
            svg.line(curveSvg, tx, bottom, tx, bottom + 1.1, { width: 0.25 });
            svg.text(curveSvg, tx, bottom + 4.2, String(rp), { size: 2.3, anchor: "middle" });
        });

        // Average annual loss reference line.
        if (simulation.averageAnnualLoss > 0 && simulation.averageAnnualLoss <= maxLoss) {
            var aalY = yFor(simulation.averageAnnualLoss);
            var aalLine = svg.line(curveSvg, left, aalY, right, aalY, { stroke: "var(--accent-text)", width: 0.28 });
            aalLine.setAttribute("stroke-dasharray", "1.6 1.2");
            svg.text(curveSvg, right, aalY - 1.2, "AAL " + fmt.money(simulation.averageAnnualLoss, { compact: true }),
                { size: 2.2, anchor: "end", fill: "var(--accent-text)" });
        }

        // The curve itself.
        var points = curve.map(function (p) {
            return xFor(p.returnPeriod).toFixed(2) + "," + yFor(p.loss).toFixed(2);
        }).join(" ");

        curveSvg.appendChild(svg.el("polyline", {
            points: points,
            fill: "none",
            stroke: "var(--chart-series)",
            "stroke-width": "0.6",
            "stroke-linejoin": "round"
        }));

        // Mark the two return periods carriers actually quote.
        var quoted = [];
        simulation.returnPeriodLosses.forEach(function (r) {
            if (r.returnPeriod !== 100 && r.returnPeriod !== 250) { return; }
            if (r.returnPeriod < minRp || r.returnPeriod > maxRp) { return; }

            var cx = xFor(r.returnPeriod);
            var cy = yFor(r.loss);
            curveSvg.appendChild(svg.el("circle", {
                cx: cx.toFixed(2), cy: cy.toFixed(2), r: "0.9", fill: "var(--status-danger-fill)"
            }));

            var label = "1-in-" + r.returnPeriod + " " + fmt.money(r.loss, { compact: true });
            quoted.push(label);
            svg.text(curveSvg, cx, cy - 2, label, { size: 2.2, anchor: "middle", fill: "var(--text)" });
        });

        charts.describe(curveSvg, "Occurrence exceedance probability curve, loss against return period on a log scale. " +
            "Average annual loss " + fmt.money(simulation.averageAnnualLoss, { compact: true }) +
            (quoted.length ? "; " + quoted.join("; ") : "") + ".");
    }

    // ── Community table ─────────────────────────────────────────────────────

    function buildCommunityTable(communities) {
        var el = window.PortfolioDom.el;
        var ramp = hazardRamp();
        var rows = communities.map(function (c) {
            var swatch = el("span", { class: "legend__swatch", "aria-hidden": "true" });
            swatch.style.setProperty("--swatch", hazardColor(c.meanSiteHazard, ramp));
            return el("div", { class: "geo-cell-item" },
                el("div", { class: "d-flex align-items-center justify-content-between" },
                    el("span", { class: "d-inline-flex align-items-center gap-2" }, swatch, c.name),
                    el("strong", { text: fmt.money(c.totalInsuredValue, { compact: true }) })),
                el("div", { class: "geo-cell-meta",
                    text: c.locationCount + " locations · mean hazard " + c.meanSiteHazard.toFixed(2) }));
        });
        communityTable.replaceChildren.apply(communityTable, rows);
    }
}());
