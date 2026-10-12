// fe-audit.js — DOM/geometry assertions for Portfolio pages. No screenshots.
// Copy this file to Portfolio.Web/wwwroot/__audit/ while auditing (temporary; never commit it there).
// Exports: run, assert, focusIndicators, tabOrder, focusProbe.
// Usage (javascript_tool):  (await import('/__audit/fe-audit.js?v=1')).run({ wait: 1200 })
// Returns a compact JSON-able object. Theme passes use window.SitePreferences when present.

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const FOCUSABLE = 'a[href], area[href], button, input:not([type="hidden"]), select, textarea, summary, iframe, ' +
    '[tabindex], [contenteditable="true"], [role="button"], [role="link"], [role="checkbox"], [role="radio"], ' +
    '[role="switch"], [role="tab"], [role="menuitem"], [role="option"], [role="slider"], [role="combobox"]';

function describe(el) {
    if (!el || el.nodeType !== 1) return String(el);
    let s = el.tagName.toLowerCase();
    if (el.id) s += '#' + el.id;
    const cls = (typeof el.className === 'string' ? el.className : (el.className && el.className.baseVal) || '')
        .trim().split(/\s+/).filter(Boolean).slice(0, 3);
    if (cls.length) s += '.' + cls.join('.');
    const role = el.getAttribute('role');
    if (role) s += `[role=${role}]`;
    return s;
}

function path(el) {
    const parts = [];
    let n = el;
    for (let i = 0; n && n.nodeType === 1 && i < 3; i++, n = n.parentElement) parts.unshift(describe(n));
    return parts.join(' > ');
}

function isRendered(el) {
    if (!el.isConnected) return false;
    if (el.closest('[hidden]')) return false;
    const cs = getComputedStyle(el);
    if (cs.display === 'none' || cs.visibility === 'hidden' || cs.visibility === 'collapse') return false;
    const r = el.getBoundingClientRect();
    if (r.width === 0 && r.height === 0) return false;
    // display:none ancestors make rect 0; also treat visually-hidden 1px clip as not rendered
    if (r.width <= 1 && r.height <= 1) return false;
    if (cs.clip === 'rect(0px, 0px, 0px, 0px)' || cs.clipPath === 'inset(50%)') return false;
    return true;
}

function hiddenFromAT(el) {
    return !!el.closest('[aria-hidden="true"], [inert]');
}

// ---------- colour maths ----------
function parseColor(str) {
    if (!str) return null;
    const m = str.match(/rgba?\(([^)]+)\)/);
    if (!m) {
        if (str === 'transparent') return [0, 0, 0, 0];
        const c = str.match(/color\(srgb ([\d.]+) ([\d.]+) ([\d.]+)(?: \/ ([\d.]+))?\)/);
        if (c) return [c[1] * 255, c[2] * 255, c[3] * 255, c[4] === undefined ? 1 : +c[4]];
        return null;
    }
    const p = m[1].split(/[\s,/]+/).filter(Boolean).map(Number);
    return [p[0], p[1], p[2], p.length > 3 ? p[3] : 1];
}
const over = (fg, bg) => [0, 1, 2].map((i) => fg[i] * fg[3] + bg[i] * (1 - fg[3])).concat(1);
function lum(c) {
    const f = (v) => { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
    return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]);
}
function ratio(a, b) {
    const l1 = lum(a), l2 = lum(b);
    return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
}
const rgb = (c) => c ? `rgb(${c.slice(0, 3).map((v) => Math.round(v)).join(',')})` : '?';

