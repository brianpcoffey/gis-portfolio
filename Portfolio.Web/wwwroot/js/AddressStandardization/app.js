// AddressStandardization/app.js
// Self-contained IIFE for the Address Standardization & Validation page.
// Uses jQuery (global) and D3 (loaded per-page before this script).

(function ($) {
    "use strict";

    // ── Constants ──────────────────────────────────────────────────────────────
    var PARSE_API    = "/api/addressstandardization/parse";
    var VALIDATE_API = "/api/addressstandardization/validate";
    var SCORE_KEY    = "as_score_history";
    var MAX_SCORES   = 5;
    var DEBOUNCE_MS  = 600;

    // ── State ──────────────────────────────────────────────────────────────────
    var debounceTimer    = null;
    var lastParsedDto    = null;
    var confidenceSvgInit = false;

    // ── DOM refs ───────────────────────────────────────────────────────────────
    var $textarea         = $("#asRawAddress");
    var $charCount        = $("#asCharCount");
    var $btnParse         = $("#asBtnParse");
    var $btnValidate      = $("#asBtnValidate");
    var $analyzingIndicator = $("#asAnalyzingIndicator");
    var $alertBox         = $("#asAlert");
    var $alertMsg         = $("#asAlertMsg");

    var $parsePlaceholder = $("#asParsePlaceholder");
    var $parseResult      = $("#asParseResult");
    var $parseSpinner     = $("#asParseSpinner");
    var $standardized     = $("#asStandardizedAddress");
    var $componentsBody   = $("#asComponentsBody");
    var $confidenceLabel  = $("#asConfidenceLabel");
    var $copyBtn          = $("#asCopyBtn");

    var $validatePlaceholder = $("#asValidatePlaceholder");
    var $validateResult      = $("#asValidateResult");
    var $validateSpinner     = $("#asValidateSpinner");
    var $tierCard            = $("#asTierCard");
    var $tierIcon            = $("#asTierIcon");
    var $tierLabel           = $("#asTierLabel");
    var $tierScore           = $("#asTierScore");
    var $matchedAddress      = $("#asMatchedAddress");
    var $fallbackAlert       = $("#asFallbackAlert");
    var $unresolvedAlert     = $("#asUnresolvedAlert");

    // ── Helpers ────────────────────────────────────────────────────────────────
    function escapeHtml(str) {
        if (!str) return "";
        var d = document.createElement("div");
        d.textContent = str;
        return d.innerHTML;
    }

    function dash(val) {
        if (val === null || val === undefined || val === "") {
            return '<span class="text-muted">&mdash;</span>';
        }
        return escapeHtml(val);
    }

    function showAlert(msg) {
        $alertMsg.text(msg);
        $alertBox.removeClass("d-none show");
        requestAnimationFrame(function () { $alertBox.addClass("show"); });
    }

    function hideAlert() {
        $alertBox.removeClass("show").addClass("d-none");
    }

    function setButtonsBusy(busy) {
        $btnParse.prop("disabled", busy || $textarea.val().trim() === "");
        $btnValidate.prop("disabled", busy || $textarea.val().trim() === "");
    }

    function showAnalyzing(show) {
        if (show) {
            $analyzingIndicator.removeClass("d-none").addClass("as-visible");
        } else {
            $analyzingIndicator.removeClass("as-visible");
            setTimeout(function () {
                if (!$analyzingIndicator.hasClass("as-visible")) {
                    $analyzingIndicator.addClass("d-none");
                }
            }, 260);
        }
    }

    // ── Score history (sessionStorage) ─────────────────────────────────────────
    function loadScores() {
        try { return JSON.parse(sessionStorage.getItem(SCORE_KEY) || "[]"); } catch (_) { return []; }
    }

    function saveScores(arr) {
        try { sessionStorage.setItem(SCORE_KEY, JSON.stringify(arr)); } catch (_) {}
    }

    function pushScore(score) {
        var scores = loadScores();
        scores.push(score);
        scores = scores.slice(-MAX_SCORES);
        saveScores(scores);
        renderSparkline(scores);
    }

    // ── D3 Confidence Meter ────────────────────────────────────────────────────
    var confSvg, confTrack, confFill, confWidth;

    function initConfidenceMeter() {
        var container = document.getElementById("confidence-chart");
        if (!container) return;
        confWidth = container.clientWidth || 400;
        var height = 24;
        var r = 4;

        d3.select(container).selectAll("*").remove();

        confSvg = d3.select(container)
            .append("svg")
            .attr("width", "100%")
            .attr("height", height)
            .attr("aria-hidden", "true");

        // Track (background)
        confSvg.append("rect")
            .attr("x", 0).attr("y", 0)
            .attr("width", "100%").attr("height", height)
            .attr("rx", r).attr("ry", r)
            .attr("fill", "var(--border)");

        // Fill (starts at 0)
        confFill = confSvg.append("rect")
            .attr("x", 0).attr("y", 0)
            .attr("width", 0).attr("height", height)
            .attr("rx", r).attr("ry", r)
            .attr("fill", "var(--accent)");

        confidenceSvgInit = true;
    }

    function getConfidenceColor(pct) {
        if (pct >= 0.85) return "#38a169";   // green / --accent
        if (pct >= 0.60) return "#d69e2e";   // amber
        return "#e53e3e";                     // red
    }

    function updateConfidenceMeter(value) {
        // value: 0.0 – 1.0
        if (!confidenceSvgInit) initConfidenceMeter();
        var container = document.getElementById("confidence-chart");
        if (!container) return;
        var totalW = container.clientWidth || 400;
        var targetW = Math.max(0, Math.min(1, value)) * totalW;
        var color = getConfidenceColor(value);
        var pct = Math.round(value * 100);

        confFill.transition()
            .duration(300)
            .ease(d3.easeQuadOut)
            .attr("width", targetW)
            .attr("fill", color);

        $confidenceLabel.text(pct + "% confident").css("color", color);
    }

    // ── D3 Score Sparkline ─────────────────────────────────────────────────────
    function renderSparkline(scores) {
        var container = document.getElementById("as-sparkline");
        var tooltipEl = document.getElementById("as-sparkline-tooltip");
        if (!container) return;

        d3.select(container).selectAll("*").remove();

        if (!scores || scores.length < 2) {
            d3.select(container).append("div")
                .attr("class", "text-muted small text-center py-2")
                .text(scores && scores.length === 1 ? "Validate one more address to see trend." : "No scores yet.");
            return;
        }

        var W = container.clientWidth || 200;
        var H = 60;
        var pad = { top: 8, right: 8, bottom: 8, left: 8 };
        var iW = W - pad.left - pad.right;
        var iH = H - pad.top - pad.bottom;

        var svg = d3.select(container).append("svg")
            .attr("width", W).attr("height", H)
            .attr("aria-hidden", "true")
            .append("g")
            .attr("transform", "translate(" + pad.left + "," + pad.top + ")");

        var x = d3.scaleLinear().domain([0, scores.length - 1]).range([0, iW]);
        var extent = d3.extent(scores);
        var lo = Math.max(0,   extent[0] - 10);
        var hi = Math.min(100, extent[1] + 10);
        var y = d3.scaleLinear().domain([lo, hi]).range([iH, 0]);

        var line = d3.line()
            .x(function (_, i) { return x(i); })
            .y(function (d) { return y(d); })
            .curve(d3.curveMonotoneX);

        svg.append("path")
            .datum(scores)
            .attr("fill", "none")
            .attr("stroke", "var(--accent)")
            .attr("stroke-width", 2)
            .attr("d", line);

        svg.selectAll("circle")
            .data(scores)
            .enter()
            .append("circle")
            .attr("cx", function (_, i) { return x(i); })
            .attr("cy", function (d) { return y(d); })
            .attr("r", 4)
            .attr("fill", "var(--accent)")
            .attr("stroke", "var(--surface)")
            .attr("stroke-width", 1.5)
            .style("cursor", "pointer")
            .on("mouseover", function (event, d) {
                var score = typeof d === "number" ? d : d;
                if (tooltipEl) {
                    tooltipEl.textContent = "Score: " + (Math.round(score * 10) / 10);
                    tooltipEl.classList.add("visible");
                    var rect = container.getBoundingClientRect();
                    var cx = parseFloat(d3.select(this).attr("cx")) + pad.left;
                    var cy = parseFloat(d3.select(this).attr("cy")) + pad.top;
                    tooltipEl.style.left = (cx + 6) + "px";
                    tooltipEl.style.top  = (cy - 20) + "px";
                }
            })
            .on("mouseout", function () {
                if (tooltipEl) tooltipEl.classList.remove("visible");
            });
    }

    // ── Render parse result ────────────────────────────────────────────────────
    function renderParseResult(dto) {
        lastParsedDto = dto;

        $standardized.text(dto.standardizedAddress || "—");

        var rows = [
            ["House Number", dto.houseNumber],
            ["Street Name",  dto.streetName],
            ["Street Suffix",dto.streetSuffix],
            ["Unit",         dto.unit],
            ["City",         dto.city],
            ["State",        dto.state],
            ["Postal Code",  dto.postalCode]
        ];

        var html = rows.map(function (r) {
            return "<tr><td>" + escapeHtml(r[0]) + "</td><td>" + dash(r[1]) + "</td></tr>";
        }).join("");
        $componentsBody.html(html);

        var pct = typeof dto.parseConfidence === "number" ? dto.parseConfidence : 0;
        updateConfidenceMeter(pct);

        $parsePlaceholder.addClass("d-none");
        $parseResult.removeClass("d-none");
    }

    // ── Render validation result ───────────────────────────────────────────────
    // confidenceTier arrives as an integer from the API (System.Text.Json default enum serialization):
    //   0 = High, 1 = Medium, 2 = Low, 3 = Unresolved
    // Integer keys and string keys are both provided so the lookup works regardless of serialization format.
    var tierConfig = {
        High:       { cls: "tier-high",       icon: "fa-circle-check",        label: "HIGH CONFIDENCE" },
        Medium:     { cls: "tier-medium",      icon: "fa-circle-exclamation",  label: "MEDIUM CONFIDENCE" },
        Low:        { cls: "tier-low",         icon: "fa-triangle-exclamation",label: "LOW CONFIDENCE" },
        Unresolved: { cls: "tier-unresolved",  icon: "fa-circle-xmark",        label: "UNRESOLVED" },
        0:          { cls: "tier-high",       icon: "fa-circle-check",        label: "HIGH CONFIDENCE" },
        1:          { cls: "tier-medium",      icon: "fa-circle-exclamation",  label: "MEDIUM CONFIDENCE" },
        2:          { cls: "tier-low",         icon: "fa-triangle-exclamation",label: "LOW CONFIDENCE" },
        3:          { cls: "tier-unresolved",  icon: "fa-circle-xmark",        label: "UNRESOLVED" }
    };

    function renderValidateResult(dto) {
        var parsed  = dto.parsed;
        var tier    = dto.confidenceTier;
        var score   = dto.score;
        var matched = dto.matchedAddress || "";

        // Tier card
        var cfg = tierConfig[tier] || tierConfig.Unresolved;
        $tierCard
            .removeClass("tier-high tier-medium tier-low tier-unresolved")
            .addClass(cfg.cls);
        $tierIcon.html('<i class="fa-solid ' + cfg.icon + '"></i>');
        $tierLabel.text(cfg.label);
        $tierScore.text("Score: " + (Math.round(score * 10) / 10));

        $matchedAddress.text(matched);

        // Fallback detection: matched address doesn't contain the house number
        var houseNum = (parsed && parsed.houseNumber) ? parsed.houseNumber.trim() : "";
        var isFallback = houseNum !== "" && matched.indexOf(houseNum) === -1;
        $fallbackAlert.toggleClass("d-none", !isFallback);

        // Unresolved alert — tier may be the integer 3 or the string "Unresolved"
        $unresolvedAlert.toggleClass("d-none", tier !== "Unresolved" && tier !== 3);

        $validatePlaceholder.addClass("d-none");
        $validateResult.removeClass("d-none");

        // Also populate parse panel from dto.parsed
        if (parsed) {
            renderParseResult(parsed);
        }

        // Persist score and re-render sparkline
        pushScore(score);
    }

    // ── Reset validation panel ─────────────────────────────────────────────────
    function resetValidation() {
        $validateResult.addClass("d-none");
        $validatePlaceholder.removeClass("d-none");
        $fallbackAlert.addClass("d-none");
        $unresolvedAlert.addClass("d-none");
    }

    // ── API helpers ────────────────────────────────────────────────────────────
    function callParse(raw, callback) {
        return fetch(PARSE_API, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ RawAddress: raw })
        });
    }

    function callValidate(raw) {
        return fetch(VALIDATE_API, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ RawAddress: raw })
        });
    }

    function handleApiError(response) {
        return response.json().then(function (body) {
            showAlert(body.error || "An unexpected error occurred.");
        }).catch(function () {
            showAlert("An unexpected error occurred (status " + response.status + ").");
        });
    }

    // ── Parse action ───────────────────────────────────────────────────────────
    function doParse(raw, fromDebounce) {
        hideAlert();
        if (fromDebounce) {
            showAnalyzing(true);
        } else {
            $parseSpinner.removeClass("d-none");
            setButtonsBusy(true);
        }

        callParse(raw)
            .then(function (resp) {
                if (resp.ok) {
                    return resp.json().then(function (dto) {
                        renderParseResult(dto);
                    });
                } else {
                    $parsePlaceholder.removeClass("d-none");
                    $parseResult.addClass("d-none");
                    return handleApiError(resp);
                }
            })
            .catch(function (err) {
                if (window.showToast) window.showToast("Network error: " + err.message, "danger");
            })
            .finally(function () {
                showAnalyzing(false);
                $parseSpinner.addClass("d-none");
                setButtonsBusy(false);
            });
    }

    // ── Validate action ────────────────────────────────────────────────────────
    function doValidate(raw) {
        hideAlert();
        $parseSpinner.removeClass("d-none");
        $validateSpinner.removeClass("d-none");
        setButtonsBusy(true);

        callValidate(raw)
            .then(function (resp) {
                if (resp.ok) {
                    return resp.json().then(function (dto) {
                        renderValidateResult(dto);
                    });
                } else {
                    return handleApiError(resp);
                }
            })
            .catch(function (err) {
                if (window.showToast) window.showToast("Network error: " + err.message, "danger");
            })
            .finally(function () {
                $parseSpinner.addClass("d-none");
                $validateSpinner.addClass("d-none");
                setButtonsBusy(false);
            });
    }

    // ── Textarea wiring ────────────────────────────────────────────────────────
    $textarea.on("input", function () {
        var val = $textarea.val();
        var len = val.length;
        $charCount.text(len + (len === 1 ? " character" : " characters"));

        var hasText = val.trim().length > 0;
        $btnParse.prop("disabled", !hasText);
        $btnValidate.prop("disabled", !hasText);

        // Reset validation when input changes
        resetValidation();

        // Debounced auto-parse
        clearTimeout(debounceTimer);
        if (hasText) {
            debounceTimer = setTimeout(function () {
                doParse(val, true);
            }, DEBOUNCE_MS);
        } else {
            $parsePlaceholder.removeClass("d-none");
            $parseResult.addClass("d-none");
            showAnalyzing(false);
        }
    });

    $btnParse.on("click", function () {
        clearTimeout(debounceTimer);
        var val = $textarea.val().trim();
        if (val) doParse(val, false);
    });

    $btnValidate.on("click", function () {
        clearTimeout(debounceTimer);
        var val = $textarea.val().trim();
        if (val) doValidate(val);
    });

    // ── Example addresses ──────────────────────────────────────────────────────
    $(document).on("click", ".as-example-item", function () {
        var addr = $(this).data("address");
        $textarea.val(addr).trigger("input");

        // Close the Bootstrap collapse
        var collapseEl = document.getElementById("asExamples");
        if (collapseEl && window.bootstrap && bootstrap.Collapse) {
            var bsCollapse = bootstrap.Collapse.getInstance(collapseEl);
            if (bsCollapse) {
                bsCollapse.hide();
            } else {
                new bootstrap.Collapse(collapseEl, { toggle: false }).hide();
            }
        }
    });

    // ── Clipboard copy ─────────────────────────────────────────────────────────
    $copyBtn.on("click", function () {
        var text = $standardized.text();
        if (!text || text === "—") return;

        navigator.clipboard.writeText(text).then(function () {
            var $icon = $copyBtn.find("i");
            $icon.removeClass("fa-regular fa-copy").addClass("fa-solid fa-check");
            $copyBtn.addClass("text-accent").attr("title", "Copied!");

            // Bootstrap Tooltip feedback
            if (window.bootstrap && bootstrap.Tooltip) {
                var existingTip = bootstrap.Tooltip.getInstance($copyBtn[0]);
                if (existingTip) existingTip.dispose();
                var tip = new bootstrap.Tooltip($copyBtn[0], {
                    title: "Copied!",
                    trigger: "manual",
                    placement: "top"
                });
                tip.show();
                setTimeout(function () {
                    tip.hide();
                    tip.dispose();
                    $icon.removeClass("fa-solid fa-check").addClass("fa-regular fa-copy");
                    $copyBtn.removeClass("text-accent").attr("title", "Copy to clipboard");
                }, 2000);
            } else {
                setTimeout(function () {
                    $icon.removeClass("fa-solid fa-check").addClass("fa-regular fa-copy");
                    $copyBtn.removeClass("text-accent").attr("title", "Copy to clipboard");
                }, 2000);
            }
        }).catch(function () {
            if (window.showToast) window.showToast("Could not copy to clipboard.", "warning");
        });
    });

    // ── Initialization ─────────────────────────────────────────────────────────
    (function init() {
        // Initialize confidence meter at 0
        initConfidenceMeter();
        updateConfidenceMeter(0);

        // Render existing score history (if any from this session)
        renderSparkline(loadScores());
    }());

}(window.jQuery));
