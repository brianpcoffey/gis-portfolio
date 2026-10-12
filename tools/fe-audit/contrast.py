"""WCAG 2.x contrast ratios for the site's token pairs (values read from the CSS by hand).
Usage: py -I tools/fe-audit/contrast.py   (no arguments; edit the pair tables when the tokens change)."""
import re

def hex2rgb(h):
    h = h.lstrip('#')
    if len(h) == 3:
        h = ''.join(c * 2 for c in h)
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))

def parse(c):
    """'#rrggbb' or 'rgba(r,g,b,a)' -> (r,g,b,a)."""
    c = c.strip()
    if c.startswith('#'):
        return hex2rgb(c) + (1.0,)
    m = re.match(r'rgba?\(([^)]+)\)', c)
    parts = [p.strip() for p in m.group(1).split(',')]
    r, g, b = (float(x) for x in parts[:3])
    a = float(parts[3]) if len(parts) > 3 else 1.0
    return (r, g, b, a)

def over(fg, bg):
    """Alpha-composite fg over opaque bg."""
    r, g, b, a = fg
    R, G, B = bg[:3]
    return (r * a + R * (1 - a), g * a + G * (1 - a), b * a + B * (1 - a), 1.0)

def lum(c):
    def ch(v):
        v /= 255.0
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    r, g, b = c[:3]
    return 0.2126 * ch(r) + 0.7152 * ch(g) + 0.0722 * ch(b)

def ratio(fg, bg, under=None):
    bgc = parse(bg)
    if bgc[3] < 1:
        bgc = over(bgc, parse(under))
    fgc = parse(fg)
    if fgc[3] < 1:
        fgc = over(fgc, bgc)
    l1, l2 = lum(fgc), lum(bgc)
    hi, lo = max(l1, l2), min(l1, l2)
    return (hi + 0.05) / (lo + 0.05)

L = dict(background='#f7fafc', surface='#ffffff', text='#1a202c', muted='#5a6a7d', border='#e2e8f0',
         accent='#38a169', accent_light='#48bb78', on_fill='#2c7a53', on_fill_h='#215c3f',
         accent_text='#2c7a53', accent_soft='#e6f4ec', primary='#1a365d', primary_light='#2c5282')
D = dict(L, background='#1a202c', surface='#2d3748', text='#f7fafc', muted='#a0aec0', border='#4a5568',
         accent_text='#68d391', accent_soft='rgba(104, 211, 145, 0.14)')

rows = []
def check(theme, label, fg, bg, need, under=None, src=''):
    r = ratio(fg, bg, under)
    rows.append((theme, label, fg, bg if under is None else f"{bg} over {under}", r, need, 'PASS' if r >= need else 'FAIL', src))

for theme, T in (('light', L), ('dark', D)):
    for bgname in ('background', 'surface'):
        bg = T[bgname]
        check(theme, f'--text on --{bgname}', T['text'], bg, 4.5, src='base.css:11/37')
        check(theme, f'--text-muted on --{bgname}', T['muted'], bg, 4.5, src='base.css:13/40')
        check(theme, f'link --accent-text on --{bgname}', T['accent_text'], bg, 4.5, src='base.css:94')
        hover = T['on_fill_h'] if theme == 'light' else T['accent_light']
        check(theme, f'link:hover on --{bgname}', hover, bg, 4.5, src='base.css:100/105')
        check(theme, f'--accent used as TEXT on --{bgname} (normal)', T['accent'], bg, 4.5,
              src='spatialcompute.css:176, reversegeocoding.css:214, addressstandardization.css:248')
        check(theme, f'--accent used as TEXT on --{bgname} (large/bold>=18.66px)', T['accent'], bg, 3.0,
              src='spatialcompute.css:134 .kpi-value, network.css:106 .kpi-val')
        check(theme, f'focus ring --accent vs --{bgname} (non-text)', T['accent'], bg, 3.0, src='preferences.css:68')
        check(theme, f'input border --border vs --{bgname} (non-text)', T['border'], bg, 3.0, src='components.css:70')
        check(theme, f'outline btn text --accent-text on --{bgname}', T['accent_text'], bg, 4.5, src='site.css:38')
        check(theme, f'muted on --accent-soft over --{bgname}', T['accent_text'], T['accent_soft'], 4.5, under=bg, src='preferences.css:256 .pref-badge')
    check(theme, 'white on --accent-on-fill (btn-accent, badge-gis)', '#ffffff', T['on_fill'], 4.5, src='site.css:21')
    check(theme, 'white on --accent-on-fill-h (btn hover)', '#ffffff', T['on_fill_h'], 4.5, src='site.css:30')
    check(theme, 'white on --accent (raw)', '#ffffff', T['accent'], 4.5, src='reversegeocoding.css:134')
    check(theme, 'focus ring --accent vs --accent-on-fill btn (adjacent)', T['accent'], T['on_fill'], 3.0, src='preferences.css:68 vs site.css:21')

