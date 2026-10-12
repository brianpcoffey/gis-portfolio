// window.PortfolioDom — the only sanctioned way to put untrusted text into markup.
(function (g) {
  "use strict";
  var MAP = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" };
  function escapeHtml(v) { return v === null || v === undefined ? "" : String(v).replace(/[&<>"']/g, function (c) { return MAP[c]; }); }
  // el("span", { class: "badge", "data-id": id, text: label }, child1, child2) — attributes via setAttribute, text via textContent.
  function el(tag, attrs) { var n = document.createElement(tag); if (attrs) Object.keys(attrs).forEach(function (k) {
      if (k === "text") n.textContent = attrs[k]; else if (k.slice(0, 2) !== "on") n.setAttribute(k, attrs[k]); });
    for (var i = 2; i < arguments.length; i++) { var c = arguments[i]; if (c !== null && c !== undefined) n.append(c); } return n; }
  g.PortfolioDom = { escapeHtml: escapeHtml, el: el };
}(window));
