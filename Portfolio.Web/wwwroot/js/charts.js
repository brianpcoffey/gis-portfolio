// window.PortfolioCharts — theme tokens, SVG helpers, scales, ticks, seeded RNG, legend and chart descriptions.
(function (g) {
  "use strict";
  var SVGNS = "http://www.w3.org/2000/svg";

  // Resolved CSS custom property, e.g. token("accent") reads --accent from <body>.
  function token(name) {
    return g.getComputedStyle(document.body).getPropertyValue("--" + name).trim();
  }

  // --chart-1..--chart-8 resolved for the current theme (canvas/Leaflet need concrete colours).
  function palette(n) {
    var out = [], count = n === undefined ? 8 : n;
    for (var i = 0; i < count; i++) { out.push(token("chart-" + ((i % 8) + 1))); }
    return out;
  }

  // 'success'|'danger'|'warning'|'info' -> resolved --status-*-fill
  function status(kind) { return token("status-" + kind + "-fill"); }

  function onThemeChange(cb) { g.addEventListener("themeChanged", cb); }

  var svg = {
    el: function (tag, attrs) {
      var n = document.createElementNS(SVGNS, tag);
      if (attrs) { Object.keys(attrs).forEach(function (k) { n.setAttribute(k, attrs[k]); }); }
      return n;
    },
    text: function (root, x, y, content, o) {
      o = o || {};
      var t = svg.el("text", {
        x: x.toFixed(2), y: y.toFixed(2),
        "font-size": String(o.size === undefined ? 2.2 : o.size),
        "text-anchor": o.anchor || "start",
        fill: o.fill || "var(--text-muted)"
      });
      if (o.rotate) { t.setAttribute("transform", "rotate(" + o.rotate + " " + x.toFixed(2) + " " + y.toFixed(2) + ")"); }
      t.textContent = content;
      root.appendChild(t);
      return t;
    },
    line: function (root, x1, y1, x2, y2, o) {
      o = o || {};
      var l = svg.el("line", {
        x1: x1.toFixed(2), y1: y1.toFixed(2), x2: x2.toFixed(2), y2: y2.toFixed(2),
        stroke: o.stroke || "var(--text-muted)", "stroke-width": String(o.width === undefined ? 0.25 : o.width)
      });
      root.appendChild(l);
      return l;
    },
    clear: function (root) { root.replaceChildren(); }
  };

  function clamp01(t) { return Math.max(0, Math.min(1, t)); }
  var scale = {
    linear: function (d, r) {
      return function (v) { return r[0] + clamp01((v - d[0]) / (d[1] - d[0])) * (r[1] - r[0]); };
    },
    log: function (d, r) {
      var a = Math.log10(d[0]), b = Math.log10(d[1]);
      return function (v) { return r[0] + clamp01((Math.log10(Math.max(v, d[0])) - a) / (b - a)) * (r[1] - r[0]); };
    }
  };

  // "Nice" tick values covering [min, max] with roughly `count` intervals.
  function ticks(min, max, count) {
    count = count || 5;
    if (!(max > min)) { return [min]; }
    var raw = (max - min) / count;
    var mag = Math.pow(10, Math.floor(Math.log10(raw)));
    var f = raw / mag;
    var step = (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * mag;
    var out = [];
    for (var v = Math.ceil(min / step) * step; v <= max + step * 1e-9; v += step) {
      out.push(Math.round(v / step) * step);
    }
    return out;
  }

  // Linear blend of two #rrggbb colours -> "rgb(r,g,b)".
  function mix(from, to, t) {
    var f = parseInt(from.slice(1), 16);
    var h = parseInt(to.slice(1), 16);
    var r = Math.round(((f >> 16) & 255) + (((h >> 16) & 255) - ((f >> 16) & 255)) * t);
    var gr = Math.round(((f >> 8) & 255) + (((h >> 8) & 255) - ((f >> 8) & 255)) * t);
    var b = Math.round((f & 255) + ((h & 255) - (f & 255)) * t);
    return "rgb(" + r + "," + gr + "," + b + ")";
  }

  // Seeded LCG, byte-identical to the per-page copies: seed = seed * 48271 % 2147483647.
  function rng(seed) {
    var s = seed;
    return function () { s = (s * 48271) % 2147483647; return s / 2147483647; };
  }

  // legend(container, [{ label, color }]) — swatch + text, built with DOM (no innerHTML).
  function legend(container, items) {
    container.replaceChildren();
    container.classList.add("legend");
    items.forEach(function (it) {
      var row = document.createElement("span");
      row.className = "legend-item";
      var sw = document.createElement("span");
      sw.className = "legend-swatch";
      sw.setAttribute("aria-hidden", "true");
      sw.style.backgroundColor = it.color;
      row.appendChild(sw);
      row.appendChild(document.createTextNode(it.label));
      container.appendChild(row);
    });
  }

  var describeSeq = 0;
  // Adds a visually-hidden summary after the chart and points aria-describedby at it.
  function describe(chart, summaryText) {
    var id = chart.getAttribute("data-describe-id");
    var node = id && document.getElementById(id);
    if (!node) {
      id = "chartDesc" + (++describeSeq);
      node = document.createElement("p");
      node.id = id;
      node.className = "visually-hidden";
      chart.parentNode.insertBefore(node, chart.nextSibling);
      chart.setAttribute("data-describe-id", id);
      chart.setAttribute("aria-describedby", id);
    }
    node.textContent = summaryText;
    return node;
  }

  g.PortfolioCharts = {
    token: token, palette: palette, status: status, onThemeChange: onThemeChange,
    svg: svg, scale: scale, ticks: ticks, mix: mix, rng: rng, legend: legend, describe: describe
  };
}(window));