// Walk ancestors compositing translucent backgrounds; a gradient/image makes the result unknown.
function effectiveBg(el) {
    // Returns { colors: [candidate backgrounds] } — more than one when a gradient sits in the stack
    // (every stop is composited over what lies beneath; the caller takes the worst case).
    const layers = [];
    for (let n = el; n && n.nodeType === 1; n = n.parentElement) {
        const cs = getComputedStyle(n);
        const c = parseColor(cs.backgroundColor);
        if (cs.backgroundImage && cs.backgroundImage !== 'none') {
            if (/url\(/.test(cs.backgroundImage)) return { unknown: 'bg-url:' + describe(n) };
            const stops = (cs.backgroundImage.match(/rgba?\([^)]+\)/g) || []).map(parseColor).filter(Boolean);
            layers.push({ grad: stops, under: c && c[3] > 0 ? c : null, opaque: stops.length && stops.every((s) => s[3] >= 1) });
            if (layers[layers.length - 1].opaque) break;
            if (c && c[3] >= 1) break;
            continue;
        }
        if (c && c[3] > 0) {
            layers.push({ color: c });
            if (c[3] >= 1) break;
        }
    }
    let bases = [[255, 255, 255, 1]];
    for (let i = layers.length - 1; i >= 0; i--) {
        const L = layers[i];
        if (L.color) bases = bases.map((b) => (L.color[3] >= 1 ? L.color : over(L.color, b)));
        else {
            const under = bases.map((b) => (L.under ? (L.under[3] >= 1 ? L.under : over(L.under, b)) : b));
            const next = [];
            for (const b of under) for (const s of L.grad) next.push(s[3] >= 1 ? s : over(s, b));
            bases = next.length ? next : under;
        }
    }
    return { colors: bases, gradient: layers.some((l) => l.grad) };
}

// ---------- checks ----------
function overflowCheck() {
    const de = document.documentElement;
    const cw = de.clientWidth;
    const res = { scrollW: de.scrollWidth, clientW: cw, innerW: innerWidth, pageScrollX: de.scrollWidth > cw };
    const offenders = [];
    const all = document.body.querySelectorAll('*');
    for (const el of all) {
        if (el.closest('.leaflet-container, .esri-view, .esri-ui, .visually-hidden, .visually-hidden-focusable:not(:focus)')) continue;
        const r = el.getBoundingClientRect();
        if (r.width <= 1 || r.height <= 1) continue;
        if (r.right <= cw + 0.5 && r.left >= -0.5) continue;
        // contained if any ancestor (below body) clips or scrolls horizontally
        let contained = false;
        for (let a = el.parentElement; a && a !== document.body && a !== de; a = a.parentElement) {
            const ox = getComputedStyle(a).overflowX;
            if (ox !== 'visible') { contained = true; break; }
        }
        if (contained) continue;
        const cs = getComputedStyle(el);
        if (cs.position === 'fixed' && (cs.visibility === 'hidden' || +cs.opacity === 0)) continue;
        offenders.push({ el, txt: `${path(el)} [${Math.round(r.left)}..${Math.round(r.right)}]` });
    }
    // keep outermost offenders only
    const outer = offenders.filter((o) => !offenders.some((p) => p !== o && p.el.contains(o.el)));
    res.offenders = outer.slice(0, 6).map((o) => o.txt);
    res.offenderCount = outer.length;
    return res;
}

function textOf(el) {
    let s = '';
    for (const n of el.childNodes) {
        if (n.nodeType === 3) s += n.nodeValue;
        else if (n.nodeType === 1) {
            if (n.getAttribute('aria-hidden') === 'true') continue;
            const cs = getComputedStyle(n);
            if (cs.display === 'none' || cs.visibility === 'hidden') continue;
            if (n.tagName === 'IMG') s += ' ' + (n.getAttribute('alt') || '');
            else if (n.tagName.toLowerCase() === 'svg') {
                const t = n.querySelector('title'); s += ' ' + (n.getAttribute('aria-label') || (t && t.textContent) || '');
            } else s += ' ' + (n.getAttribute('aria-label') || textOf(n));
        }
    }
    return s.replace(/\s+/g, ' ').trim();
}

function accName(el) {
    const lb = el.getAttribute('aria-labelledby');
    if (lb) {
        const t = lb.split(/\s+/).map((id) => { const r = document.getElementById(id); return r ? textOf(r) || r.textContent : ''; }).join(' ').trim();
        if (t) return t;
    }
    const al = el.getAttribute('aria-label');
    if (al && al.trim()) return al.trim();
    const tag = el.tagName;
    if (/^(INPUT|SELECT|TEXTAREA|METER|PROGRESS|OUTPUT)$/.test(tag)) {
        if (el.id) {
            const l = document.querySelector(`label[for="${CSS.escape(el.id)}"]`);
            if (l && textOf(l)) return textOf(l);
        }
        const wl = el.closest('label');
        if (wl && textOf(wl)) return textOf(wl);
        if (tag === 'INPUT' && /^(button|submit|reset)$/.test(el.type)) return el.value || el.type;
        if (tag === 'INPUT' && el.type === 'image') return el.alt || '';
    } else {
        const t = textOf(el);
        if (t) return t;
    }
    const title = el.getAttribute('title');
    if (title && title.trim()) return title.trim();
    if (el.placeholder) return '(placeholder only) ' + el.placeholder;
    return '';
}

