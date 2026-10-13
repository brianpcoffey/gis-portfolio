// Renders the saved-states list, the collections list (and the save dialog's collection select)
// and the statistics counters. Render helpers are idempotent and quick; they read state, never write it.

import { getState } from './stateStore.js';
import { HEX_COLOR, el, icon, query } from './uiHelpers.js';

// Whether the visitor can use the collections API (false after a 401 for an anonymous visitor).
let collectionsSignedIn = true;

// Render helpers - idempotent and quick
export function renderSavedFeaturesList() {
    const container = query("savedFeaturesList");
    const countEl = query("savedCount");
    if (!container) return;
    container.replaceChildren();
    const state = getState();
    const entries = Array.from(state.savedFeatures.values());
    if (!entries.length) {
        container.appendChild(el("div", { class: "list-group-item theme-text-muted text-center small", text: "No saved states yet" }));
        if (countEl) countEl.textContent = "0";
        return;
    }

    const frag = document.createDocumentFragment();
    entries.forEach(saved => {
        // saved: may contain saved.Id (db id) and featureId (external)
        const featureKey = String(saved.featureId || saved.FeatureId);
        const savedDbId = String(saved.id || saved.Id || "");
        const item = el("div", { class: "list-group-item d-flex justify-content-between align-items-center" });
        // store both for click handling
        item.dataset.featureId = featureKey;
        if (savedDbId) item.dataset.savedId = savedDbId;

        const titleText = saved.displayName || saved.Name || saved.name || `State ${featureKey}`;
        const collectionName = saved.CollectionName || saved.collectionName;
        const left = el("button", { type: "button", class: "btn btn-link p-0 text-start flex-grow-1 se-saved-select", "data-action": "select", "data-feature-id": featureKey },
            titleText);
        if (collectionName) {
            left.append(" ", el("small", { class: "theme-text-muted ms-2", text: `(${collectionName})` }));
        }
        item.appendChild(left);

        const btnGroup = el("div", { class: "btn-group btn-group-sm" });
        const makeBtn = (cls, label, iconCls, action) => {
            const b = el("button", { type: "button", class: `btn ${cls}`, title: label, "aria-label": `${label}: ${titleText}`, "data-action": action },
                icon(iconCls));
            b.dataset.featureId = featureKey;
            // also include saved id on the button to make it available (remove needs it for the API)
            if (savedDbId) b.dataset.savedId = savedDbId;
            return b;
        };
        btnGroup.appendChild(makeBtn("btn-outline-accent", "View details", "fa-circle-info", "view"));
        btnGroup.appendChild(makeBtn("btn-outline-secondary", "Zoom", "fa-magnifying-glass-plus", "zoom"));
        btnGroup.appendChild(makeBtn("btn-outline-danger", "Remove", "fa-trash", "remove"));

        item.appendChild(btnGroup);
        frag.appendChild(item);
    });
    container.appendChild(frag);
    if (countEl) countEl.textContent = String(entries.length);
}

// Anonymous visitors cannot use the collections API: show a sign-in hint and disable "+ New".
export function setCollectionsSignedIn(signedIn) {
    collectionsSignedIn = !!signedIn;
    const btn = query("btnNewCollection");
    if (btn) {
        btn.setAttribute("aria-disabled", String(!collectionsSignedIn));
        btn.title = collectionsSignedIn ? "" : "Sign in to create collections";
    }
    renderCollections();
}

export function isCollectionsSignedIn() { return collectionsSignedIn; }

export function renderCollections() {
    const list = query("collectionsList");
    const select = query("modalCollection");
    if (list) list.replaceChildren();
    if (select) {
        select.innerHTML = '<option value="">-- None --</option>';
    }

    if (!collectionsSignedIn) {
        if (list) {
            const returnUrl = encodeURIComponent(window.location.pathname);
            list.appendChild(el("div", { class: "list-group-item theme-text-muted small", id: "collectionsSignInHint" },
                "Sign in to use collections. ",
                el("a", { href: `/Login?returnUrl=${returnUrl}`, text: "Sign in" })));
        }
        return;
    }

    const state = getState();
    if (list && !state.collections.length) {
        list.appendChild(el("div", { class: "list-group-item theme-text-muted text-center small", text: "No collections yet" }));
    }
    state.collections.forEach(col => {
        if (list) {
            const isActive = String(state.selectedCollectionId) === String(col.id);
            const item = el("div", {
                class: "list-group-item d-flex justify-content-between align-items-center gap-2" + (isActive ? " active" : ""),
                "data-collection-id": String(col.id)
            });

            // Colour badge: built as a node; the colour reaches CSS only after a hex check.
            const colorBadge = el("span", { class: "se-color-dot me-2", "aria-hidden": "true" });
            if (HEX_COLOR.test(col.color)) colorBadge.style.backgroundColor = col.color;

            // The name is its own button; edit and delete are siblings, never nested inside it.
            const nameBtn = el("button", {
                type: "button",
                class: "btn btn-link p-0 text-start flex-grow-1 se-collection-select",
                "data-action": "select",
                "aria-pressed": String(isActive)
            }, colorBadge, el("span", { text: col.name }));

            const group = el("div", { class: "btn-group btn-group-sm" },
                el("button", { type: "button", class: "btn btn-outline-secondary btn-sm", "data-action": "edit", title: "Edit", "aria-label": `Edit collection ${col.name}` }, icon("fa-pen")),
                el("button", { type: "button", class: "btn btn-outline-danger btn-sm ms-1", "data-action": "delete", title: "Delete", "aria-label": `Delete collection ${col.name}` }, icon("fa-trash")));

            item.append(nameBtn, group);
            list.appendChild(item);
        }
        if (select) {
            const opt = document.createElement("option");
            opt.value = String(col.id);
            opt.textContent = col.name;
            select.appendChild(opt);
        }
    });
}

export function renderStats() {
    const state = getState();
    const total = state.features.length || 0;
    const saved = state.savedFeatures.size || 0;
    const selected = state.selectedFeatureId ? 1 : 0;
    const comparing = state.compareSet.size || 0;
    const set = (id, val) => { const el = query(id); if (el) el.textContent = String(val); };
    set("statTotal", total);
    set("statSaved", saved);
    set("statSelected", selected);
    set("statComparing", comparing);
}
