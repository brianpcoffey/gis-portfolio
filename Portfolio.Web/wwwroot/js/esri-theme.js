// window.PortfolioEsri — loads the ArcGIS JS API and its widget theme, and keeps the theme
// in step with the site's light/dark toggle. Replaces the inline loaders on the ArcGIS pages.
//
//   <script src="~/js/esri-theme.js"></script>
//   <script>PortfolioEsri.load();</script>          // theme CSS now, API script appended
//   PortfolioEsri.loadTheme();                      // theme CSS only (page loads the API itself)
//
// SRI hook (WP-46): fill `integrity` with { "<absolute url>": "sha384-..." }; any URL listed
// gets integrity + crossorigin="anonymous". Unlisted URLs load without SRI.
(function (g) {
  "use strict";

  var VERSION = "4.29";
  var BASE = "https://js.arcgis.com/" + VERSION + "/";
  var integrity = {};
  var themeLink = null;

  function themeHref(dark) { return BASE + "esri/themes/" + (dark ? "dark" : "light") + "/main.css"; }
  function isDark() { return document.documentElement.getAttribute("data-bs-theme") === "dark"; }

  function applyIntegrity(el, url) {
    if (integrity[url]) {
      el.integrity = integrity[url];
      el.crossOrigin = "anonymous";
    }
  }

  // Inserts (once) the theme stylesheet matching the current theme; swaps it on themeChanged.
  function loadTheme() {
    if (themeLink) { return themeLink; }
    themeLink = document.createElement("link");
    themeLink.rel = "stylesheet";
    themeLink.id = "esriThemeCss";
    var href = themeHref(isDark());
    applyIntegrity(themeLink, href);
    themeLink.href = href;
    document.head.appendChild(themeLink);
    g.addEventListener("themeChanged", function (e) {
      // The two themes have different hashes, so drop integrity unless one is registered.
      var next = themeHref(!!(e.detail && e.detail.dark));
      themeLink.removeAttribute("integrity");
      themeLink.removeAttribute("crossorigin");
      applyIntegrity(themeLink, next);
      themeLink.href = next;
    });
    return themeLink;
  }

  // Theme plus the API script. Resolves when the API script has loaded.
  function load() {
    loadTheme();
    return new Promise(function (resolve, reject) {
      var s = document.createElement("script");
      s.src = BASE;
      applyIntegrity(s, BASE);
      s.onload = function () { resolve(g.require); };
      s.onerror = function () { reject(new Error("Failed to load the ArcGIS API.")); };
      document.head.appendChild(s);
    });
  }

  g.PortfolioEsri = { version: VERSION, baseUrl: BASE, themeHref: themeHref, integrity: integrity, loadTheme: loadTheme, load: load };
}(window));