function interactive() {
    return [...document.querySelectorAll(FOCUSABLE)].filter((el) => {
        if (el.matches('[tabindex="-1"]') && !el.matches('a[href], button, input, select, textarea, [role]')) return false;
        if (el.closest('[inert]')) return false;
        return isRendered(el);
    });
}

function targetSizeCheck(els) {
    const rects = els.map((el) => ({ el, r: el.getBoundingClientRect() }));
    const small = [];
    for (const o of rects) {
        const { el, r } = o;
        if (el.disabled) continue;
        if (r.width >= 24 && r.height >= 24) continue;
        if (hiddenFromAT(el) && el.tabIndex < 0) continue;
        // inline exception: link inside a run of text
        const cs = getComputedStyle(el);
        if (el.tagName === 'A' && cs.display === 'inline' && el.parentElement && textOf(el.parentElement).length > textOf(el).length + 3) continue;
        // spacing exception: 24px circle centred on the target must not hit another target
        const cx = r.left + r.width / 2, cy = r.top + r.height / 2;
        let collide = false;
        for (const p of rects) {
            if (p === o || p.el.contains(el) || el.contains(p.el)) continue;
            const q = p.r;
            const dx = Math.max(q.left - cx, 0, cx - q.right), dy = Math.max(q.top - cy, 0, cy - q.bottom);
            const pSmall = q.width < 24 || q.height < 24;
            if (pSmall) {
                const px = q.left + q.width / 2, py = q.top + q.height / 2;
                if (Math.hypot(px - cx, py - cy) < 24) { collide = true; break; }
            } else if (Math.hypot(dx, dy) < 12) { collide = true; break; }
        }
        small.push(`${describe(el)} ${Math.round(r.width)}x${Math.round(r.height)}${collide ? '' : ' (spacing-ok)'}`);
    }
    const failing = small.filter((s) => !s.endsWith('(spacing-ok)'));
    return { undersized: small.length, failing: failing.length, examples: failing.slice(0, 8) };
}

function nameCheck(els) {
    const missing = [], placeholderOnly = [];
    for (const el of els) {
        if (hiddenFromAT(el)) continue;
        const n = accName(el);
        if (!n) missing.push(path(el));
        else if (n.startsWith('(placeholder only)')) placeholderOnly.push(describe(el));
    }
    return { missing: missing.length, examples: missing.slice(0, 8), placeholderOnly };
}

