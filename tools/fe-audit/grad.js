// grad.js - gradient-aware contrast probe. Copy next to fe-audit.js in Portfolio.Web/wwwroot/__audit/
// (temporary; do not commit it there), then in javascript_tool:
//   (await import('/__audit/grad.js?v=1')).sample('<section selector>', ['<text selector>', ...])
// Samples the real colour of a 135deg linear-gradient under each text box (grid of points)
// and reports the minimum contrast actually reached. Assumes stops evenly listed with %.
function parse(str) { const p = str.match(/[\d.]+/g).map(Number); return [p[0], p[1], p[2], p.length > 3 ? p[3] : 1]; }
function lum(c) { const f = (v) => { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]); }
function ratio(a, b) { const l1 = lum(a), l2 = lum(b); return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05); }
const over = (fg, bg) => [0, 1, 2].map((i) => fg[i] * fg[3] + bg[i] * (1 - fg[3])).concat(1);

function gradientAt(section, x, y) {
    const cs = getComputedStyle(section);
    const g = cs.backgroundImage;
    const m = g.match(/linear-gradient\((\d+)deg,\s*(.*)\)$/);
    if (!m) return null;
    const ang = +m[1] * Math.PI / 180;
    const stops = [...m[2].matchAll(/(rgba?\([^)]+\))\s*([\d.]+)%/g)].map((s) => ({ c: parse(s[1]), p: +s[2] / 100 }));
    const r = section.getBoundingClientRect();
    const W = r.width, H = r.height;
    const L = Math.abs(W * Math.sin(ang)) + Math.abs(H * Math.cos(ang));
    const px = x - (r.left + W / 2), py = y - (r.top + H / 2);
    const t = 0.5 + (px * Math.sin(ang) - py * Math.cos(ang)) / L;
    let a = stops[0], b = stops[stops.length - 1];
    for (let i = 0; i < stops.length - 1; i++) if (t >= stops[i].p && t <= stops[i + 1].p) { a = stops[i]; b = stops[i + 1]; }
    const k = Math.max(0, Math.min(1, (t - a.p) / ((b.p - a.p) || 1)));
    return [0, 1, 2].map((i) => a.c[i] + (b.c[i] - a.c[i]) * k).concat(1);
}

export function sample(sectionSel, textSels) {
    const section = document.querySelector(sectionSel);
    const out = [];
    for (const sel of textSels) {
        for (const el of document.querySelectorAll(sel)) {
            if (!section.contains(el)) continue;
            const r = el.getBoundingClientRect();
            if (!r.width) continue;
            const fg = parse(getComputedStyle(el).color);
            let worst = Infinity;
            // range objects give the actual glyph boxes of each line
            const range = document.createRange(); range.selectNodeContents(el);
            for (const box of range.getClientRects()) {
                for (let i = 0; i <= 4; i++) for (let j = 0; j <= 2; j++) {
                    const x = box.left + box.width * i / 4, y = box.top + box.height * j / 2;
                    const bg = gradientAt(section, x, y);
                    // translucent element background (e.g. .contact-link rgba(255,255,255,.1)) on top
                    const own = parse(getComputedStyle(el).backgroundColor);
                    const base = own[3] > 0 ? over(own, bg) : bg;
                    const fgc = fg[3] < 1 ? over(fg, base) : fg;
                    worst = Math.min(worst, ratio(fgc, base));
                }
            }
            const cs = getComputedStyle(el);
            out.push(`${sel} ${Math.round(parseFloat(cs.fontSize))}px/${cs.fontWeight} min=${worst.toFixed(2)}`);
        }
    }
    return out;
}
