# CSS architecture

A small, layered set of stylesheets. Bootstrap 5.3 provides the responsive grid,
components, and native color modes (`data-bs-theme`); these files layer the
portfolio's palette, theming, and page-specific styles on top.

## Load order

Every page loads the **global** stylesheets from `_Layout.cshtml`, in this order:

1. `bootstrap.min.css`, Font Awesome, DataTables (vendor)
2. `site.css` — entry point; `@import`s `base.css` then `utilities.css`, then adds shared component styles
3. `layout.css` — header / nav / footer / hero / project grids
4. `components.css` — shared components + dark-mode theming for Bootstrap/DataTables
5. `preferences.css` — accessibility + theme preferences, and site-wide animations

Individual project pages then link their **page-specific** stylesheet(s) in the page
markup (after the global ones), e.g. `homefinder.css`, `network.css`,
`plantoperationsdashboard.css`. `gis.css` is the shared base for all `/Projects/*` pages.

## Single sources of truth

| Concern | Lives in |
|---|---|
| Palette variables (`:root`) + dark-mode overrides (`body.dark-mode`) | `base.css` |
| Base elements (`html`, `body`, `a`, headings) + `.section-title` | `base.css` |
| Text-scale / contrast / motion / theme, driven by `<html>` `data-*` attrs | `preferences.css` |
| Shared buttons, forms, cards, tables, modals, badges | `components.css` / `site.css` |

## Conventions

- **Theming:** never hard-code palette colors; use `var(--text)`, `var(--surface)`,
  `var(--accent)`, etc. Dark mode is `body.dark-mode { --var: … }`; Bootstrap
  components additionally read `data-bs-theme` on `<html>`.
- **Page scoping:** page-specific rules are scoped under a page/root class so they only
  apply on that page (e.g. `.reverse-geocoding-page …`, `.batch-geocoding-page …`).
- **Motion:** any `transition`/`animation` is automatically neutralized when the user
  chooses "reduce motion" (or the OS requests it) — see the reduced-motion block in
  `preferences.css`. No extra work needed per animation.
- **Responsive:** prefer Bootstrap's grid/utilities and relative units; wide content
  (carousels, tables) must scroll inside its own overflow container, never the page body.
