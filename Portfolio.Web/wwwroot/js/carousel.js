/**
 * carousel.js
 * Home-page project carousel: a pause control (WCAG 2.2.2) and a manual,
 * scroll-snap mode (2.1.1, 2.4.11) used while paused, under reduced motion,
 * or while a card has keyboard focus. The auto-scroll itself is pure CSS
 * (layout.css, @keyframes scrollProjects); this only toggles `.is-manual`.
 */
(function () {
    'use strict';

    var slider = document.getElementById('projectsSlider');
    var button = document.getElementById('projectsPause');
    if (!slider) return;

    var root = document.documentElement;
    var paused = false;
    var focused = false;

    function reduced() {
        return root.dataset.motion === 'reduce';
    }

    // Manual mode whenever the marquee should not be moving.
    function sync() {
        slider.classList.toggle('is-manual', paused || focused || reduced());
        if (button) {
            // Nothing to pause when reduced motion already stopped it.
            button.hidden = reduced();
            button.setAttribute('aria-pressed', paused ? 'true' : 'false');
            var icon = button.querySelector('i');
            if (icon) {
                icon.classList.toggle('fa-pause', !paused);
                icon.classList.toggle('fa-play', paused);
            }
        }
    }

    if (button) {
        button.addEventListener('click', function () {
            paused = !paused;
            sync();
        });
    }

    // Keyboard focus into a card: stop the track, then bring the card into view.
    slider.addEventListener('focusin', function (e) {
        var el = e.target;
        if (!(el instanceof Element) || !el.matches(':focus-visible')) return;
        focused = true;
        sync();
        var card = el.closest('.project-card');
        if (card) card.scrollIntoView({ inline: 'nearest', block: 'nearest' });
    });

    slider.addEventListener('focusout', function (e) {
        if (e.relatedTarget && slider.contains(e.relatedTarget)) return;
        focused = false;
        sync();
    });

    // Follow the Reduce motion preference live (preferences.js sets data-motion).
    new MutationObserver(sync).observe(root, { attributes: true, attributeFilter: ['data-motion'] });

    sync();
})();