function staticChecks() {
    const out = {};
    // images
    out.imgNoAlt = [...document.querySelectorAll('img:not([alt])')].filter((i) => !i.closest('.leaflet-container, .esri-view'))
        .map((i) => describe(i) + ' ' + (i.getAttribute('src') || '').slice(-40)).slice(0, 6);
    out.leafletImgNoAlt = document.querySelectorAll('.leaflet-container img:not([alt])').length;
    // role=img / svg naming
    out.roleImgNoName = [...document.querySelectorAll('[role="img"]')].filter((e) => !accName(e)).map(describe).slice(0, 6);
    out.svgUnlabelled = [...document.querySelectorAll('svg')].filter((s) => !s.closest('[aria-hidden="true"], .leaflet-container, .esri-view, button, a') &&
        !s.getAttribute('role') && !s.getAttribute('aria-label') && !s.getAttribute('aria-hidden') && isRendered(s)).map(describe).slice(0, 6);
    out.canvasNoText = [...document.querySelectorAll('canvas')].filter((c) => !c.closest('.leaflet-container, .esri-view') && !accName(c) && !c.textContent.trim()).map(describe);
    // ids
    const seen = {};
    document.querySelectorAll('[id]').forEach((e) => { seen[e.id] = (seen[e.id] || 0) + 1; });
    out.dupIds = Object.entries(seen).filter(([, n]) => n > 1).map(([id, n]) => `${id}x${n}`);
    // headings
    const hs = [...document.querySelectorAll('h1,h2,h3,h4,h5,h6,[role="heading"]')].filter((h) => isRendered(h) && !hiddenFromAT(h));
    const levels = hs.map((h) => +(h.getAttribute('aria-level') || h.tagName[1]));
    const skips = [];
    for (let i = 1; i < levels.length; i++) if (levels[i] > levels[i - 1] + 1) skips.push(`h${levels[i - 1]}->h${levels[i]} "${textOf(hs[i]).slice(0, 30)}"`);
    out.headings = { count: hs.length, h1: levels.filter((l) => l === 1).length, outline: levels.join(''), skips: skips.slice(0, 5) };
    // landmarks
    const top = (sel) => [...document.querySelectorAll(sel)].filter((e) => !e.closest('main, article, aside, nav, section') || e.tagName === 'MAIN');
    out.landmarks = {
        main: document.querySelectorAll('main, [role="main"]').length,
        banner: top('header, [role="banner"]').length,
        contentinfo: top('footer, [role="contentinfo"]').length,
        nav: document.querySelectorAll('nav, [role="navigation"]').length,
        regionsNamed: document.querySelectorAll('section[aria-label], section[aria-labelledby], [role="region"]').length,
        application: [...document.querySelectorAll('[role="application"]')].map(describe)
    };
    // aria-label on generic elements (ignored by AT)
    out.ariaLabelOnGeneric = [...document.querySelectorAll('div[aria-label], span[aria-label], p[aria-label]')]
        .filter((e) => !e.getAttribute('role') && !e.closest('.leaflet-container, .esri-view')).map(describe).slice(0, 8);
    // focusable inside aria-hidden
    out.focusableInAriaHidden = [...document.querySelectorAll('[aria-hidden="true"]')].flatMap((h) =>
        [...h.querySelectorAll(FOCUSABLE)].filter((f) => f.tabIndex >= 0 && !f.disabled && isRendered(f))).map(describe).slice(0, 6);
    // live regions
    out.live = [...document.querySelectorAll('[aria-live], [role="status"], [role="alert"], [role="log"]')]
        .map((e) => `${describe(e)}:${e.getAttribute('aria-live') || e.getAttribute('role')}`).slice(0, 12);
    // icons inside headings/buttons without aria-hidden (FA glyphs)
    out.faExposed = [...document.querySelectorAll('i[class*="fa-"]')].filter((i) =>
        !i.closest('[aria-hidden="true"], [aria-label], [aria-labelledby]') && isRendered(i)).length;
    // positive tabindex
    out.posTabindex = [...document.querySelectorAll('[tabindex]')].filter((e) => +e.getAttribute('tabindex') > 0).map(describe);
    // form controls with both a <label for> and an aria-label that does not contain the visible label (2.5.3)
    out.labelInName = [...document.querySelectorAll('input[aria-label], select[aria-label], textarea[aria-label]')].filter((el) => {
        if (!el.id) return false;
        const l = document.querySelector(`label[for="${CSS.escape(el.id)}"]`);
        if (!l) return false;
        const vis = textOf(l).toLowerCase().replace(/[:$%0-9.,]+/g, ' ').replace(/\s+/g, ' ').trim();
        return vis && !el.getAttribute('aria-label').toLowerCase().includes(vis.split(' ').slice(0, 2).join(' '));
    }).map((el) => `${describe(el)} label="${textOf(document.querySelector(`label[for="${CSS.escape(el.id)}"]`)).slice(0, 30)}" aria-label="${el.getAttribute('aria-label')}"`).slice(0, 6);
    return out;
}

