// window.PortfolioEsri — loads the ArcGIS JS API and its widget theme, and keeps the theme
// in step with the site's light/dark toggle. Replaces the inline loaders on the ArcGIS pages.
//
//   <script src="~/js/esri-theme.js"></script>
//   <script>PortfolioEsri.load();</script>          // theme CSS now, API script appended
//   PortfolioEsri.loadTheme();                      // theme CSS only (page loads the API itself)
//
// Subresource Integrity (SEC-09, WP-46). `integrity` maps each absolute URL this loader requests
// to its sha384 digest; a listed URL is fetched with integrity + crossorigin="anonymous" (both
// set before src/href), so the browser blocks the file if its bytes ever change. A URL missing
// from the map loads without SRI. The three pinned files, all under https://js.arcgis.com/4.29/:
//   init.js                      the API loader (the explicit file URL, not the directory URL)
//   esri/themes/light/main.css   widget theme, light
//   esri/themes/dark/main.css    widget theme, dark
// The page that loads init.js from markup (Plant Operations Dashboard) carries the same digest
// in its own <script integrity> attribute. SRI covers only these files, not the ~100 AMD
// modules init.js loads afterwards. The pins fail closed: if Esri republishes 4.29 in place, or
// VERSION changes without new digests, the pages stop loading the map until they are refreshed.
// To recompute a digest (needs network; compare two downloads before trusting it):
//   curl -s https://js.arcgis.com/4.29/init.js | openssl dgst -sha384 -binary | openssl base64 -A
// then prefix the output with "sha384-" here and in Pages/Projects/PlantOperationsDashboard/Index.cshtml.
(function (g) {
  "use strict";

  var VERSION = "4.29";
  var BASE = "https://js.arcgis.com/" + VERSION + "/";   // the version folder (assets, themes)
  var SCRIPT_URL = BASE + "init.js";                      // the API loader itself
  var integrity = {};
  var themeLink = null;

  function themeHref(dark) { return BASE + "esri/themes/" + (dark ? "dark" : "light") + "/main.css"; }
  function isDark() { return document.documentElement.getAttribute("data-bs-theme") === "dark"; }

  // Keys are built by the same expressions that build the requested URLs, so they cannot drift.
  integrity[SCRIPT_URL] = "sha384-Wbv6M4lOxynphwP8EvtO0ny1cq4SjtPEzNKHaJ5KyOoVZZAVtT2NH/WmrDRYZN0S";
  integrity[themeHref(false)] = "sha384-f8NuhFNMt2lrnbQ6jdNBW6hKxHKLYtpdjcsOCTSsZ5EjQhZV5dNWU7T1WSqHF7qT";
  integrity[themeHref(true)] = "sha384-FsBIl6r6hx6Km/SGKQLxOr+6GTq9lySC0kcfk+ln29alVyYnhOrL3xscLDka048v";

  // Must run before the element's src/href is set: that assignment is what starts the fetch.
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
  var loadPromise = null;
  function load() {
    if (loadPromise) { return loadPromise; }
    loadTheme();
    loadPromise = new Promise(function (resolve, reject) {
      var s = document.createElement("script");
      applyIntegrity(s, SCRIPT_URL);
      s.src = SCRIPT_URL;
      s.onload = function () { resolve(g.require); };
      s.onerror = function () { reject(new Error("Failed to load the ArcGIS API.")); };
      document.head.appendChild(s);
    });
    return loadPromise;
  }

  g.PortfolioEsri = { version: VERSION, baseUrl: BASE, scriptUrl: SCRIPT_URL, themeHref: themeHref, integrity: integrity, loadTheme: loadTheme, load: load };
}(window));
