// window.PortfolioEsri — loads the ArcGIS JS API and its widget theme, and keeps the theme
// in step with the site's light/dark toggle. Replaces the inline loaders on the ArcGIS pages.
//
//   <script src="~/js/esri-theme.js"></script>
//   <script>PortfolioEsri.load();</script>          // theme CSS now, API script appended
//   PortfolioEsri.loadTheme();                      // theme CSS only (page loads the API itself)
//   PortfolioEsri.load().catch(function () { PortfolioEsri.showLoadError(mapEl, "Search still works."); });
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
  var SCRIPT_URL = BASE + "init.js";
  var integrity = {};
  var themeLink = null;

  function themeHref(dark) { return BASE + "esri/themes/" + (dark ? "dark" : "light") + "/main.css"; }
  function isDark() { return document.documentElement.getAttribute("data-bs-theme") === "dark"; }

  // Keys are built by the same expressions that build the requested URLs, so they cannot drift.
  integrity[SCRIPT_URL] = "sha384-Wbv6M4lOxynphwP8EvtO0ny1cq4SjtPEzNKHaJ5KyOoVZZAVtT2NH/WmrDRYZN0S";
  integrity[themeHref(false)] = "sha384-f8NuhFNMt2lrnbQ6jdNBW6hKxHKLYtpdjcsOCTSsZ5EjQhZV5dNWU7T1WSqHF7qT";
  integrity[themeHref(true)] = "sha384-FsBIl6r6hx6Km/SGKQLxOr+6GTq9lySC0kcfk+ln29alVyYnhOrL3xscLDka048v";

  // Must run before the element's src/href is set and before it is inserted: a new element starts
  // fetching when it is inserted, the theme link when its href changes. crossOrigin is only set
  // when it changes, because touching it on a connected <link> can trigger a re-fetch.
  function applyIntegrity(el, url) {
    if (integrity[url]) {
      el.integrity = integrity[url];
      if (el.crossOrigin !== "anonymous") { el.crossOrigin = "anonymous"; }
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
      // themeChanged also fires on page load and on every display-preference change; only a real
      // light/dark switch changes the URL. The two themes have different hashes, so the new one is
      // applied before the href; an unpinned URL drops the attributes instead.
      var next = themeHref(!!(e.detail && e.detail.dark));
      if (themeLink.getAttribute("href") === next) { return; }
      if (!integrity[next]) {
        themeLink.removeAttribute("integrity");
        themeLink.removeAttribute("crossorigin");
      }
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

  // Fills a map container with an inline notice when load() rejects (network failure, or the SRI pin
  // blocking a changed file). The box is role="alert", so it is announced once. `hint` is an optional
  // second sentence saying what still works on the page. Replaces whatever the container held (a
  // loading spinner); returns the box, or null when there is no container.
  function showLoadError(container, hint) {
    if (!container) { return null; }
    var box = document.createElement("div");
    box.className = "alert alert-warning esri-load-error m-3";
    box.setAttribute("role", "alert");
    var icon = document.createElement("i");
    icon.className = "fa-solid fa-triangle-exclamation me-1";
    icon.setAttribute("aria-hidden", "true");
    box.appendChild(icon);
    box.appendChild(document.createTextNode(
      "The map could not be loaded. Check your connection and reload the page to try again." + (hint ? " " + hint : "")));
    container.replaceChildren(box);
    return box;
  }

  // A start-up timeout that only counts while the page is visible. Browsers pause rendering in a
  // background tab, so a map opened there cannot become ready; counting that time would report a
  // working map as failed. Returns { clear() }.
  function visibleTimeout(ms, onTimeout) {
    var remaining = ms, startedAt = 0, timer = null, done = false;
    function start() {
      if (done || timer !== null || document.visibilityState !== "visible") { return; }
      startedAt = Date.now();
      timer = setTimeout(fire, remaining);
    }
    function pause() {
      if (timer === null) { return; }
      clearTimeout(timer);
      timer = null;
      remaining -= Date.now() - startedAt;
    }
    function onVisibility() { if (document.visibilityState === "visible") { start(); } else { pause(); } }
    function stop() { done = true; pause(); document.removeEventListener("visibilitychange", onVisibility); }
    function fire() { timer = null; stop(); onTimeout(); }
    document.addEventListener("visibilitychange", onVisibility);
    start();
    return { clear: stop };
  }

  g.PortfolioEsri = { version: VERSION, baseUrl: BASE, scriptUrl: SCRIPT_URL, themeHref: themeHref, integrity: integrity, loadTheme: loadTheme, load: load, showLoadError: showLoadError, visibleTimeout: visibleTimeout };
}(window));