function contrastCheck() {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
        acceptNode: (n) => (n.nodeValue.trim() ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP)
    });
    const seen = new Set();
    const fails = [], gradFails = [];
    let checked = 0, unknown = 0;
    while (walker.nextNode()) {
        const el = walker.currentNode.parentElement;
        if (!el || seen.has(el)) continue;
        seen.add(el);
        if (el.closest('script, style, noscript, .leaflet-container, .esri-view, .visually-hidden, [aria-hidden="true"]')) continue;
        if (!isRendered(el)) continue;
        if (el.closest('button:disabled, input:disabled, select:disabled, textarea:disabled, fieldset:disabled, [aria-disabled="true"], .disabled')) continue;
        const cs = getComputedStyle(el);
        const isSvg = el instanceof SVGElement;
        let fg = parseColor(isSvg ? cs.fill : cs.color);
        if (!fg) { unknown++; continue; }
        const bg = effectiveBg(isSvg ? (el.ownerSVGElement || el).parentElement || el : el);
        if (bg.unknown) { unknown++; continue; }
        let worst = Infinity, worstBg = null, worstFg = null;
        for (const b of bg.colors) {
            const fgc = fg[3] < 1 ? over(fg, b) : fg;
            const r = ratio(fgc, b);
            if (r < worst) { worst = r; worstBg = b; worstFg = fgc; }
        }
        const size = parseFloat(cs.fontSize);
        const weight = parseInt(cs.fontWeight, 10) || 400;
        const large = size >= 24 || (size >= 18.66 && weight >= 700);
        const need = large ? 3 : 4.5;
        checked++;
        if (worst < need - 0.01) {
            const rec = `${worst.toFixed(2)}<${need} ${describe(el)} "${walker.currentNode.nodeValue.trim().slice(0, 24)}" fg=${rgb(worstFg)} bg=${rgb(worstBg)} ${Math.round(size)}px/${weight}`;
            (bg.gradient ? gradFails : fails).push(rec);
        }
    }
    // collapse identical selector+ratio
    const uniq = (list) => [...new Set(list.map((f) => f.replace(/ ".*?" /, ' ')))];
    return { checked, unknown, fails: fails.length, examples: uniq(fails).slice(0, 10), gradientWorstCase: uniq(gradFails).slice(0, 5) };
}

function setTheme(t) {
    if (window.SitePreferences && typeof window.SitePreferences.set === 'function') {
        window.SitePreferences.set('theme', t);
    } else {
        document.documentElement.setAttribute('data-bs-theme', t);
        document.body.classList.toggle('dark-mode', t === 'dark');
    }
}

export async function run(opts = {}) {
    if (opts.wait) await sleep(opts.wait);
    if (opts.click) {
        const b = document.querySelector(opts.click);
        if (b && !b.disabled) { b.click(); await sleep(opts.afterClick || 2500); }
    }
    const startDark = document.body.classList.contains('dark-mode');
    const out = { path: location.pathname + location.search, title: document.title.slice(0, 40), vw: innerWidth };
    out.overflow = overflowCheck();
    const els = interactive();
    out.targets = targetSizeCheck(els);
    out.names = nameCheck(els);
    Object.assign(out, staticChecks());
    if (opts.contrast !== false) {
        out.contrast = {};
        for (const t of opts.themes || ['light', 'dark']) {
            setTheme(t);
            await sleep(opts.themeWait || 250);
            out.contrast[t] = contrastCheck();
            if (t === 'dark') out.overflowDark = overflowCheck().pageScrollX;
        }
        setTheme(startDark ? 'dark' : 'light');
    }
    return out;
}

// Keyboard helper: list the first N tab stops in DOM/tabindex order with focus-visibility info.
export function tabOrder(n = 25) {
    const els = [...document.querySelectorAll(FOCUSABLE)].filter((e) => e.tabIndex >= 0 && !e.disabled && isRendered(e) && !e.closest('[inert]'));
    els.sort((a, b) => (a.tabIndex || 0) - (b.tabIndex || 0) || 0);
    return els.slice(0, n).map((e) => `${describe(e)} "${accName(e).slice(0, 30)}"`);
}

