// Comparison panel: the side-by-side cards for the features in the compare set, and the
// Compare Mode button's on/off look. Reads state only; the cards' buttons are wired by delegation in uiManager.js.

import { getState } from './stateStore.js';
import { extractMetadataPairs } from './metadata.js';
import { el, query } from './uiHelpers.js';

export function renderComparison() {
    const panel = query("comparisonPanel");
    const content = query("comparisonContent");
    if (!panel || !content) return;

    const state = getState();
    const ids = Array.from(state.compareSet || []);
    if (!state.compareMode || ids.length === 0) {
        panel.classList.add("d-none");
        content.innerHTML = "";
        renderCompareIndicator();
        return;
    }

    const frag = document.createDocumentFragment();
    ids.forEach(id => {
        // Try to locate feature in features list first, then savedFeatures
        let feature = state.features.find(f => String(f.featureId || f.FeatureId) === String(id));
        if (!feature) feature = state.savedFeatures.get(String(id));
        const title = feature?.displayName || feature?.Name || feature?.name || `State ${id}`;
        const desc = (feature?.Description || feature?.description || "").toString();

        const metaPairs = extractMetadataPairs(feature);
        const metaHtml = metaPairs.length ? `<div class="mt-2"><small class="theme-text-muted">${metaPairs.map(p => `<div>${PortfolioDom.escapeHtml(p[0])}: <strong>${PortfolioDom.escapeHtml(p[1])}</strong></div>`).join("")}</small></div>` : "";

        const card = document.createElement("div");
        card.className = "card theme-card mb-2 p-2";
        // The template keeps its original indentation: its whitespace is part of the string.
        card.innerHTML = `
                <div class="d-flex justify-content-between align-items-start">
                    <div style="min-width:0;">
                        <strong>${PortfolioDom.escapeHtml(title)}</strong>
                        <div class="small theme-text-muted">ID: ${PortfolioDom.escapeHtml(id)}</div>
                        <div class="small text-truncate se-compare-desc">${PortfolioDom.escapeHtml(desc)}</div>
                        ${metaHtml}
                    </div>
                    <div class="btn-group-vertical btn-group-sm ms-2">
                        <button type="button" class="btn btn-outline-secondary" data-action="zoom" data-feature-id="${PortfolioDom.escapeHtml(id)}" title="Zoom to feature" aria-label="Zoom to ${PortfolioDom.escapeHtml(title)}"><i class="fa-solid fa-magnifying-glass-plus" aria-hidden="true"></i></button>
                        <button type="button" class="btn btn-outline-danger" data-action="toggle" data-feature-id="${PortfolioDom.escapeHtml(id)}" title="Remove from comparison" aria-label="Remove ${PortfolioDom.escapeHtml(title)} from comparison"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button>
                    </div>
                </div>
            `;
        frag.appendChild(card);
    });

    content.innerHTML = "";
    content.appendChild(frag);
    panel.classList.remove("d-none");
    renderCompareIndicator();
}

export function renderCompareIndicator() {
    const btn = query("btnCompareMode");
    if (!btn) return;
    const state = getState();
    btn.classList.toggle("btn-accent", !!state.compareMode);
    btn.classList.toggle("btn-outline-accent", !state.compareMode);
    btn.classList.toggle("active", !!state.compareMode);
    btn.setAttribute("aria-pressed", String(!!state.compareMode));
    btn.replaceChildren(
        el("i", { class: "fa-solid fa-chart-column me-1", "aria-hidden": "true" }),
        state.compareMode ? `Compare Mode (${state.compareSet.size || 0})` : "Compare Mode");
}
