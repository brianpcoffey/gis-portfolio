// saved-searches.js - Smart Home Finder: saved searches.
// Saves the current preferences and results to the visitor's profile, lists and deletes saved
// searches in the modal, and re-applies one (which re-runs the search). Publishes its entry points
// on window.HomeFinder; app.js wires the buttons.
(function (HF) {
    const state = HF.state;
    const HOME_FINDER_ROUTES = HF.routes;

    // Marks the name field invalid and links it to its error text, or clears both. aria-describedby is
    // only present while invalid, so the hidden message is not read otherwise.
    function setNameInvalid(input, invalid) {
        input.classList.toggle("is-invalid", invalid);
        if (invalid) {
            input.setAttribute("aria-invalid", "true");
            input.setAttribute("aria-describedby", "saveSearchNameError");
        } else {
            input.removeAttribute("aria-invalid");
            input.removeAttribute("aria-describedby");
        }
    }

    // Replaces window.prompt: a Bootstrap modal (#saveSearchModal in Index.cshtml) with one name field.
    // Opens with the name selected, Enter submits, an empty name is refused in place, and focus goes
    // back to the Save Search button when it closes. Resolves to the trimmed name on Save, or null when
    // the dialog is dismissed.
    function askSearchName(defaultName) {
        const modalEl = document.getElementById("saveSearchModal");
        const form = document.getElementById("saveSearchForm");
        const input = document.getElementById("saveSearchName");
        if (!modalEl || !form || !input) return Promise.resolve(null);

        input.value = defaultName || "";
        setNameInvalid(input, false);
        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);

        return new Promise((resolve) => {
            let result = null;
            const onSubmit = (e) => {
                e.preventDefault();
                const trimmed = input.value.trim();
                if (!trimmed) {
                    setNameInvalid(input, true);
                    input.focus();
                    return;
                }
                result = trimmed;
                modal.hide();
            };
            const onInput = () => setNameInvalid(input, false);
            const onShown = () => {
                input.focus();
                input.select();
            };
            const onHidden = () => {
                form.removeEventListener("submit", onSubmit);
                input.removeEventListener("input", onInput);
                modalEl.removeEventListener("shown.bs.modal", onShown);
                setNameInvalid(input, false);
                resolve(result);
                // Bootstrap only restores focus to data-api triggers, and this one is opened from script.
                setTimeout(() => document.getElementById("btnSaveSearch")?.focus(), 0);
            };
            form.addEventListener("submit", onSubmit);
            input.addEventListener("input", onInput);
            modalEl.addEventListener("shown.bs.modal", onShown, { once: true });
            modalEl.addEventListener("hidden.bs.modal", onHidden, { once: true });
            modal.show();
        });
    }

    async function saveSearch() {
        const prefs = HF.getPreferences();
        const name = await askSearchName(`Search ${new Date().toLocaleDateString()}`);
        if (!name) return;

        try {
            const res = await apiFetch(HOME_FINDER_ROUTES.searches, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    name: name.trim(),
                    preferences: prefs,
                    propertyIds: state.currentResults.map(r => r.propertyId)
                })
            });
            if (res.ok) {
                showToast("Search saved to your profile!", "success");
            } else {
                showToast("Failed to save search.", "danger");
            }
        } catch (err) {
            console.error("Save search failed:", err);
            showToast("Failed to save search.", "danger");
        }
    }

    async function loadSavedSearches() {
        const body = document.getElementById("savedSearchesBody");
        if (!body) return;

        body.innerHTML = '<p class="theme-text-muted text-center small">Loading\u2026</p>';

        try {
            const res = await apiFetch(HOME_FINDER_ROUTES.searches);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const searches = await res.json();

            if (!searches.length) {
                body.innerHTML = '<p class="theme-text-muted text-center small">No saved searches found.</p>';
                return;
            }

            body.innerHTML = searches.map(s => `
            <div class="card theme-card mb-2 p-2">
                <div class="d-flex justify-content-between align-items-center">
                    <div>
                        <strong class="small">${PortfolioDom.escapeHtml(s.name)}</strong>
                        <div class="theme-text-muted" style="font-size:.75rem;">
                            Saved ${new Date(s.createdAt).toLocaleDateString()}
                        </div>
                    </div>
                    <div class="d-flex gap-1">
                        <button class="btn btn-sm btn-outline-accent btn-load-search" data-search-id="${s.id}">
                            <i class="fa-solid fa-upload me-1" aria-hidden="true"></i>Load
                        </button>
                        <button class="btn btn-sm btn-outline-danger btn-delete-search" data-search-id="${s.id}" aria-label="Delete saved search ${PortfolioDom.escapeHtml(s.name)}">
                            <i class="fa-solid fa-trash" aria-hidden="true"></i>
                        </button>
                    </div>
                </div>
            </div>
        `).join("");

            body.querySelectorAll(".btn-load-search").forEach(btn => {
                btn.addEventListener("click", async () => {
                    const id = btn.dataset.searchId;
                    await applySavedSearch(id);
                    const modal = bootstrap.Modal.getInstance(document.getElementById("savedSearchesModal"));
                    modal?.hide();
                });
            });

            body.querySelectorAll(".btn-delete-search").forEach(btn => {
                btn.addEventListener("click", async () => {
                    const id = btn.dataset.searchId;
                    if (!await confirmDialog("Delete this saved search?")) return;
                    try {
                        const res = await apiFetch(`${HOME_FINDER_ROUTES.searches}/${id}`, { method: "DELETE" });
                        if (res.ok) {
                            btn.closest(".card").remove();
                            showToast("Search deleted.", "success");
                        }
                    } catch (err) {
                        console.error("Delete failed:", err);
                        showToast("Delete failed.", "danger");
                    }
                });
            });
        } catch (err) {
            console.error("Load searches failed:", err);
            body.innerHTML = '<p class="text-danger text-center small">Failed to load saved searches.</p>';
        }
    }

    async function applySavedSearch(searchId) {
        try {
            const res = await apiFetch(`${HOME_FINDER_ROUTES.searches}/${searchId}`);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const saved = await res.json();

            const prefs = saved.preferences || JSON.parse(saved.preferencesJson || "{}");

            for (const [sliderId, cfg] of Object.entries(HF.sliders)) {
                const el = document.getElementById(sliderId);
                if (!el || prefs[cfg.key] === undefined) continue;

                const val = cfg.divisor ? Math.round(prefs[cfg.key] * cfg.divisor) : prefs[cfg.key];
                el.value = val;
                HF.syncSliderLabel(sliderId);
            }

            HF.enforceWeightBudget();
            HF.updateAllWeightBars();
            HF.updateWeightStatus();
            await HF.performSearch();
            showToast(`Loaded "${saved.name}"`, "success");
        } catch (err) {
            console.error("Apply saved search failed:", err);
            showToast("Failed to load search.", "danger");
        }
    }

    Object.assign(HF, { saveSearch, loadSavedSearches, applySavedSearch });
}(window.HomeFinder));
