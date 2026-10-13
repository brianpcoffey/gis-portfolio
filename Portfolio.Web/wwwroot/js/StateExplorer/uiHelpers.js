// Small DOM helpers and constants shared by the State Explorer UI modules
// (lists.js, modals.js, comparison.js, uiManager.js).

// Default swatch for a new collection (also what the collection dialog opens with).
export const DEFAULT_COLLECTION_COLOR = "#6c757d";
export const HEX_COLOR = /^#[0-9a-fA-F]{6}$/;

export const el = (tag, attrs, ...kids) => window.PortfolioDom.el(tag, attrs, ...kids);
export const icon = (cls) => el("i", { class: `fa-solid ${cls}`, "aria-hidden": "true" });

export function query(id) { return document.getElementById(id); }
