// window.PortfolioFormat — one set of number/unit formatting conventions (en-US).
(function (g) {
  "use strict";

  var cache = {};
  function nf(o) {
    var key = JSON.stringify(o);
    return cache[key] || (cache[key] = new Intl.NumberFormat("en-US", o));
  }
  function bad(v) { return v === null || v === undefined || v === "" || !isFinite(Number(v)); }
  var DASH = "–";

  // 1234.5 -> "1,235" (decimals = 0) or "1,234.5" (decimals = 1)
  function number(v, o) {
    if (bad(v)) { return DASH; }
    var d = (o && o.decimals) || 0;
    return nf({ minimumFractionDigits: d, maximumFractionDigits: d }).format(Number(v));
  }

  // 94.1 or (fraction: true) 0.941 -> "94.1%"
  function percent(v, o) {
    if (bad(v)) { return DASH; }
    o = o || {};
    var d = o.decimals === undefined ? 1 : o.decimals;
    var n = o.fraction ? Number(v) * 100 : Number(v);
    return nf({ minimumFractionDigits: d, maximumFractionDigits: d }).format(n) + "%";
  }

  // full: "$800,000"; compact: "$832.9M" ($1.2B, $45.3K, below 1,000 plain)
  function money(v, o) {
    if (bad(v)) { return DASH; }
    var n = Number(v);
    if (!(o && o.compact)) {
      return nf({ style: "currency", currency: "USD", maximumFractionDigits: 0 }).format(n);
    }
    var abs = Math.abs(n);
    var sign = n < 0 ? "-" : "";
    var steps = [[1e9, "B"], [1e6, "M"], [1e3, "K"]];
    for (var i = 0; i < steps.length; i++) {
      if (abs >= steps[i][0]) {
        return sign + "$" + nf({ minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(abs / steps[i][0]) + steps[i][1];
      }
    }
    return sign + "$" + nf({ maximumFractionDigits: 0 }).format(abs);
  }

  // unit(61.43, "km") -> "61.4 km"
  function unit(v, u, o) {
    if (bad(v)) { return DASH; }
    var d = o && o.decimals !== undefined ? o.decimals : 1;
    return nf({ minimumFractionDigits: d, maximumFractionDigits: d }).format(Number(v)) + " " + u;
  }

  // minutes since midnight -> "HH:MM" (wraps at 24 h)
  function clock(minutes) {
    if (bad(minutes)) { return DASH; }
    var total = Math.round(Number(minutes));
    var h = ((Math.floor(total / 60) % 24) + 24) % 24;
    var m = ((total % 60) + 60) % 60;
    return (h < 10 ? "0" : "") + h + ":" + (m < 10 ? "0" : "") + m;
  }

  function coord(lat, lng, decimals) {
    if (bad(lat) || bad(lng)) { return DASH; }
    var d = decimals === undefined ? 5 : decimals;
    return Number(lat).toFixed(d) + ", " + Number(lng).toFixed(d);
  }

  function yesNo(b) { return b ? "Yes" : "No"; }

  g.PortfolioFormat = { number: number, percent: percent, money: money, unit: unit, clock: clock, coord: coord, yesNo: yesNo };
}(window));
