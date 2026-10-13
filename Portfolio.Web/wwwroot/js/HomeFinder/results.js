// results.js - Smart Home Finder: the results panel.
// Renders the ranked property cards, announces the outcome, and lets a card (click or Enter/Space)
// fly the map to that property. Publishes its entry points on window.HomeFinder.
(function (HF) {
    const state = HF.state;
    const announce = HF.announce;

    function getScoreBarClass(score) {
        if (score >= 70) return "score-excellent";
        if (score >= 40) return "score-good";
        return "score-fair";
    }

    /** Formats price-per-square-foot as a compact dollar string. */
    function pricePerSqft(p) {
        if (!p.lotSqft) return "$—";
        return "$" + Math.round(p.price / p.lotSqft).toLocaleString();
    }

    function renderResults(results) {
        const panel = document.getElementById("resultsPanel");
        if (!panel) return;

        if (!results.length) {
            panel.innerHTML =
                '<p class="theme-text-muted text-center small">No matching properties found. Try adjusting your filters.</p>';
            announce("No matching properties found. Try adjusting your filters.");
            return;
        }

        const html = results.map(p => {
            const scorePct = Math.min(Math.max(p.compositeScore, 0), 100);
            const barClass = getScoreBarClass(p.compositeScore);

            return `
            <div class="card theme-card mb-2 p-2" style="cursor:pointer;"
                 data-property-id="${p.propertyId}" tabindex="0" role="button"
                 aria-label="View property #${p.rank} at ${PortfolioDom.escapeHtml(p.street)}">
                <div class="d-flex justify-content-between align-items-start">
                    <div class="flex-grow-1 me-2">
                        <strong class="small">#${p.rank}</strong>
                        <span class="small ms-1">${PortfolioDom.escapeHtml(p.street)}</span>
                        <div class="theme-text-muted" style="font-size:.75rem;">
                            $${p.price.toLocaleString()} \u2014 ${p.bedrooms}bd/${p.bathrooms}ba \u2014 ${p.lotSqft.toLocaleString()} sqft \u2014 ${pricePerSqft(p)}/sqft
                        </div>
                        <div class="theme-text-muted" style="font-size:.7rem;">
                            ${PortfolioDom.escapeHtml(p.propertyType)} \u00b7 Built ${p.yearBuilt} \u00b7 ${p.garageSpaces}-car garage${p.hasPool ? " \u00b7 Pool" : ""} \u00b7 School ${p.schoolRating}
                        </div>
                        <div class="score-bar-container" aria-label="Composite score: ${scorePct}%">
                            <div class="score-bar-fill ${barClass}" style="width: 0%;" data-target-width="${scorePct}"></div>
                        </div>
                    </div>
                    <span class="badge bg-success">${p.compositeScore}</span>
                </div>
            </div>`;
        }).join("");

        panel.innerHTML = html;
        announce(`${results.length} ${results.length === 1 ? "property" : "properties"} found`);

        requestAnimationFrame(() => {
            panel.querySelectorAll(".score-bar-fill[data-target-width]").forEach(bar => {
                bar.style.width = bar.dataset.targetWidth + "%";
            });
        });

        panel.querySelectorAll("[data-property-id]").forEach(card => {
            const handler = async () => {
                const id = parseInt(card.dataset.propertyId, 10);
                const match = state.currentResults.find(r => r.propertyId === id);
                if (match && state.mapView) {
                    const [Point] = await window.$arcgis.import(["@arcgis/core/geometry/Point.js"]);
                    const point = new Point({ longitude: match.longitude, latitude: match.latitude });
                    state.mapView.goTo({ target: point, zoom: 16 });
                    state.mapView.openPopup({ title: `#${match.rank} \u2014 ${PortfolioDom.escapeHtml(match.street)}`, location: point });
                }
            };

            card.addEventListener("click", handler);
            card.addEventListener("keydown", (e) => {
                if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    handler();
                }
            });
        });
    }

    Object.assign(HF, { pricePerSqft, renderResults });
}(window.HomeFinder));