// Focus every visible tab stop and classify its focus indicator. Needs keyboard modality:
// press Tab once with the computer tool before calling, or :focus-visible will not match.
// ring = outline (>=1px, visible colour) or an opaque-enough box-shadow; weak = only colour /
// border / faint-shadow changes; none = nothing changes at all.
function snap(el) {
    const c = getComputedStyle(el);
    return { os: c.outlineStyle, ow: parseFloat(c.outlineWidth) || 0, oc: parseColor(c.outlineColor), bs: c.boxShadow,
        bc: c.borderColor, bg: c.backgroundColor, co: c.color, td: c.textDecorationLine };
}
function shadowAlpha(bs) {
    if (!bs || bs === 'none') return 0;
    return Math.max(0, ...(bs.match(/rgba?\([^)]+\)/g) || []).map((s) => (parseColor(s) || [0, 0, 0, 0])[3]));
}
export function focusIndicators(limit = 400) {
    const root = document.documentElement;
    root.classList.add('theme-switching'); // the site's own transition kill-switch (preferences.css)
    const prev = document.activeElement;
    const els = interactive().filter((e) => e.tabIndex >= 0 && !e.disabled && !hiddenFromAT(e)).slice(0, limit);
    const none = [], weak = [];
    for (const el of els) {
        el.blur();
        const b = snap(el);
        el.focus({ preventScroll: true, focusVisible: true });
        if (document.activeElement !== el || !el.matches(':focus-visible')) continue;
        const a = snap(el);
        const ring = (a.os !== 'none' && a.ow >= 1 && a.oc && a.oc[3] > 0.5) || shadowAlpha(a.bs) > 0.5;
        const changed = a.bs !== b.bs || a.bc !== b.bc || a.bg !== b.bg || a.co !== b.co || a.td !== b.td;
        const label = `${describe(el)} "${accName(el).slice(0, 24)}"`;
        if (!ring && !changed) none.push(label);
        else if (!ring) weak.push(label);
        el.blur();
    }
    root.classList.remove('theme-switching');
    if (prev && typeof prev.focus === 'function') prev.focus({ preventScroll: true });
    const uniq = (l) => [...new Set(l.map((s) => s.replace(/ ".*"$/, '')))];
    return { checked: els.length, none: none.length, weak: weak.length, noneKinds: uniq(none).slice(0, 10), weakKinds: uniq(weak).slice(0, 10) };
}

// Turns a run() result into pass/fail lines against the Stage 2 acceptance criteria.
export function assert(r, allow = {}) {
    const fails = [];
    if (r.overflow && r.overflow.pageScrollX) fails.push('horizontal page scroll: ' + r.overflow.offenders.join('; '));
    if (r.overflowDark) fails.push('horizontal page scroll in dark mode');
    const tAllow = allow.targets || [];
    const t = (r.targets.examples || []).filter((x) => !tAllow.some((a) => x.includes(a)));
    if (t.length) fails.push('targets <24px without spacing: ' + t.join(', '));
    const nAllow = allow.names || ['esri-view-surface'];
    const n = (r.names.examples || []).filter((x) => !nAllow.some((a) => x.includes(a)));
    if (n.length) fails.push('controls without a name: ' + n.join(', '));
    if (r.imgNoAlt && r.imgNoAlt.length) fails.push('img without alt: ' + r.imgNoAlt.join(', '));
    if (r.dupIds && r.dupIds.length) fails.push('duplicate ids: ' + r.dupIds.join(', '));
    if (r.headings && r.headings.h1 !== 1) fails.push('h1 count = ' + r.headings.h1);
    if (r.headings && r.headings.skips.length) fails.push('heading skips: ' + r.headings.skips.join(', '));
    if (r.ariaLabelOnGeneric && r.ariaLabelOnGeneric.length) fails.push('aria-label on generic element: ' + r.ariaLabelOnGeneric.join(', '));
    if (r.labelInName && r.labelInName.length) fails.push('label-in-name: ' + r.labelInName.join(' | '));
    if (r.focusableInAriaHidden && r.focusableInAriaHidden.length) fails.push('focusable inside aria-hidden: ' + r.focusableInAriaHidden.join(', '));
    for (const t2 of Object.keys(r.contrast || {})) {
        const c = r.contrast[t2];
        if (c.fails) fails.push(`contrast (${t2}): ` + c.examples.join(' ; '));
    }
    return fails.length ? fails : ['PASS'];
}

// Focus an element and report whether a visible focus indicator is drawn (outline or box-shadow change).
export function focusProbe(selector) {
    const el = typeof selector === 'string' ? document.querySelector(selector) : selector;
    if (!el) return 'not found: ' + selector;
    const before = getComputedStyle(el);
    const b = { o: before.outlineStyle + ' ' + before.outlineWidth + ' ' + before.outlineColor, s: before.boxShadow, bc: before.borderColor, bg: before.backgroundColor };
    el.focus({ focusVisible: true });
    const cs = getComputedStyle(el);
    const a = { o: cs.outlineStyle + ' ' + cs.outlineWidth + ' ' + cs.outlineColor, s: cs.boxShadow, bc: cs.borderColor, bg: cs.backgroundColor };
    return { el: describe(el), focused: document.activeElement === el, focusVisible: el.matches(':focus-visible'), before: b, after: a };
}
