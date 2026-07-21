/**
 * preferences.js — user-controllable display & accessibility settings
 * -------------------------------------------------------------------
 * Single source of truth for theme + accessibility preferences.
 *
 * Design goals:
 *   • Extensible  — add a preference by adding one entry to REGISTRY.
 *                   Each entry knows its default, how to normalize a value,
 *                   and how to apply it to the DOM. The panel controls bind
 *                   themselves generically via [data-pref] attributes.
 *   • No flash    — the inline <head> script in _Layout applies the same
 *                   attributes before first paint; this file re-applies and
 *                   keeps everything in sync afterwards.
 *   • Respectful  — the OS settings (prefers-color-scheme, prefers-reduced-motion)
 *                   seed the defaults; "reduce motion" left on auto keeps
 *                   tracking the OS live.
 *
 * State is persisted as JSON under localStorage["sitePrefs"]. A legacy
 * localStorage["theme"] ("dark"/"light") value is migrated and mirrored so
 * older code paths keep working.
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'sitePrefs';
    var root = document.documentElement;
    var mqDark = window.matchMedia('(prefers-color-scheme: dark)');
    var mqReduce = window.matchMedia('(prefers-reduced-motion: reduce)');

    /**
     * The preference registry. To add a preference, add an entry here and a
     * matching control (data-pref="key") in the panel markup — nothing else.
     */
    var REGISTRY = {
        // Two states only. A third "System" option looked tidy but behaved badly:
        // it is invisible in the UI (the button shows a half-circle, not the theme
        // you actually get) and it can flip the site mid-session when the OS
        // crosses its light/dark schedule. The device preference now seeds the
        // default on first visit and is not consulted again.
        theme: {
            def: mqDark.matches ? 'dark' : 'light',
            values: ['light', 'dark'],
            apply: function (v) {
                var dark = v === 'dark';
                root.setAttribute('data-bs-theme', dark ? 'dark' : 'light');
                root.setAttribute('data-theme', v);
                if (document.body) document.body.classList.toggle('dark-mode', dark);
            }
        },
        contrast: {
            def: 'normal',
            values: ['normal', 'high'],
            toggleOn: 'high',
            apply: function (v) { root.setAttribute('data-contrast', v); }
        },
        motion: {
            def: 'auto',
            values: ['auto', 'reduce'],
            toggleOn: 'reduce',
            apply: function (v) {
                var reduce = v === 'reduce' || (v === 'auto' && mqReduce.matches);
                root.setAttribute('data-motion', reduce ? 'reduce' : 'no-pref');
            }
        },
        underline: {
            def: 'off',
            values: ['off', 'on'],
            toggleOn: 'on',
            apply: function (v) { root.setAttribute('data-underline', v); }
        },
        readable: {
            def: 'off',
            values: ['off', 'on'],
            toggleOn: 'on',
            apply: function (v) { root.setAttribute('data-readable', v); }
        },
        fontScale: {
            def: 1,
            min: 0.85,
            max: 1.5,
            step: 0.05,
            normalize: function (v) {
                v = parseFloat(v);
                if (isNaN(v)) return 1;
                return Math.min(this.max, Math.max(this.min, v));
            },
            apply: function (v) { root.style.setProperty('--user-font-scale', v); }
        }
    };

    function defaults() {
        var d = {};
        for (var k in REGISTRY) d[k] = REGISTRY[k].def;
        return d;
    }

    function load() {
        var saved = {};
        try { saved = JSON.parse(localStorage.getItem(STORAGE_KEY)) || {}; } catch (e) { saved = {}; }
        // Migrate the old two-value theme key.
        if (saved.theme === undefined) {
            var legacy = localStorage.getItem('theme');
            if (legacy === 'dark' || legacy === 'light') saved.theme = legacy;
        }
        var prefs = defaults();
        for (var k in REGISTRY) {
            if (saved[k] !== undefined) prefs[k] = normalize(k, saved[k]);
        }
        return prefs;
    }

    function normalize(key, value) {
        var spec = REGISTRY[key];
        if (!spec) return value;
        if (typeof spec.normalize === 'function') return spec.normalize(value);
        if (spec.values && spec.values.indexOf(value) === -1) return spec.def;
        return value;
    }

    var prefs = load();

    function effectiveDark(themeVal) {
        return (themeVal || prefs.theme) === 'dark';
    }

    function save() {
        try { localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs)); } catch (e) { /* storage full/blocked */ }
        try { localStorage.setItem('theme', effectiveDark() ? 'dark' : 'light'); } catch (e) { /* legacy mirror */ }
    }

    function applyAll() {
        for (var k in REGISTRY) REGISTRY[k].apply.call(REGISTRY[k], prefs[k]);
        updateThemeToggle();
        syncControls();
        window.dispatchEvent(new CustomEvent('themeChanged', { detail: { dark: effectiveDark(), theme: prefs.theme } }));
        window.dispatchEvent(new CustomEvent('preferencesChanged', { detail: getAll() }));
    }

    /**
     * Applies a palette change with transitions suppressed for one frame.
     *
     * A theme switch only changes custom properties. Chrome will not restart a
     * running transition when the var() behind a property changes but the
     * declaration text does not, so transitioning elements keep the previous
     * palette until something forces a full re-render. That left body text and
     * every .btn-outline-accent on the light-mode green after switching to dark
     * — a 2.29:1 contrast ratio on a control that reads 6.46:1 on a fresh load.
     *
     * Killing transitions across the switch makes every consumer resolve
     * against the new values immediately. Two rAFs: the first lets the new
     * styles apply, the second restores transitions after they have painted.
     */
    function withoutTransitions(fn) {
        var r = document.documentElement;
        r.classList.add('theme-switching');
        fn();
        // Reading offsetHeight forces the suppressed styles to flush before the
        // class comes back off; without it the removal can coalesce into the
        // same frame and the suppression never takes effect.
        void r.offsetHeight;

        var restored = false;
        var restore = function () {
            if (restored) return;
            restored = true;
            r.classList.remove('theme-switching');
        };

        // rAF is the clean path — it restores transitions the moment the new
        // palette has painted. But rAF does not fire in a background tab, and
        // leaving transitions off forever is a worse bug than the one this
        // works around, so a timer guarantees the class always comes back.
        if (typeof window.requestAnimationFrame === 'function') {
            window.requestAnimationFrame(function () {
                window.requestAnimationFrame(restore);
            });
        }
        window.setTimeout(restore, 120);
    }

    function set(key, value) {
        if (!REGISTRY[key]) return;
        prefs[key] = normalize(key, value);
        save();
        // Only the palette keys repaint the whole page; a range drag should not
        // thrash the class on every input event.
        if (key === 'theme' || key === 'contrast') withoutTransitions(applyAll);
        else applyAll();
        announce(describe(key));
    }

    function reset() {
        prefs = defaults();
        save();
        withoutTransitions(applyAll);
        announce('Display settings reset to defaults');
    }

    function getAll() {
        var out = {};
        for (var k in prefs) out[k] = prefs[k];
        return out;
    }

    /* ---- Live OS changes feed the remaining "auto" option (motion) ----
       Theme deliberately does not listen: once the visitor has a stored
       light/dark value, the OS no longer gets to change it underneath them. */
    function onSystemChange() {
        if (prefs.motion === 'auto') REGISTRY.motion.apply(prefs.motion);
    }
    if (mqReduce.addEventListener) {
        mqReduce.addEventListener('change', onSystemChange);
    } else if (mqReduce.addListener) { // older Safari
        mqReduce.addListener(onSystemChange);
    }

    /* ---- Screen-reader announcements ---- */
    function announce(msg) {
        var live = document.getElementById('a11yAnnounce');
        if (!live) return;
        live.textContent = '';
        window.setTimeout(function () { live.textContent = msg; }, 40);
    }

    function describe(key) {
        var v = prefs[key];
        switch (key) {
            case 'theme': return v === 'dark' ? 'Dark mode on' : 'Dark mode off';
            case 'contrast': return v === 'high' ? 'High contrast on' : 'High contrast off';
            case 'motion': return v === 'reduce' ? 'Reduced motion on' : 'Motion follows system';
            case 'underline': return v === 'on' ? 'Link underlines on' : 'Link underlines off';
            case 'readable': return v === 'on' ? 'Readable font on' : 'Readable font off';
            case 'fontScale': return 'Text size ' + Math.round(v * 100) + '%';
            default: return 'Preference updated';
        }
    }

    /* ---- Navbar theme toggle (light ⇄ dark) ---- */
    function cycleTheme() {
        set('theme', prefs.theme === 'dark' ? 'light' : 'dark');
    }

    function updateThemeToggle() {
        var dark = prefs.theme === 'dark';
        var icon = document.getElementById('themeIcon');
        var btn = document.getElementById('themeToggle');
        // The icon shows the state you would switch *to*, so it reads as an
        // action rather than a status indicator.
        if (icon) icon.className = 'fa-solid ' + (dark ? 'fa-sun' : 'fa-moon');
        if (btn) {
            // A named toggle button: the name stays fixed and aria-pressed carries
            // the state, so screen readers announce "Dark mode, toggle button,
            // pressed" instead of a label that renames itself on every click.
            btn.setAttribute('aria-label', 'Dark mode');
            btn.setAttribute('aria-pressed', dark ? 'true' : 'false');
        }
    }

    /* ---- Bind the panel controls generically from [data-pref] ---- */
    function syncControls() {
        document.querySelectorAll('[data-pref]').forEach(function (ctrl) {
            var key = ctrl.getAttribute('data-pref');
            if (!(key in prefs)) return;
            var spec = REGISTRY[key];
            if (ctrl.hasAttribute('data-value')) { // segmented button
                var active = String(prefs[key]) === ctrl.getAttribute('data-value');
                ctrl.classList.toggle('active', active);
                ctrl.setAttribute('aria-pressed', active ? 'true' : 'false');
            } else if (ctrl.type === 'checkbox') {
                ctrl.checked = prefs[key] === (spec && spec.toggleOn);
            } else if (ctrl.type === 'range') {
                ctrl.value = prefs[key];
            }
        });
        var pct = Math.round((prefs.fontScale || 1) * 100) + '%';
        document.querySelectorAll('.js-pref-fontscale-value').forEach(function (o) { o.textContent = pct; });
    }

    function wireControls() {
        // Segmented buttons: <button data-pref="theme" data-value="dark">
        document.querySelectorAll('[data-pref][data-value]').forEach(function (b) {
            b.addEventListener('click', function () { set(b.getAttribute('data-pref'), b.getAttribute('data-value')); });
        });
        // Inputs: checkboxes (switches) and ranges
        document.querySelectorAll('input[data-pref]').forEach(function (inp) {
            var key = inp.getAttribute('data-pref');
            var spec = REGISTRY[key] || {};
            if (inp.type === 'checkbox') {
                inp.addEventListener('change', function () {
                    set(key, inp.checked ? (spec.toggleOn || 'on') : (spec.values ? spec.values[0] : 'off'));
                });
            } else if (inp.type === 'range') {
                if (spec.min != null) inp.min = spec.min;
                if (spec.max != null) inp.max = spec.max;
                if (spec.step != null) inp.step = spec.step;
                inp.addEventListener('input', function () { set(key, inp.value); });
            }
        });
        document.querySelectorAll('.js-prefs-reset').forEach(function (btn) { btn.addEventListener('click', reset); });
        var themeBtn = document.getElementById('themeToggle');
        if (themeBtn) themeBtn.addEventListener('click', cycleTheme);
    }

    /* ---- Public API for future extensions ---- */
    window.SitePreferences = {
        get: function (key) { return key ? prefs[key] : getAll(); },
        set: set,
        reset: reset,
        defaults: defaults,
        registry: REGISTRY
    };

    document.addEventListener('DOMContentLoaded', function () {
        wireControls();
        applyAll(); // body now exists — guarantees dark-mode class + control sync
    });
})();
