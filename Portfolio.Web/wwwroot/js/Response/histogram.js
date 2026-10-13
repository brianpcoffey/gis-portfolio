// histogram.js — Emergency Response Coverage Optimizer: the response-time histogram.
// Draws the SVG chart of call volume per one-minute response bin, with today's stations as a dashed
// outline and the NFPA threshold markers. Publishes drawHistogram on window.ResponsePage (see context.js).
(function () {
    "use strict";

    var R = window.ResponsePage;
    var charts = window.PortfolioCharts;
    var fmt = window.PortfolioFormat;

    var state = R.state;
    var histogramSvg = R.dom.histogramSvg;
    var firstInput = R.dom.firstInput;
    var secondInput = R.dom.secondInput;

    // ── Response-time histogram ─────────────────────────────────────────────

    function drawHistogram(result) {
        charts.svg.clear(histogramSvg);

        var left = 11, right = 97, top = 5, bottom = 48;
        var svg = charts.svg;

        svg.line(histogramSvg, left, top, left, bottom, { width: 0.3 });
        svg.line(histogramSvg, left, bottom, right, bottom, { width: 0.3 });
        svg.text(histogramSvg, (left + right) / 2, 58, "Response Time (minutes)", { size: 2.6, anchor: "middle" });
        svg.text(histogramSvg, 3.0, (top + bottom) / 2, "Calls / year", { size: 2.6, anchor: "middle", rotate: -90 });

        if (!result || !result.assignments || result.assignments.length === 0) {
            svg.text(histogramSvg, (left + right) / 2, (top + bottom) / 2,
                "Run an optimization to build the distribution", { size: 3, anchor: "middle" });
            charts.describe(histogramSvg, "Response-time histogram. No optimization has been run yet.");
            return;
        }

        var binCount = 14;
        var optimizedBins = binWeights(result.assignments, binCount);
        var baselineBins = binWeights(result.baselineAssignments, binCount);

        var maxWeight = 1;
        var i;
        for (i = 0; i < binCount; i++) {
            if (optimizedBins[i] > maxWeight) { maxWeight = optimizedBins[i]; }
            if (baselineBins[i] > maxWeight) { maxWeight = baselineBins[i]; }
        }

        var xFor = charts.scale.linear([0, binCount], [left, right]);
        var yFor = charts.scale.linear([0, maxWeight], [bottom, top]);

        // Y gridlines at quarter steps.
        var q;
        for (q = 1; q <= 4; q++) {
            var gy = yFor(maxWeight * q / 4);
            svg.line(histogramSvg, left, gy, right, gy, { stroke: "var(--border)", width: 0.18 });
            svg.text(histogramSvg, left - 1.2, gy + 0.9, formatCount(maxWeight * q / 4), { size: 2.2, anchor: "end" });
        }

        // Optimized bars.
        var barWidth = (right - left) / binCount;
        for (i = 0; i < binCount; i++) {
            if (optimizedBins[i] <= 0) { continue; }
            histogramSvg.appendChild(svg.el("rect", {
                x: (xFor(i) + barWidth * 0.12).toFixed(2),
                y: yFor(optimizedBins[i]).toFixed(2),
                width: (barWidth * 0.76).toFixed(2),
                height: Math.max(bottom - yFor(optimizedBins[i]), 0).toFixed(2),
                fill: "var(--chart-series)",
                "fill-opacity": "0.85"
            }));
        }

        // Baseline as a translucent step outline so the improvement reads in one glance.
        if (result.baselineAssignments && result.baselineAssignments.length > 0) {
            var points = [];
            for (i = 0; i < binCount; i++) {
                points.push(xFor(i).toFixed(2) + "," + yFor(baselineBins[i]).toFixed(2));
                points.push(xFor(i + 1).toFixed(2) + "," + yFor(baselineBins[i]).toFixed(2));
            }
            histogramSvg.appendChild(svg.el("polyline", {
                points: points.join(" "),
                fill: "none",
                stroke: "var(--text-muted)",
                "stroke-width": "0.4",
                "stroke-dasharray": "1.4 1"
            }));
            svg.text(histogramSvg, right, top + 2, "outline = today", { size: 2.2, anchor: "end" });
        }

        // X ticks every two minutes.
        for (i = 0; i <= binCount; i += 2) {
            var tx = xFor(i);
            svg.line(histogramSvg, tx, bottom, tx, bottom + 1.1, { width: 0.25 });
            svg.text(histogramSvg, tx, bottom + 4.2, String(i), { size: 2.3, anchor: "middle" });
        }

        // NFPA threshold markers.
        markMinute(parseFloat(firstInput.value), "first-due", "var(--status-success-fill)", "var(--status-success-text)",
            top, bottom, binCount, xFor);
        markMinute(parseFloat(secondInput.value), "ALS", "var(--status-warning-fill)", "var(--status-warning-text)",
            top, bottom, binCount, xFor);

        // Shade everything past the optimized p90 — the tail the standard is written about.
        var p90 = result.optimized.p90Minutes;
        if (p90 > 0 && p90 < binCount) {
            histogramSvg.appendChild(svg.el("rect", {
                x: xFor(p90).toFixed(2),
                y: String(top),
                width: Math.max(right - xFor(p90), 0).toFixed(2),
                height: (bottom - top).toFixed(2),
                fill: "var(--status-danger-fill)",
                "fill-opacity": "0.10"
            }));
            svg.text(histogramSvg, Math.min(xFor(p90) + 1, right - 8), top + 5.5,
                "p90 " + fmt.number(p90, { decimals: 1 }), { size: 2.2, anchor: "start", fill: "var(--status-danger-text)" });
        }

        charts.describe(histogramSvg, "Response-time histogram, call volume per one-minute bin. Optimized p90 is " +
            fmt.unit(p90, "min") + "; the dashed outline shows today's stations.");
    }

    function markMinute(minute, label, lineColor, textColor, top, bottom, binCount, xFor) {
        if (!isFinite(minute) || minute <= 0 || minute > binCount) { return; }
        var x = xFor(minute);
        var line = charts.svg.line(histogramSvg, x, top, x, bottom, { stroke: lineColor, width: 0.35 });
        line.setAttribute("stroke-dasharray", "1.2 1");
        charts.svg.text(histogramSvg, x, top - 0.6, label, { size: 2.2, anchor: "middle", fill: textColor });
    }

    // Sums call volume into one-minute bins, so the y axis is calls rather than incidents.
    function binWeights(assignments, binCount) {
        var bins = [];
        var i;
        for (i = 0; i < binCount; i++) { bins.push(0); }
        if (!assignments) { return bins; }

        assignments.forEach(function (a) {
            var demand = state.demandById[a.demandPointId];
            if (!demand) { return; }
            var bin = Math.floor(a.responseMinutes);
            if (bin < 0) { bin = 0; }
            if (bin >= binCount) { bin = binCount - 1; }
            bins[bin] += demand.callVolume;
        });

        return bins;
    }

    function formatCount(value) {
        if (value >= 1000) { return fmt.number(value / 1000, { decimals: 1 }) + "k"; }
        return String(Math.round(value));
    }

    R.drawHistogram = drawHistogram;
}());
