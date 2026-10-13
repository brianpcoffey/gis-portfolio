// Batch Geocoding Page — self-contained IIFE, no ES module syntax.
(function () {
    "use strict";

    // ── State ──────────────────────────────────────────────────────────────
    var _file = null;           // raw File object
    var _results = [];          // full result array from API
    var _dataTable = null;      // DataTables instance

    // ── Element refs ────────────────────────────────────────────────────────
    var uploadZone    = document.getElementById("uploadZone");
    var csvFileInput  = document.getElementById("csvFileInput");
    var fileInfoCard  = document.getElementById("fileInfoCard");
    var fileInfoName  = document.getElementById("fileInfoName");
    var fileInfoSize  = document.getElementById("fileInfoSize");
    var fileInfoRows  = document.getElementById("fileInfoRows");
    var btnGeocode    = document.getElementById("btnGeocode");
    var btnClear      = document.getElementById("btnClear");
    var btnExport     = document.getElementById("btnExport");
    var spinnerOverlay       = document.getElementById("spinnerOverlay");
    var resultsPlaceholder   = document.getElementById("resultsPlaceholder");
    var resultsContent       = document.getElementById("resultsContent");
    var uploadAlert          = document.getElementById("uploadAlert");
    var uploadAlertMsg       = document.getElementById("uploadAlertMsg");
    var apiAlert             = document.getElementById("apiAlert");
    var apiAlertMsg          = document.getElementById("apiAlertMsg");
    var kpiTotal             = document.getElementById("kpiTotal");
    var kpiMatched           = document.getElementById("kpiMatched");
    var kpiUnmatched         = document.getElementById("kpiUnmatched");
    var summaryText          = document.getElementById("summaryText");
    var filterBtns           = document.querySelectorAll(".filter-btn");
    var fmt                  = window.PortfolioFormat;
    var announce             = window.PortfolioUi.announce;

    // ── CSV helpers ─────────────────────────────────────────────────────────

    // Simple state-machine CSV parser that handles quoted fields with commas.
    function parseCSVLine(line) {
        var fields = [];
        var field = "";
        var inQuotes = false;
        for (var i = 0; i < line.length; i++) {
            var ch = line[i];
            if (ch === '"') {
                if (inQuotes && line[i + 1] === '"') {
                    field += '"';
                    i++;
                } else {
                    inQuotes = !inQuotes;
                }
            } else if (ch === "," && !inQuotes) {
                fields.push(field.trim());
                field = "";
            } else {
                field += ch;
            }
        }
        fields.push(field.trim());
        return fields;
    }

    function countDataRows(csvText) {
        var lines = csvText.split(/\r?\n/).filter(function (l) { return l.trim() !== ""; });
        return Math.max(0, lines.length - 1); // subtract header row
    }

    function formatBytes(bytes) {
        if (bytes < 1024) return fmt.number(bytes) + " B";
        if (bytes < 1048576) return fmt.unit(bytes / 1024, "KB");
        return fmt.unit(bytes / 1048576, "MB");
    }

    // ── Upload zone wiring ──────────────────────────────────────────────────

    uploadZone.addEventListener("click", function () {
        csvFileInput.click();
    });

    uploadZone.addEventListener("keydown", function (e) {
        if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            csvFileInput.click();
        }
    });

    uploadZone.addEventListener("dragover", function (e) {
        e.preventDefault();
        uploadZone.classList.add("dragover");
    });

    uploadZone.addEventListener("dragleave", function () {
        uploadZone.classList.remove("dragover");
    });

    uploadZone.addEventListener("drop", function (e) {
        e.preventDefault();
        uploadZone.classList.remove("dragover");
        var files = e.dataTransfer.files;
        if (files && files.length > 0) {
            handleFile(files[0]);
        }
    });

    csvFileInput.addEventListener("change", function () {
        if (csvFileInput.files && csvFileInput.files.length > 0) {
            handleFile(csvFileInput.files[0]);
        }
    });

    function handleFile(file) {
        hideUploadAlert();
        hideApiAlert();

        if (!file.name.toLowerCase().endsWith(".csv")) {
            showUploadAlert("Please select a valid .csv file.");
            return;
        }

        _file = file;
        var reader = new FileReader();
        reader.onload = function (e) {
            var text = e.target.result;
            var rowCount = countDataRows(text);

            if (rowCount === 0) {
                showUploadAlert("The CSV file has no data rows. Please include at least one address row after the header.");
                _file = null;
                return;
            }

            fileInfoName.textContent = file.name;
            fileInfoSize.textContent = formatBytes(file.size);
            fileInfoRows.textContent = fmt.number(rowCount);

            fileInfoCard.classList.remove("d-none");
            btnGeocode.disabled = false;
            btnClear.classList.remove("d-none");
        };
        reader.readAsText(file);
    }

    // ── Clear ───────────────────────────────────────────────────────────────

    btnClear.addEventListener("click", function () {
        resetUpload();
    });

    function resetUpload() {
        _file = null;
        csvFileInput.value = "";
        fileInfoCard.classList.add("d-none");
        btnGeocode.disabled = true;
        btnClear.classList.add("d-none");
        hideUploadAlert();
        hideApiAlert();
    }

    // ── Alert helpers ───────────────────────────────────────────────────────

    function showUploadAlert(msg) {
        uploadAlertMsg.textContent = msg;
        uploadAlert.classList.remove("d-none");
        uploadAlert.classList.add("show");
    }

    function hideUploadAlert() {
        uploadAlert.classList.add("d-none");
        uploadAlert.classList.remove("show");
    }

    function showApiAlert(msg) {
        apiAlertMsg.textContent = msg;
        apiAlert.classList.remove("d-none");
        apiAlert.classList.add("show");
    }

    function hideApiAlert() {
        apiAlert.classList.add("d-none");
        apiAlert.classList.remove("show");
    }

    // ── Geocode button ──────────────────────────────────────────────────────

    btnGeocode.addEventListener("click", function () {
        if (!_file) return;
        runGeocode();
    });

    function runGeocode() {
        hideApiAlert();
        var done = setLoading(true);

        var formData = new FormData();
        formData.append("file", _file);

        apiPostForm(window.PortfolioApi.routes.geocoding.batchSync, formData)
        .then(function (data) {
            _results = data;
            renderResults(data);
            var total = data.length;
            var msg = "Geocoding complete \u2014 " + fmt.number(total) + " address" + (total !== 1 ? "es" : "") + " processed.";
            showToast(msg, "success");
            announce(msg + " " + summaryText.textContent + ".");
        })
        .catch(function (err) {
            showApiAlert(err.message || "Failed to geocode addresses.");
            window.showToast(err.message || "Geocoding failed.", "danger");
            announce(err.message || "Geocoding failed.");
        })
        .finally(function () {
            done();
        });
    }

    // Shows the overlay and marks the button busy (aria-disabled + click guard, so focus stays on it).
    // Returns the function that undoes both.
    function setLoading() {
        spinnerOverlay.classList.remove("d-none");
        var undo = window.PortfolioUi.busy({ buttons: [btnGeocode], region: resultsContent.parentNode });
        return function () {
            spinnerOverlay.classList.add("d-none");
            undo();
        };
    }

    // ── Score bar HTML ──────────────────────────────────────────────────────

    function scoreBarHtml(score) {
        if (score === null || score === undefined) {
            return '<span class="theme-text-muted">\u2014</span>';
        }
        var pct = Math.min(100, Math.max(0, Math.round(score)));
        var cls = pct >= 90 ? "score-high" : pct >= 75 ? "score-medium" : "score-low";
        return '<div class="score-bar-wrap">' +
               '<div class="score-bar"><div class="score-bar-fill ' + cls + '" style="width:' + pct + '%"></div></div>' +
               '<span class="score-label">' + pct + '</span>' +
               '</div>';
    }

    // ── Render results ──────────────────────────────────────────────────────

    function renderResults(data) {
        var total     = data.length;
        var matched   = data.filter(function (r) { return r.matched; }).length;
        var unmatched = total - matched;

        // KPIs
        kpiTotal.textContent    = fmt.number(total);
        kpiMatched.textContent  = fmt.number(matched);
        kpiUnmatched.textContent = fmt.number(unmatched);
        summaryText.textContent = fmt.number(matched) + " of " + fmt.number(total) + " addresses matched (" +
            fmt.percent(total > 0 ? (matched / total) * 100 : 0) + " match rate)";

        // Show results panel
        resultsPlaceholder.classList.add("d-none");
        resultsContent.classList.remove("d-none");

        // Destroy existing DataTable
        if (_dataTable !== null) {
            _dataTable.destroy();
            _dataTable = null;
            $("#resultsTable tbody").empty();
        }

        // Build rows
        var rows = data.map(function (r, idx) {
            var statusBadge = r.matched
                ? '<span class="badge status-badge status-matched">Matched</span>'
                : '<span class="badge status-badge status-unmatched">Unmatched</span>';
            var matchedAddr = r.matched && r.matchedAddress ? r.matchedAddress : "\u2014";
            var lat = r.matched && r.latitude  !== null && r.latitude  !== undefined ? r.latitude.toFixed(5)  : "\u2014";
            var lng = r.matched && r.longitude !== null && r.longitude !== undefined ? r.longitude.toFixed(5) : "\u2014";
            var scoreCell = r.matched ? scoreBarHtml(r.score) : '<span class="theme-text-muted">\u2014</span>';

            return [
                idx + 1,
                r.originalAddress,
                statusBadge,
                matchedAddr,
                scoreCell,
                lat,
                lng
            ];
        });

        // Initialize DataTable
        _dataTable = $("#resultsTable").DataTable({
            data: rows,
            columns: [
                { title: "#",                width: "40px" },
                { title: "Original Address", render: $.fn.dataTable.render.text() },
                { title: "Status",           width: "100px" },
                { title: "Matched Address", render: $.fn.dataTable.render.text() },
                { title: "Score",            width: "110px", orderable: false },
                { title: "Lat",              width: "90px"  },
                { title: "Lng",              width: "90px"  }
            ],
            pageLength: 25,
            order: [[0, "asc"]],
            language: {
                emptyTable: "No results to display."
            }
        });

        // Reset filter buttons
        setActiveFilter(document.querySelector('.filter-btn[data-filter="all"]'));
    }

    // ── Filter toggle ───────────────────────────────────────────────────────

    // Exactly one filter button is active; aria-pressed mirrors the visual state.
    function setActiveFilter(active) {
        filterBtns.forEach(function (b) {
            var on = b === active;
            b.classList.toggle("active", on);
            b.classList.toggle("btn-accent", on);
            b.classList.toggle("btn-outline-accent", !on);
            b.setAttribute("aria-pressed", on ? "true" : "false");
        });
    }

    filterBtns.forEach(function (btn) {
        btn.addEventListener("click", function () {
            if (!_dataTable) return;
            var filter = btn.getAttribute("data-filter");

            setActiveFilter(btn);

            // Column 2 (Status) search — DataTables regex search
            if (filter === "matched") {
                _dataTable.column(2).search("^Matched$", true, false).draw();
            } else if (filter === "unmatched") {
                _dataTable.column(2).search("^Unmatched$", true, false).draw();
            } else {
                _dataTable.column(2).search("").draw();
            }
        });
    });

    // ── Export CSV ──────────────────────────────────────────────────────────

    btnExport.addEventListener("click", function () {
        if (!_results || _results.length === 0) return;
        exportCSV(_results);
    });

    function exportCSV(data) {
        var header = ["OriginalAddress", "Matched", "MatchedAddress", "Score", "Latitude", "Longitude"];
        var lines  = [header.join(",")];

        data.forEach(function (r) {
            var row = [
                csvEscape(r.originalAddress),
                r.matched ? "true" : "false",
                csvEscape(r.matched && r.matchedAddress ? r.matchedAddress : ""),
                r.matched && r.score !== null && r.score !== undefined ? r.score : "",
                r.matched && r.latitude  !== null && r.latitude  !== undefined ? r.latitude  : "",
                r.matched && r.longitude !== null && r.longitude !== undefined ? r.longitude : ""
            ];
            lines.push(row.join(","));
        });

        var blob = new Blob([lines.join("\r\n")], { type: "text/csv;charset=utf-8;" });
        var url  = URL.createObjectURL(blob);
        var a    = document.createElement("a");
        a.href     = url;
        a.download = "geocoding-results.csv";
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }

    function csvEscape(value) {
        if (value === null || value === undefined) return "";
        var str = String(value);
        if (str.indexOf(",") !== -1 || str.indexOf('"') !== -1 || str.indexOf("\n") !== -1) {
            return '"' + str.replace(/"/g, '""') + '"';
        }
        return str;
    }

})();