# Fixed (non-token) pairs found in the CSS
fixed = [
    ('light', '.destination-chip #198754 on tint', '#198754', 'rgba(25,135,84,0.12)', 4.5, '#ffffff', 'network.css:79-80'),
    ('dark', '.destination-chip #198754 on tint', '#198754', 'rgba(25,135,84,0.12)', 4.5, '#2d3748', 'network.css:79-80'),
    ('light', 'batch .kpi-success #38a169 (1.6rem bold)', '#38a169', '#ffffff', 3.0, None, 'batchgeocoding.css:109'),
    ('light', 'batch .kpi-danger #e53e3e (1.6rem bold)', '#e53e3e', '#ffffff', 3.0, None, 'batchgeocoding.css:113'),
    ('dark', 'batch .kpi-success #68d391', '#68d391', '#2d3748', 3.0, None, 'batchgeocoding.css:117'),
    ('dark', 'batch .kpi-danger #fc8181', '#fc8181', '#2d3748', 3.0, None, 'batchgeocoding.css:121'),
    ('light', 'rg badge white on #38a169', '#ffffff', '#38a169', 4.5, None, 'reversegeocoding.css:134-135'),
    ('light', 'rg badge white on #3182ce', '#ffffff', '#3182ce', 4.5, None, 'reversegeocoding.css:139-140'),
    ('light', 'rg badge white on #b7791f', '#ffffff', '#b7791f', 4.5, None, 'reversegeocoding.css:144-145'),
    ('light', 'rg badge white on #4a5568', '#ffffff', '#4a5568', 4.5, None, 'reversegeocoding.css:317-318'),
    ('light', 'as tier high #276749 on tint', '#276749', 'rgba(56,161,105,0.12)', 4.5, '#ffffff', 'addressstandardization.css:173-175'),
    ('light', 'as tier med #975a16 on tint', '#975a16', 'rgba(214,158,46,0.12)', 4.5, '#ffffff', 'addressstandardization.css:179-181'),
    ('light', 'as tier low #9c4221 on tint', '#9c4221', 'rgba(237,137,54,0.12)', 4.5, '#ffffff', 'addressstandardization.css:185-187'),
    ('light', 'as tier none #9b2c2c on tint', '#9b2c2c', 'rgba(229,62,62,0.12)', 4.5, '#ffffff', 'addressstandardization.css:191-193'),
    ('dark', 'as tier high #9ae6b4 on tint', '#9ae6b4', 'rgba(56,161,105,0.2)', 4.5, '#2d3748', 'addressstandardization.css:307-309'),
    ('dark', 'as tier med #fbd38d on tint', '#fbd38d', 'rgba(214,158,46,0.2)', 4.5, '#2d3748', 'addressstandardization.css:313-315'),
    ('dark', 'as tier none #feb2b2 on tint', '#feb2b2', 'rgba(229,62,62,0.2)', 4.5, '#2d3748', 'addressstandardization.css:325-327'),
    ('dark', 'alert-danger #feb2b2 on tint', '#feb2b2', 'rgba(229,62,62,0.15)', 4.5, '#1a202c', 'components.css:231-235'),
    ('dark', 'alert-info #90cdf4 on tint', '#90cdf4', 'rgba(49,130,206,0.15)', 4.5, '#1a202c', 'components.css:213-217'),
    ('dark', 'alert-warning #fbd38d on tint', '#fbd38d', 'rgba(214,158,46,0.15)', 4.5, '#1a202c', 'components.css:225-229'),
    ('light', 'footer text rgba(255,255,255,.8) on --primary', 'rgba(255,255,255,0.8)', '#1a365d', 4.5, None, 'layout.css:526'),
    ('light', 'footer icons rgba(255,255,255,.7) on --primary (non-text)', 'rgba(255,255,255,0.7)', '#1a365d', 3.0, None, 'layout.css:543'),
    ('light', 'footer link hover --accent-light on --primary', '#48bb78', '#1a365d', 3.0, None, 'layout.css:551'),
    ('light', 'hero-desc rgba(255,255,255,.85) on #2c5282 (gradient mid)', 'rgba(255,255,255,0.85)', '#2c5282', 4.5, None, 'layout.css:163'),
    ('light', 'hero-desc rgba(255,255,255,.85) on #38a169 (gradient end)', 'rgba(255,255,255,0.85)', '#38a169', 4.5, None, 'layout.css:163 + base.css:17'),
    ('light', 'hero-desc rgba(255,255,255,.85) on est. #327d75 (text bottom-right @1280)', 'rgba(255,255,255,0.85)', '#327d75', 4.5, None, 'layout.css:163 (estimate)'),
    ('light', 'hero .btn-outline-accent white on #38a169 (gradient end)', '#ffffff', '#38a169', 4.5, None, 'site.css:51-54'),
    ('light', 'login-back-link rgba(255,255,255,.65) on #2c5282', 'rgba(255,255,255,0.65)', '#2c5282', 4.5, None, 'site.css:273'),
    ('light', 'login-back-link rgba(255,255,255,.65) on #38a169', 'rgba(255,255,255,0.65)', '#38a169', 4.5, None, 'site.css:273'),
    ('light', 'project-image icon rgba(255,255,255,.8) on --primary (non-text)', 'rgba(255,255,255,0.8)', '#1a365d', 3.0, None, 'layout.css:274'),
    ('light', 'toast close white X on text-bg-warning #ffc107 (non-text)', '#ffffff', '#ffc107', 3.0, None, 'toasts.js:43'),
    ('light', 'toast close white X on text-bg-info #0dcaf0 (non-text)', '#ffffff', '#0dcaf0', 3.0, None, 'toasts.js:43'),
    ('light', 'toast text white on text-bg-success #198754', '#ffffff', '#198754', 4.5, None, 'toasts.js:24'),
    ('light', 'toast text white on text-bg-danger #dc3545', '#ffffff', '#dc3545', 4.5, None, 'toasts.js:24'),
    ('light', 'hamburger focus ring rgba(56,161,105,.35) vs surface (non-text)', 'rgba(56,161,105,0.35)', '#ffffff', 3.0, None, 'layout.css:79'),
    ('light', 'gis input focus ring rgba(56,161,105,.12) vs surface (non-text)', 'rgba(56,161,105,0.12)', '#ffffff', 3.0, None, 'gis.css:285'),
    ('dark', 'feature-card focus outline rgba(56,161,105,.12) vs surface (non-text)', 'rgba(56,161,105,0.12)', '#2d3748', 3.0, None, 'gis.css:419'),
    ('light', 'skip-link white on --accent-on-fill', '#ffffff', '#2c7a53', 4.5, None, 'preferences.css:34-35'),
    ('dark', 'dark theme toggle #ffd700 on surface (non-text)', '#ffd700', '#2d3748', 3.0, None, 'site.css:79'),
    ('light', '.hint-text muted 0.75rem on surface', '#5a6a7d', '#ffffff', 4.5, None, 'network.css:59-61'),
    ('light', 'map-click-hint white on rgba(0,0,0,.65) over tiles(#f2efe9)', '#ffffff', 'rgba(0,0,0,0.65)', 4.5, '#f2efe9', 'network.css:157-158'),
    ('light', 'viewshed swatch hidden #3a3a45 vs --background (non-text)', '#3a3a45', '#f7fafc', 3.0, None, 'spatialcompute.css:116'),
    ('dark', 'viewshed swatch hidden #3a3a45 vs --background (non-text)', '#3a3a45', '#1a202c', 3.0, None, 'spatialcompute.css:116'),
    ('light', 'network route #2563eb vs OSM land #f2efe9 (non-text)', '#2563eb', '#f2efe9', 3.0, None, 'network.css:173'),
    ('light', 'score-bar track rgba(255,255,255,.08) vs surface (non-text)', 'rgba(255,255,255,0.08)', '#ffffff', 3.0, None, 'homefinder.css:244'),
    ('light', 'Bootstrap .text-muted light rgba(33,37,41,.75) on white', 'rgba(33,37,41,0.75)', '#ffffff', 4.5, None, 'bootstrap 5.3 --bs-secondary-color'),
    ('dark', 'Bootstrap .text-muted dark rgba(222,226,230,.75) on --surface', 'rgba(222,226,230,0.75)', '#2d3748', 4.5, None, 'bootstrap 5.3 --bs-secondary-color'),
    ('dark', 'Bootstrap .text-muted dark rgba(222,226,230,.75) on --background', 'rgba(222,226,230,0.75)', '#1a202c', 4.5, None, 'bootstrap 5.3 --bs-secondary-color'),
]
for t, label, fg, bg, need, under, src in fixed:
    check(t, label, fg, bg, need, under, src)

