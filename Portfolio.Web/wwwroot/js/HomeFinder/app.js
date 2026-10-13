// app.js — Smart Home Finder orchestration
// Runs the search and wires the page on DOMContentLoaded. The rest of the page lives in sibling
// scripts that share the window.HomeFinder namespace: context.js (loads first), preferences.js,
// map.js, results.js, saved-searches.js. This script loads last.
(function (HF) {
    const HOME_FINDER_ROUTES = HF.routes;
    const state = HF.state;
    const announce = HF.announce;

    async function performSearch() {
        const total = HF.getWeightSum();
        if (total !== 100) {
            HF.updateWeightStatus();
            const diff = 100 - total;
            showToast(
                diff > 0
                    ? `Weights are ${diff}% under \u2014 adjust sliders to total 100%.`
                    : `Weights are ${Math.abs(diff)}% over \u2014 reduce sliders to total 100%.`,
                "warning"
            );
            return;
        }

        const prefs = HF.getPreferences();
        const btn = document.getElementById("btnSearch");
        const done = PortfolioUi.busy({ buttons: [btn], region: document.getElementById("resultsPanel") });
        btn.replaceChildren(PortfolioDom.el("span", { class: "spinner-border spinner-border-sm me-1", "aria-hidden": "true" }), "Searching\u2026");

        try {
            // apiFetch rather than raw fetch, so a 401 becomes a full-page redirect to /Login (see api-fetch.js).
            const res = await apiFetch(HOME_FINDER_ROUTES.search, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(prefs)
            });

            if (!res.ok) {
                // Surface server-side validation errors
                if (res.status === 400) {
                    const body = await res.json().catch(() => null);
                    const msg = body?.errors
                        ? Object.values(body.errors).flat().join(" ")
                        : "Invalid preferences. Check your inputs.";
                    showToast(msg, "danger");
                    return;
                }
                throw new Error(`HTTP ${res.status}`);
            }

            state.currentResults = await res.json();
            HF.plotResults(state.currentResults);
            HF.renderResults(state.currentResults);
            HF.persistPreferences();
            showToast(`Found ${state.currentResults.length} matching properties.`, "success");

            document.getElementById("btnSaveSearch").disabled = state.currentResults.length === 0;
        } catch (err) {
            console.error("Search failed:", err);
            document.getElementById("resultsPanel").innerHTML =
                '<p class="text-danger text-center small">Search failed. Please try again.</p>';
            showToast("Search failed. Please try again.", "danger");
            announce("Search failed. Please try again.");
        } finally {
            done();
            btn.replaceChildren(PortfolioDom.el("i", { class: "fa-solid fa-magnifying-glass me-1", "aria-hidden": "true" }), "Find Homes");
            HF.updateWeightStatus();
        }
    }

    // HomeFinder.performSearch is also called by saved-searches.js (applying a saved search re-runs it).
    HF.performSearch = performSearch;

    document.addEventListener("DOMContentLoaded", async () => {
        // Restore previous session preferences before wiring sliders
        HF.restorePreferences();

        HF.wireSliders();
        await HF.initMap();
        HF.wireBasemapSelect();

        document.getElementById("btnSearch")?.addEventListener("click", performSearch);
        document.getElementById("btnSaveSearch")?.addEventListener("click", HF.saveSearch);
        document.getElementById("btnLoadSearches")?.addEventListener("click", () => {
            const modal = new bootstrap.Modal(document.getElementById("savedSearchesModal"));
            modal.show();
            HF.loadSavedSearches();
        });
    });
}(window.HomeFinder));