// ReverseGeocoding/app.js
// Self-contained IIFE for the Reverse Geocoding page.
// Loads ArcGIS JS SDK 4.29 via require() and manages map, marker, API calls, and history.

(function () {
    "use strict";

    // ── Constants ──────────────────────────────────────────────────────────────
    var HISTORY_KEY = "rg_recent_lookups";
    var MAX_HISTORY = 5;
    var API_BASE = "/api/reversegeocoding";

    // ── State ──────────────────────────────────────────────────────────────────
    var currentResult = null;
    var markerGraphic = null;
    var graphicsLayer = null;
    var mapView = null;
    var GraphicClass = null;
    var webMercatorUtils = null;

    // ── DOM refs ───────────────────────────────────────────────────────────────
    var coordinateReadout = document.getElementById("rgCoordinateReadout");
    var latInput = document.getElementById("rgLatInput");
    var lngInput = document.getElementById("rgLngInput");
    var lookupBtn = document.getElementById("rgLookupBtn");
    var placeholder = document.getElementById("rgPlaceholder");
    var spinner = document.getElementById("rgSpinner");
    var notFound = document.getElementById("rgNotFound");
    var resultCard = document.getElementById("rgResultCard");
    var matchAddress = document.getElementById("rgMatchAddress");
    var locationTypeBadge = document.getElementById("rgLocationTypeBadge");
    var houseNumber = document.getElementById("rgHouseNumber");
    var street = document.getElementById("rgStreet");
    var city = document.getElementById("rgCity");
    var region = document.getElementById("rgRegion");
    var postal = document.getElementById("rgPostal");
    var country = document.getElementById("rgCountry");
    var resultLat = document.getElementById("rgResultLat");
    var resultLng = document.getElementById("rgResultLng");
    var saveHistoryBtn = document.getElementById("rgSaveHistoryBtn");
    var historyList = document.getElementById("rgHistoryList");
    var historyEmpty = document.getElementById("rgHistoryEmpty");
    var alertBox = document.getElementById("rgAlert");
    var alertMsg = document.getElementById("rgAlertMsg");

    // ── History helpers ────────────────────────────────────────────────────────
    function loadHistory() {
        try {
            return JSON.parse(sessionStorage.getItem(HISTORY_KEY) || "[]");
        } catch (_) {
            return [];
        }
    }

    function saveHistory(arr) {
        try {
            sessionStorage.setItem(HISTORY_KEY, JSON.stringify(arr));
        } catch (_) {}
    }

    function addToHistory(dto, lat, lng) {
        var history = loadHistory();
        var entry = { dto: dto, lat: lat, lng: lng };
        history.unshift(entry);
        history = history.slice(0, MAX_HISTORY);
        saveHistory(history);
        renderHistory(history);
    }

    function renderHistory(history) {
        if (!historyList) return;
        // Remove old items (keep the empty placeholder element)
        var items = historyList.querySelectorAll(".rg-lookup-item");
        items.forEach(function (el) { el.remove(); });

        if (history.length === 0) {
            if (historyEmpty) historyEmpty.style.display = "";
            return;
        }
        if (historyEmpty) historyEmpty.style.display = "none";

        history.forEach(function (entry, idx) {
            var item = document.createElement("div");
            item.className = "rg-lookup-item";
            item.setAttribute("role", "button");
            item.setAttribute("tabindex", "0");
            item.innerHTML =
                '<div class="rg-lookup-address">' + escapeHtml(entry.dto.matchedAddress || "Unknown address") + "</div>" +
                '<div class="rg-lookup-coords">Lat: ' + entry.lat.toFixed(6) + "  Lng: " + entry.lng.toFixed(6) + "</div>";

            item.addEventListener("click", function () {
                onHistoryItemClick(entry);
            });
            item.addEventListener("keydown", function (e) {
                if (e.key === "Enter" || e.key === " ") onHistoryItemClick(entry);
            });

            // Mark first (most recent) as active
            if (idx === 0) item.classList.add("active");

            historyList.appendChild(item);
        });
    }

    function onHistoryItemClick(entry) {
        if (mapView) {
            mapView.goTo({ center: [entry.lng, entry.lat], zoom: 14 });
        }
        placeMarker(entry.lat, entry.lng);
        updateCoordinateDisplay(entry.lat, entry.lng);
        renderResult(entry.dto, entry.lat, entry.lng);
    }

    // ── UI helpers ─────────────────────────────────────────────────────────────
    function escapeHtml(str) {
        var d = document.createElement("div");
        d.textContent = str;
        return d.innerHTML;
    }

    function showState(state) {
        // state: "placeholder" | "loading" | "notfound" | "result"
        placeholder.classList.add("d-none");
        spinner.classList.add("d-none");
        notFound.classList.add("d-none");
        resultCard.classList.add("d-none");

        if (state === "placeholder") placeholder.classList.remove("d-none");
        else if (state === "loading") spinner.classList.remove("d-none");
        else if (state === "notfound") notFound.classList.remove("d-none");
        else if (state === "result") resultCard.classList.remove("d-none");
    }

    function showAlert(message) {
        alertMsg.textContent = message;
        alertBox.classList.remove("d-none", "show");
        // Trigger Bootstrap fade-in
        requestAnimationFrame(function () { alertBox.classList.add("show"); });
    }

    function hideAlert() {
        alertBox.classList.remove("show");
        alertBox.classList.add("d-none");
    }

    function updateCoordinateDisplay(lat, lng) {
        coordinateReadout.textContent =
            "Lat: " + lat.toFixed(6) + "   Lng: " + lng.toFixed(6);
        coordinateReadout.classList.remove("text-muted");
        latInput.value = lat.toFixed(6);
        lngInput.value = lng.toFixed(6);
    }

    function getLocationTypeBadgeClass(locType) {
        if (!locType) return "type-other";
        var lt = locType.toLowerCase();
        if (lt === "streetaddress" || lt === "pointaddress" || lt === "streetint") return "type-street";
        if (lt === "poi") return "type-poi";
        if (lt.indexOf("postal") !== -1) return "type-postal";
        return "type-other";
    }

    function renderResult(dto, lat, lng) {
        currentResult = { dto: dto, lat: lat, lng: lng };

        matchAddress.textContent = dto.matchedAddress || "—";

        var ltClass = getLocationTypeBadgeClass(dto.locationType);
        locationTypeBadge.className = "badge rg-location-type-badge " + ltClass;
        locationTypeBadge.innerHTML = '<i class="fa-solid fa-map-pin me-1"></i>' + escapeHtml(dto.locationType || "Unknown");

        houseNumber.textContent = dto.houseNumber || "—";
        street.textContent = dto.street || "—";
        city.textContent = dto.city || "—";
        region.textContent = dto.region || "—";
        postal.textContent = dto.postalCode || "—";
        country.textContent = dto.countryCode || "—";
        resultLat.textContent = lat.toFixed(6);
        resultLng.textContent = lng.toFixed(6);

        showState("result");
    }

    // ── Marker management ──────────────────────────────────────────────────────
    function placeMarker(lat, lng) {
        if (!graphicsLayer || !GraphicClass) return;

        graphicsLayer.removeAll();

        // Get accent color from CSS custom property
        var accentColor = getComputedStyle(document.documentElement)
            .getPropertyValue("--accent").trim() || "#38a169";

        var point = { type: "point", longitude: lng, latitude: lat, spatialReference: { wkid: 4326 } };

        var symbol = {
            type: "simple-marker",
            color: accentColor,
            size: "14px",
            outline: { color: "#fff", width: 2 }
        };

        var graphic = new GraphicClass({ geometry: point, symbol: symbol });
        graphicsLayer.add(graphic);
        markerGraphic = graphic;
    }

    function updateMarkerPopup(dto) {
        if (!markerGraphic) return;
        markerGraphic.popupTemplate = {
            title: dto.matchedAddress || "Address",
            content: dto.locationType ? "Location type: " + dto.locationType : ""
        };
        if (mapView && mapView.popup) {
            mapView.popup.open({ features: [markerGraphic], location: markerGraphic.geometry });
        }
    }

    // ── API call ───────────────────────────────────────────────────────────────
    function fetchReverseGeocode(lat, lng) {
        hideAlert();
        showState("loading");
        lookupBtn.disabled = true;

        var url = API_BASE + "?lat=" + encodeURIComponent(lat) + "&lng=" + encodeURIComponent(lng);

        fetch(url)
            .then(function (response) {
                if (response.ok) {
                    return response.json().then(function (dto) {
                        renderResult(dto, lat, lng);
                        updateMarkerPopup(dto);
                    });
                } else if (response.status === 400) {
                    return response.json().then(function (body) {
                        showState("placeholder");
                        showAlert(body.error || "Bad request.");
                    });
                } else if (response.status === 404) {
                    showState("notfound");
                } else {
                    showState("placeholder");
                    if (window.showToast) {
                        window.showToast("Unexpected server error (" + response.status + ").", "danger");
                    }
                }
            })
            .catch(function (err) {
                showState("placeholder");
                if (window.showToast) {
                    window.showToast("Network error: " + err.message, "danger");
                }
            })
            .finally(function () {
                lookupBtn.disabled = false;
            });
    }

    // ── Manual input validation ────────────────────────────────────────────────
    function validateManualInputs() {
        var lat = parseFloat(latInput.value);
        var lng = parseFloat(lngInput.value);
        var valid = true;

        latInput.classList.remove("is-invalid");
        lngInput.classList.remove("is-invalid");

        if (isNaN(lat) || lat < -90 || lat > 90) {
            latInput.classList.add("is-invalid");
            valid = false;
        }
        if (isNaN(lng) || lng < -180 || lng > 180) {
            lngInput.classList.add("is-invalid");
            valid = false;
        }
        return valid ? { lat: lat, lng: lng } : null;
    }

    // ── Save to history button ─────────────────────────────────────────────────
    if (saveHistoryBtn) {
        saveHistoryBtn.addEventListener("click", function () {
            if (currentResult) {
                addToHistory(currentResult.dto, currentResult.lat, currentResult.lng);
            }
        });
    }

    // ── Look Up button ─────────────────────────────────────────────────────────
    if (lookupBtn) {
        lookupBtn.addEventListener("click", function () {
            var coords = validateManualInputs();
            if (!coords) return;

            updateCoordinateDisplay(coords.lat, coords.lng);
            if (mapView) {
                mapView.goTo({ center: [coords.lng, coords.lat], zoom: 14 });
            }
            placeMarker(coords.lat, coords.lng);
            fetchReverseGeocode(coords.lat, coords.lng);
        });
    }

    // ── ArcGIS Map initialization ──────────────────────────────────────────────
    window.require(
        [
            "esri/Map",
            "esri/views/MapView",
            "esri/Graphic",
            "esri/layers/GraphicsLayer",
            "esri/geometry/support/webMercatorUtils"
        ],
        function (EsriMap, MapView, Graphic, GraphicsLayer, WmUtils) {

            GraphicClass = Graphic;
            webMercatorUtils = WmUtils;

            graphicsLayer = new GraphicsLayer();

            var map = new EsriMap({
                basemap: "streets-navigation-vector",
                layers: [graphicsLayer]
            });

            mapView = new MapView({
                container: "rgMapView",
                map: map,
                center: [-98.5795, 39.8283],
                zoom: 4
            });

            mapView.on("click", function (event) {
                var mapPoint = event.mapPoint;
                if (!mapPoint) return;

                var lat, lng;

                // Project to WGS84 if Web Mercator (wkid 102100 or 3857)
                if (mapPoint.spatialReference &&
                    (mapPoint.spatialReference.wkid === 102100 ||
                     mapPoint.spatialReference.wkid === 3857)) {
                    var geo = webMercatorUtils.webMercatorToGeographic(mapPoint);
                    lat = geo.latitude;
                    lng = geo.longitude;
                } else {
                    lat = mapPoint.latitude;
                    lng = mapPoint.longitude;
                }

                updateCoordinateDisplay(lat, lng);
                placeMarker(lat, lng);
                fetchReverseGeocode(lat, lng);
            });

            // Restore history on load
            renderHistory(loadHistory());
        }
    );

}());
