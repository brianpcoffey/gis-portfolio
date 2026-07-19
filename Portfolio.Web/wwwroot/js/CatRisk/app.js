// Catastrophe Risk Analyzer Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    var SVGNS = "http://www.w3.org/2000/svg";
    var KM_PER_LAT_DEGREE = 111.32;
    var BENCHMARK_PERIODS = [10, 25, 50, 100, 250, 500];

    // Cache DOM nodes up front.
    var alertBox = document.getElementById("catAlert");
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
    var kpiTiv = document.getElementById("catKpiTiv");
    var kpiAal = document.getElementById("catKpiAal");
    var kpiPml = document.getElementById("catKpiPml");
    var kpiBreaches = document.getElementById("catKpiBreaches");

    // Module state.
    var book = null;
    var projection = null;

    runBtn.addEventListener("click", run);
    reloadBtn.addEventListener("click", loadBook);
    alphaInput.addEventListener("input", function () {
        alphaValue.textContent = parseFloat(alphaInput.value).toFixed(1);
    });

    loadBook();

    // ── Data ────────────────────────────────────────────────────────────────

    function loadBook() {
        hideAlert();
        setBusy(true);
        apiGet(window.PortfolioApi.routes.spatialCompute.catRisk.book)
            .then(function (result) {
                book = result;
                projection = buildProjection(book.locations);
                kpiTiv.textContent = formatMoney(book.totalInsuredValue);
                buildCommunityTable(book.communities);
                drawExposure(null);
                drawCurve(null);
                summary.textContent = JSON.stringify({
                    book: book.bookName,
                    locations: book.locationCount,
                    events: book.events.length
                }, null, 2);
            })
            .catch(function (err) { showAlert(err.message || "Failed to load the policy book."); })
            .finally(function () { setBusy(false); });
    }

    function run() {
        if (!book) {
            showAlert("The policy book has not loaded yet.");
            return;
        }

        hideAlert();
        setBusy(true);

        var radiusKm = parseFloat(radiusInput.value);
        var concentrationLimit = parseFloat(limitInput.value) * 1000000;
        var alpha = parseFloat(alphaInput.value);
        var events = sampleCatalog(book.events, parseInt(catalogSelect.value, 10));

        var accumulation = null;

        apiPost(window.PortfolioApi.routes.spatialCompute.catRisk.accumulation, {
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
            .then(function (simulation) { render(accumulation, simulation); })
            .catch(function (err) { showAlert(err.message || "Analysis failed."); })
            .finally(function () { setBusy(false); });
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
        kpiAal.textContent = formatMoney(simulation.averageAnnualLoss);
        kpiPml.textContent = formatMoney(simulation.probableMaximumLoss);
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
        exposureSvg.innerHTML = "";
        if (!book || !projection) {
            return;
        }

        var maxTiv = 1;
        book.locations.forEach(function (l) {
            if (l.insuredValue > maxTiv) { maxTiv = l.insuredValue; }
        });

        // Worst event footprint sits underneath the exposure so markers stay readable.
        if (simulation && simulation.worstEvent) {
            var footprint = document.createElementNS(SVGNS, "circle");
            footprint.setAttribute("cx", projectX(simulation.worstEvent.longitude).toFixed(2));
            footprint.setAttribute("cy", projectY(simulation.worstEvent.latitude).toFixed(2));
            footprint.setAttribute("r", Math.max(kmToSvg(simulation.worstEvent.radiusKm), 0.5).toFixed(2));
            footprint.setAttribute("fill", "#e03131");
            footprint.setAttribute("fill-opacity", "0.13");
            footprint.setAttribute("stroke", "#e03131");
            footprint.setAttribute("stroke-width", "0.35");
            footprint.setAttribute("stroke-dasharray", "1.4 1");
            exposureSvg.appendChild(footprint);
        }

        var breachedById = {};
        if (accumulation) {
            accumulation.rings.forEach(function (r) {
                if (r.breached) { breachedById[r.locationId] = true; }
            });
        }

        book.locations.forEach(function (location) {
            var circle = document.createElementNS(SVGNS, "circle");
            var radius = 0.4 + Math.sqrt(location.insuredValue / maxTiv) * 1.2;

            circle.setAttribute("cx", projectX(location.longitude).toFixed(2));
            circle.setAttribute("cy", projectY(location.latitude).toFixed(2));
            circle.setAttribute("r", radius.toFixed(2));
            circle.setAttribute("fill", hazardColor(location.siteHazard));
            circle.setAttribute("fill-opacity", "0.82");

            if (breachedById[location.id]) {
                circle.setAttribute("stroke", "var(--text)");
                circle.setAttribute("stroke-width", "0.28");
            }

            var title = document.createElementNS(SVGNS, "title");
            title.textContent = location.name + " — " + formatMoney(location.insuredValue) +
                " TIV, hazard " + location.siteHazard.toFixed(2);
            circle.appendChild(title);

            exposureSvg.appendChild(circle);
        });
    }

    // Green -> amber -> red ramp over site hazard.
    function hazardColor(hazard) {
        var h = Math.max(0, Math.min(1, hazard));
        if (h < 0.5) {
            return mixHex("#2f9e44", "#f59f00", h / 0.5);
        }
        return mixHex("#f59f00", "#e03131", (h - 0.5) / 0.5);
    }

    function mixHex(from, to, t) {
        var f = parseInt(from.slice(1), 16);
        var g = parseInt(to.slice(1), 16);
        var r = Math.round(((f >> 16) & 255) + (((g >> 16) & 255) - ((f >> 16) & 255)) * t);
        var gr = Math.round(((f >> 8) & 255) + (((g >> 8) & 255) - ((f >> 8) & 255)) * t);
        var b = Math.round((f & 255) + ((g & 255) - (f & 255)) * t);
        return "rgb(" + r + "," + gr + "," + b + ")";
    }

    // ── Exceedance probability curve ────────────────────────────────────────

    function drawCurve(simulation) {
        curveSvg.innerHTML = "";

        var left = 13, right = 97, top = 5, bottom = 48;

        appendLine(curveSvg, left, top, left, bottom, "var(--text-muted)", 0.3);
        appendLine(curveSvg, left, bottom, right, bottom, "var(--text-muted)", 0.3);
        appendText(curveSvg, (left + right) / 2, 58, "Return Period (years)", 2.6, "middle", "var(--text-muted)");
        appendText(curveSvg, 3.2, (top + bottom) / 2, "Loss", 2.6, "middle", "var(--text-muted)", -90);

        if (!simulation || !simulation.exceedanceCurve || simulation.exceedanceCurve.length < 2) {
            appendText(curveSvg, (left + right) / 2, (top + bottom) / 2,
                "Run an analysis to build the curve", 3, "middle", "var(--text-muted)");
            return;
        }

        var curve = simulation.exceedanceCurve;
        var minRp = Math.max(curve[0].returnPeriod, 1);
        var maxRp = curve[curve.length - 1].returnPeriod;
        var maxLoss = 0;
        curve.forEach(function (p) { if (p.loss > maxLoss) { maxLoss = p.loss; } });
        if (maxLoss <= 0) { maxLoss = 1; }

        var logMin = log10(minRp);
        var logMax = log10(maxRp);
        if (logMax <= logMin) { logMax = logMin + 1; }

        function xFor(rp) {
            var t = (log10(Math.max(rp, minRp)) - logMin) / (logMax - logMin);
            return left + Math.max(0, Math.min(1, t)) * (right - left);
        }

        function yFor(loss) {
            return bottom - (loss / maxLoss) * (bottom - top);
        }

        // Y gridlines at quarter steps of the maximum loss.
        var q;
        for (q = 1; q <= 4; q++) {
            var gy = yFor(maxLoss * q / 4);
            appendLine(curveSvg, left, gy, right, gy, "var(--border)", 0.18);
            appendText(curveSvg, left - 1.2, gy + 0.9, formatMoneyShort(maxLoss * q / 4), 2.2, "end", "var(--text-muted)");
        }

        // X ticks at the benchmark return periods that fall inside the plotted range.
        BENCHMARK_PERIODS.forEach(function (rp) {
            if (rp < minRp || rp > maxRp) { return; }
            var tx = xFor(rp);
            appendLine(curveSvg, tx, bottom, tx, bottom + 1.1, "var(--text-muted)", 0.25);
            appendText(curveSvg, tx, bottom + 4.2, String(rp), 2.3, "middle", "var(--text-muted)");
        });

        // Average annual loss reference line.
        if (simulation.averageAnnualLoss > 0 && simulation.averageAnnualLoss <= maxLoss) {
            var aalY = yFor(simulation.averageAnnualLoss);
            var aalLine = appendLine(curveSvg, left, aalY, right, aalY, "var(--accent)", 0.28);
            aalLine.setAttribute("stroke-dasharray", "1.6 1.2");
            appendText(curveSvg, right, aalY - 1.2, "AAL " + formatMoneyShort(simulation.averageAnnualLoss),
                2.2, "end", "var(--accent)");
        }

        // The curve itself.
        var points = curve.map(function (p) {
            return xFor(p.returnPeriod).toFixed(2) + "," + yFor(p.loss).toFixed(2);
        }).join(" ");

        var polyline = document.createElementNS(SVGNS, "polyline");
        polyline.setAttribute("points", points);
        polyline.setAttribute("fill", "none");
        polyline.setAttribute("stroke", "var(--primary-light)");
        polyline.setAttribute("stroke-width", "0.6");
        polyline.setAttribute("stroke-linejoin", "round");
        curveSvg.appendChild(polyline);

        // Mark the two return periods carriers actually quote.
        simulation.returnPeriodLosses.forEach(function (r) {
            if (r.returnPeriod !== 100 && r.returnPeriod !== 250) { return; }
            if (r.returnPeriod < minRp || r.returnPeriod > maxRp) { return; }

            var cx = xFor(r.returnPeriod);
            var cy = yFor(r.loss);
            var dot = document.createElementNS(SVGNS, "circle");
            dot.setAttribute("cx", cx.toFixed(2));
            dot.setAttribute("cy", cy.toFixed(2));
            dot.setAttribute("r", "0.9");
            dot.setAttribute("fill", "#e03131");
            curveSvg.appendChild(dot);

            appendText(curveSvg, cx, cy - 2, "1-in-" + r.returnPeriod + " " + formatMoneyShort(r.loss),
                2.2, "middle", "var(--text)");
        });
    }

    function log10(value) {
        return Math.log(value) / Math.LN10;
    }

    function appendLine(svg, x1, y1, x2, y2, stroke, width) {
        var line = document.createElementNS(SVGNS, "line");
        line.setAttribute("x1", x1.toFixed(2));
        line.setAttribute("y1", y1.toFixed(2));
        line.setAttribute("x2", x2.toFixed(2));
        line.setAttribute("y2", y2.toFixed(2));
        line.setAttribute("stroke", stroke);
        line.setAttribute("stroke-width", String(width));
        svg.appendChild(line);
        return line;
    }

    function appendText(svg, x, y, content, size, anchor, fill, rotate) {
        var text = document.createElementNS(SVGNS, "text");
        text.setAttribute("x", x.toFixed(2));
        text.setAttribute("y", y.toFixed(2));
        text.setAttribute("font-size", String(size));
        text.setAttribute("text-anchor", anchor);
        text.setAttribute("fill", fill);
        if (rotate) {
            text.setAttribute("transform", "rotate(" + rotate + " " + x.toFixed(2) + " " + y.toFixed(2) + ")");
        }
        text.textContent = content;
        svg.appendChild(text);
        return text;
    }

    // ── Community table ─────────────────────────────────────────────────────

    function buildCommunityTable(communities) {
        var rows = communities.map(function (c) {
            return '<div class="geo-cell-item">' +
                '<div class="d-flex align-items-center justify-content-between">' +
                '<span class="d-inline-flex align-items-center gap-2">' +
                '<span style="width:0.7rem;height:0.7rem;border-radius:2px;background:' +
                hazardColor(c.meanSiteHazard) + ';display:inline-block;"></span>' +
                escapeHtml(c.name) + '</span>' +
                '<strong>' + formatMoney(c.totalInsuredValue) + '</strong></div>' +
                '<div class="geo-cell-meta">' + c.locationCount + ' locations · mean hazard ' +
                c.meanSiteHazard.toFixed(2) + '</div></div>';
        }).join("");
        communityTable.innerHTML = rows;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    function formatMoney(value) {
        if (!isFinite(value)) { return "–"; }
        if (value >= 1e9) { return "$" + (value / 1e9).toFixed(2) + "B"; }
        if (value >= 1e6) { return "$" + (value / 1e6).toFixed(1) + "M"; }
        if (value >= 1e3) { return "$" + (value / 1e3).toFixed(0) + "K"; }
        return "$" + Math.round(value);
    }

    function formatMoneyShort(value) {
        if (!isFinite(value)) { return "–"; }
        if (value >= 1e9) { return "$" + (value / 1e9).toFixed(1) + "B"; }
        if (value >= 1e6) { return "$" + (value / 1e6).toFixed(0) + "M"; }
        if (value >= 1e3) { return "$" + (value / 1e3).toFixed(0) + "K"; }
        return "$" + Math.round(value);
    }

    function setBusy(busy) {
        runBtn.disabled = busy;
        reloadBtn.disabled = busy;
    }

    function showAlert(message) {
        alertBox.textContent = message;
        alertBox.classList.remove("d-none");
    }

    function hideAlert() {
        alertBox.classList.add("d-none");
        alertBox.textContent = "";
    }

    function escapeHtml(text) {
        var div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }
}());
