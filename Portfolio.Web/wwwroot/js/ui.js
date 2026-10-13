// window.PortfolioUi — shared page-level UI helpers (announce, inline alerts, busy state, run flow).
// Loaded after toasts.js and dom-safe.js. escapeHtml/el delegate to PortfolioDom (the single escaper).
(function (g) {
  "use strict";

  // Sends a message to the global polite live region (#a11yAnnounce). Clearing first and setting on a
  // timeout makes a repeated identical message fire again instead of being swallowed.
  function announce(message) {
    var live = document.getElementById("a11yAnnounce");
    if (!live) { return; }
    live.textContent = "";
    g.setTimeout(function () { live.textContent = message; }, 40);
  }

  // inlineAlert(element) -> { show(msg), hide() }. Text only, never markup; toggles d-none.
  // show() returns true when the alert box exists and now holds the message, false when there is no box.
  function inlineAlert(element) {
    return {
      show: function (message) {
        if (!element) { return false; }
        element.textContent = message;
        element.classList.remove("d-none");
        return true;
      },
      hide: function () {
        if (!element) { return; }
        element.classList.add("d-none");
        element.textContent = "";
      }
    };
  }

  // busy({ buttons, region, label }) -> done(). Marks the region aria-busy, flags the buttons
  // aria-disabled (not `disabled`, so focus is kept) and swallows clicks until done() is called.
  function busy(opts) {
    opts = opts || {};
    var buttons = (opts.buttons || []).filter(Boolean);
    var region = opts.region || null;
    var guards = [];
    if (region) { region.setAttribute("aria-busy", "true"); region.classList.add("is-busy"); }
    buttons.forEach(function (b) {
      b.setAttribute("aria-disabled", "true");
      b.classList.add("is-busy");
      var guard = function (e) { e.preventDefault(); e.stopImmediatePropagation(); };
      b.addEventListener("click", guard, true);
      guards.push([b, guard]);
    });
    if (opts.label) { announce(opts.label); }
    var finished = false;
    return function done() {
      if (finished) { return; }
      finished = true;
      if (region) { region.removeAttribute("aria-busy"); region.classList.remove("is-busy"); }
      guards.forEach(function (pair) {
        pair[0].removeAttribute("aria-disabled");
        pair[0].classList.remove("is-busy");
        pair[0].removeEventListener("click", pair[1], true);
      });
    };
  }

  // runAction({ trigger, alert, region, request, render, summary }): hide alert -> busy -> await
  // request() -> render(result) -> announce(summary(result)); on error show alert; always done().
  // A summary that returns "" announces nothing (a page uses that to drop a stale result). An error is
  // announced only when there is no alert box to show it in: the box is role="alert", so it speaks itself.
  function runAction(opts) {
    var al = opts.alert;
    if (al && al.hide) { al.hide(); }
    var done = busy({ buttons: [opts.trigger], region: opts.region });
    return Promise.resolve()
      .then(function () { return opts.request(); })
      .then(function (result) {
        if (opts.render) { opts.render(result); }
        if (opts.summary) {
          var text = opts.summary(result);
          if (text) { announce(text); }
        }
        return result;
      })
      .catch(function (err) {
        var msg = (err && err.message) || "An unexpected error occurred.";
        if (!(al && al.show && al.show(msg))) { announce(msg); }
      })
      .then(function (r) { done(); return r; });
  }

  g.PortfolioUi = {
    announce: announce,
    inlineAlert: inlineAlert,
    busy: busy,
    runAction: runAction,
    escapeHtml: function (s) { return g.PortfolioDom.escapeHtml(s); },
    el: function () { return g.PortfolioDom.el.apply(null, arguments); }
  };
}(window));