# Chart palettes (graphical objects need 3:1 against the plot background, WCAG 1.4.11)
palettes = {
    'Clustering PALETTE (Clustering/app.js:6-9)': ["#e6194B", "#3cb44b", "#4363d8", "#f58231", "#911eb4", "#42d4f4", "#f032e6", "#bfef45", "#fabed4", "#469990"],
    'Fleet palette (Fleet/app.js:10-12)': ["#2563eb", "#e8590c", "#2f9e44", "#ae3ec9", "#0d9488", "#c2255c", "#f59f00", "#4263eb", "#7048e8", "#1098ad"],
    'Response BAND_COLORS (Response/app.js:12)': ["#2f9e44", "#74b816", "#f59f00", "#e8590c", "#c92a2a"],
    'Outage DEVICE_COLORS (Outage/app.js:20)': ["#868e96", "#7048e8", "#f59f00", "#1098ad", "#e8590c", "#0ca678", "#4c6ef5"],
    'Outage state colours (Outage/app.js:22-25)': ["#e03131", "#1c7ed6", "#f59f00", "#2f9e44"],
    'Network explored swatches (network.css:218-219)': ["#0d9488", "#f59e0b"],
}
pal_rows = []
for name, cols in palettes.items():
    for theme, bg in (('light --background', '#f7fafc'), ('dark --background', '#1a202c'), ('dark --surface', '#2d3748')):
        fails = [(c, round(ratio(c, bg), 2)) for c in cols if ratio(c, bg) < 3.0]
        pal_rows.append((name, theme, len(cols), len(fails), fails))

if __name__ == '__main__':
    print(f"{'theme':6} | {'pair':78} | {'ratio':>6} | need | result | source")
    for t, label, fg, bg, r, need, res, src in rows:
        print(f"{t:6} | {label[:78]:78} | {r:6.2f} | {need:4} | {res:6} | {src}")
    print()
    for name, theme, n, nf, fails in pal_rows:
        print(f"{name} on {theme}: {nf}/{n} below 3:1 -> {fails}")
