// preferences.js - Smart Home Finder: sliders, the 100% weight budget and saved slider state.
// Slider configuration, preference collection, weight clamping and status, slider wiring, and
// localStorage persistence. Publishes its entry points on window.HomeFinder (see context.js).
(function (HF) {
    const announce = HF.announce;

    const weightSliderIds = [
        "sliderWAfford", "sliderWNeighbor", "sliderWSize", "sliderWAppreciation",
        "sliderWCondition", "sliderWCommute", "sliderWAmenities", "sliderWTax",
        "sliderWResale", "sliderWEnv"
    ];

    const weightBarIds = {
        sliderWAfford: "barWAfford",
        sliderWNeighbor: "barWNeighbor",
        sliderWSize: "barWSize",
        sliderWAppreciation: "barWAppreciation",
        sliderWCondition: "barWCondition",
        sliderWCommute: "barWCommute",
        sliderWAmenities: "barWAmenities",
        sliderWTax: "barWTax",
        sliderWResale: "barWResale",
        sliderWEnv: "barWEnv"
    };

    const sliders = {
        sliderMaxPrice: { label: "lblMaxPrice", key: "maxPrice", format: (v) => PortfolioFormat.money(v) },
        sliderMonthlyBudget: { label: "lblMonthlyBudget", key: "maxMonthlyBudget", format: (v) => PortfolioFormat.money(v) },
        sliderMinBed: { label: "lblMinBed", key: "minBedrooms" },
        sliderMinBath: { label: "lblMinBath", key: "minBathrooms" },
        sliderMinSqft: { label: "lblMinSqft", key: "minSqft", format: (v) => PortfolioFormat.number(v) },
        sliderMaxCommute: { label: "lblMaxCommute", key: "maxCommuteMin" },
        sliderWAfford: { label: "lblWAfford", key: "weightAffordability", divisor: 100 },
        sliderWNeighbor: { label: "lblWNeighbor", key: "weightNeighborhood", divisor: 100 },
        sliderWSize: { label: "lblWSize", key: "weightSize", divisor: 100 },
        sliderWAppreciation: { label: "lblWAppreciation", key: "weightAppreciation", divisor: 100 },
        sliderWCondition: { label: "lblWCondition", key: "weightCondition", divisor: 100 },
        sliderWCommute: { label: "lblWCommute", key: "weightCommute", divisor: 100 },
        sliderWAmenities: { label: "lblWAmenities", key: "weightAmenities", divisor: 100 },
        sliderWTax: { label: "lblWTax", key: "weightTaxUtilities", divisor: 100 },
        sliderWResale: { label: "lblWResale", key: "weightResale", divisor: 100 },
        sliderWEnv: { label: "lblWEnv", key: "weightEnvironment", divisor: 100 }
    };

    /** Shows the slider's value, formatted, in its visible label and as aria-valuetext. */
    function syncSliderLabel(sliderId) {
        const cfg = sliders[sliderId];
        const el = document.getElementById(sliderId);
        if (!cfg || !el) return;
        let text;
        if (cfg.format) text = cfg.format(el.value);
        else if (cfg.divisor) text = `${el.value}%`;
        else text = String(el.value);
        const lbl = document.getElementById(cfg.label);
        if (lbl) lbl.textContent = cfg.divisor ? el.value : text;
        el.setAttribute("aria-valuetext", text);
    }

    function getPreferences() {
        const prefs = {};
        for (const [sliderId, cfg] of Object.entries(sliders)) {
            const el = document.getElementById(sliderId);
            if (!el) continue;
            const val = parseFloat(el.value);
            prefs[cfg.key] = cfg.divisor ? val / cfg.divisor : val;
        }
        return prefs;
    }

    function getWeightSum() {
        let sum = 0;
        for (const id of weightSliderIds) {
            const el = document.getElementById(id);
            if (el) sum += parseInt(el.value, 10);
        }
        return sum;
    }

    /**
     * Dynamically adjusts every weight slider's `max` attribute so the user
     * can never push the total above 100%.  The active slider (the one
     * currently being dragged) is excluded from re-clamping so it feels
     * responsive; instead, every *other* slider has its max reduced.
     *
     * @param {string|null} activeSliderId  The slider currently being dragged.
     */
    function enforceWeightBudget(activeSliderId = null) {
        const total = getWeightSum();
        const step = 5; // matches the HTML step attribute

        for (const id of weightSliderIds) {
            const el = document.getElementById(id);
            if (!el) continue;

            const currentVal = parseInt(el.value, 10);
            // remaining = how much room exists if we took this slider to 0
            const othersSum = total - currentVal;
            const maxAllowed = Math.max(0, 100 - othersSum);

            // Round down to nearest step so the slider thumb snaps correctly
            const steppedMax = Math.floor(maxAllowed / step) * step;
            el.max = steppedMax;

            if (currentVal > steppedMax) {
                el.value = steppedMax;
                syncSliderLabel(id);
            }
        }
    }

    // Bar width is a percentage of 100, color-coded by severity.
    function updateWeightBar(sliderId) {
        const barId = weightBarIds[sliderId];
        if (!barId) return;

        const slider = document.getElementById(sliderId);
        const bar = document.getElementById(barId);
        if (!slider || !bar) return;

        const value = parseInt(slider.value, 10);
        const pct = Math.min(Math.max(value, 0), 100);

        bar.style.width = pct + "%";

        // ARIA: expose current value to screen readers
        bar.setAttribute("aria-valuenow", pct);

        if (pct >= 50) {
            bar.setAttribute("data-weight-level", "high");
        } else if (pct >= 30) {
            bar.setAttribute("data-weight-level", "medium");
        } else {
            bar.removeAttribute("data-weight-level");
        }
    }

    function updateAllWeightBars() {
        for (const sliderId of weightSliderIds) {
            updateWeightBar(sliderId);
        }
    }

    /**
     * Master status update — shows total, remaining, and gates the Search button.
     * Also updates the circular/arc remaining-budget indicator.
     */
    function updateWeightStatus() {
        const total = getWeightSum();
        const remaining = 100 - total;
        const statusEl = document.getElementById("weightStatus");
        const totalEl = document.getElementById("weightTotal");
        const remainingEl = document.getElementById("weightRemaining");
        const btnSearch = document.getElementById("btnSearch");
        const budgetRing = document.getElementById("budgetRing");

        if (totalEl) totalEl.textContent = total;
        if (remainingEl) remainingEl.textContent = remaining;

        if (budgetRing) {
            const circumference = 2 * Math.PI * 36; // r=36
            const offset = circumference - (total / 100) * circumference;
            budgetRing.style.strokeDashoffset = offset;

            budgetRing.classList.remove("ring-valid", "ring-over", "ring-under");
            if (total === 100) budgetRing.classList.add("ring-valid");
            else if (total > 100) budgetRing.classList.add("ring-over");
            else budgetRing.classList.add("ring-under");
        }

        if (!statusEl) return;

        const textEl = document.getElementById("weightStatusText");
        const state = total === 100 ? "valid" : (remaining > 0 ? "under" : "over");
        const hint = remaining > 0 ? `${remaining}% remaining` : `${Math.abs(remaining)}% over`;

        statusEl.className = `alert ${state === "valid" ? "alert-success" : "alert-warning"} small py-1 px-2 mb-2`;
        if (textEl) {
            const icon = PortfolioDom.el("i", { class: `fa-solid ${state === "valid" ? "fa-circle-check" : "fa-scale-unbalanced"} me-1`, "aria-hidden": "true" });
            const tail = PortfolioDom.el("span", { text: state === "valid" ? "\u2014 ready to search" : `(${hint} \u2014 must equal 100%)` });
            textEl.replaceChildren(icon, "Total: ", PortfolioDom.el("strong", { id: "weightTotal", text: `${total}%` }), " ", tail);
        }
        if (btnSearch) btnSearch.disabled = state !== "valid";

        // The visible total (#weightStatusText) is read as plain text; only a validity flip is announced,
        // never every slider step, and not the first evaluation on page load.
        if (lastWeightState !== null && state !== lastWeightState) {
            announce(state === "valid"
                ? "Weights total 100%, ready to search."
                : `Weights total ${total}%: ${hint}, must equal 100%.`);
        }
        lastWeightState = state;
    }

    let lastWeightState = null;

    function wireSliders() {
        for (const [sliderId, cfg] of Object.entries(sliders)) {
            const el = document.getElementById(sliderId);
            const lbl = document.getElementById(cfg.label);
            if (!el || !lbl) continue;

            el.addEventListener("input", () => {
                syncSliderLabel(sliderId);

                if (cfg.divisor) {
                    enforceWeightBudget(sliderId);
                    updateWeightBar(sliderId);
                    updateWeightStatus();
                }
            });

            // The visible <label for> names the slider; aria-valuetext carries the formatted value.
            syncSliderLabel(sliderId);
        }

        enforceWeightBudget();
        updateAllWeightBars();
        updateWeightStatus();
    }

    const PREFS_STORAGE_KEY = "homeFinderPrefs";

    function persistPreferences() {
        try {
            const prefs = getPreferences();
            localStorage.setItem(PREFS_STORAGE_KEY, JSON.stringify(prefs));
        } catch { /* quota exceeded / private browsing — silently ignore */ }
    }

    /** Restores slider values from localStorage if present. Returns true if restored. */
    function restorePreferences() {
        try {
            const raw = localStorage.getItem(PREFS_STORAGE_KEY);
            if (!raw) return false;

            const prefs = JSON.parse(raw);

            for (const [sliderId, cfg] of Object.entries(sliders)) {
                const el = document.getElementById(sliderId);
                if (!el || prefs[cfg.key] === undefined) continue;

                const raw = cfg.divisor
                    ? Math.round(prefs[cfg.key] * cfg.divisor)
                    : prefs[cfg.key];
                el.value = raw;
                syncSliderLabel(sliderId);
            }

            return true;
        } catch {
            return false;
        }
    }

    Object.assign(HF, {
        sliders,
        syncSliderLabel,
        getPreferences,
        getWeightSum,
        enforceWeightBudget,
        updateAllWeightBars,
        updateWeightStatus,
        wireSliders,
        persistPreferences,
        restorePreferences
    });
}(window.HomeFinder));
