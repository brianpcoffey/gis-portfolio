# CSS architecture

A small, layered set of stylesheets. Bootstrap 5.3 provides the responsive grid,
components, and native color modes (`data-bs-theme`); these files layer the
portfolio's palette, theming, and page-specific styles on top.

## Load order

Every page loads the **global** stylesheets from `_Layout.cshtml`, in this order:

1. `bootstrap.min.css`, Font Awesome (vendor)
2. `base.css` — palette tokens and base elements; every later file consumes its variables
3. `utilities.css` — intentionally holds no rules (kept only so the link in `_Layout.cshtml` keeps resolving; `.fade-in` and the hero responsive overrides live in `components.css`)
4. `site.css` — shared component styles (linked, not `@import`ed, so each file gets its own version hash)
5. `layout.css` — header / nav / footer / hero / project grids
6. `components.css` — shared components + dark-mode theming for Bootstrap and DataTables
7. `preferences.css` — user display and accessibility preferences (text scale, contrast, motion, underline, readable font, focus ring), including the global reduced-motion neutralization

Individual pages then link their **page-specific** stylesheet(s), and any vendor CSS such
as DataTables, in their `Styles` section, which renders after the global ones:

| Stylesheet | Pages |
|---|---|
| `gis.css` | Shared base for the `/Projects/<Name>` demo pages (all except Network) |
| `spatialcompute.css` | The native-kernel demos: CatRisk, Change, Clustering, Fleet, Geometry, GeoStream, Outage, Overlay, Response, Terrain, Viewshed |
| `change.css` | Change |
| `addressstandardization.css` | AddressStandardization |
| `batchgeocoding.css` | BatchGeocoding |
| `homefinder.css` | HomeFinder |
| `network.css` | Network (Route Planner) |
| `plantoperationsdashboard.css` | PlantOperationsDashboard |
| `reversegeocoding.css` | ReverseGeocoding |
| `stateexplorer.css` | StateExplorer |
| `profile.css` | Account/Profile |

## Single sources of truth

| Concern | Lives in |
|---|---|
| Palette variables (`:root`) + dark-mode overrides (`body.dark-mode`) | `base.css` |
| Token families in `base.css`: contrast-safe accent (`--accent-on-fill`, `--accent-text`…), `--focus-ring`, `--input-border`, status (`--status-success/danger/warning/info/caution-*`), chart series (`--chart-1…8`), and the theme-independent map tokens (`--map-series-1…8`, `--map-muted`) | `base.css` |
| Base elements (`html`, `body`, `a`, headings) + `.section-title` | `base.css` |
| Text-scale / contrast / motion / theme, driven by `<html>` `data-*` attrs | `preferences.css` |
| Shared buttons, forms, cards, tables, modals, badges | `components.css` / `site.css` |

## Conventions

- **Theming:** never hard-code palette colors; use `var(--text)`, `var(--surface)`,
  `var(--accent)`, etc. Dark mode is `body.dark-mode { --var: … }`; Bootstrap
  components additionally read `data-bs-theme` on `<html>`.
- **Marks over map tiles:** Leaflet and Esri basemap tiles stay light in both themes, so marks
  drawn over them use `--map-series-*` / `--map-muted`, which are declared once in `:root` and
  never redefined in `body.dark-mode`. Charts and SVG panels on the page surface use the
  theme-aware `--chart-*`. Read either from script with `PortfolioCharts.palette()` /
  `mapPalette()` / `token(name)`.
- **Page scoping:** page-specific rules are scoped under a page/root class so they only
  apply on that page (e.g. `.reverse-geocoding-page …`, `.batch-geocoding-page …`).
- **Motion:** any `transition`/`animation` is automatically neutralized when the user
  chooses "reduce motion" (or the OS requests it) — see the reduced-motion block in
  `preferences.css`. No extra work needed per animation.
- **Responsive:** prefer Bootstrap's grid/utilities and relative units; wide content
  (carousels, tables) must scroll inside its own overflow container, never the page body.
