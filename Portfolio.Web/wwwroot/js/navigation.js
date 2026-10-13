/**
 * navigation.js
 * Handles smooth scrolling, navbar height, and mobile menu
 */

document.addEventListener('DOMContentLoaded', function () {
    initSmoothScroll();
    initNavHeight();
    initMobileMenu();
});

function initSmoothScroll() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;

            // Skip dropdown toggles — they are handled by Bootstrap
            if (this.classList.contains('dropdown-toggle')) return;

            const target = document.querySelector(targetId);
            if (target) {
                e.preventDefault();
                const navHeight = document.querySelector('.navbar')?.offsetHeight || 0;
                const targetPosition = target.getBoundingClientRect().top + window.scrollY - navHeight;

                const reduceMotion = document.documentElement.dataset.motion === 'reduce';
                window.scrollTo({
                    top: targetPosition,
                    behavior: reduceMotion ? 'auto' : 'smooth'
                });

                // Move focus too, so keyboard and screen-reader users continue from
                // the target rather than from the link they activated (WCAG 2.4.3).
                let added = false;
                if (!target.hasAttribute('tabindex') &&
                    !target.matches('a,button,input,select,textarea,[tabindex]')) {
                    target.setAttribute('tabindex', '-1');
                    added = true;
                }
                target.focus({ preventScroll: true });
                if (added) target.addEventListener('blur', () => target.removeAttribute('tabindex'), { once: true });
                history.pushState(null, '', targetId);

                closeMobileMenu();
            }
        });
    });
}

/**
 * Publish the navbar height as --nav-h for full-height pages (network.css). Updates are
 * skipped while the mobile menu is open, so the page does not resize under it. That only
 * applies while the collapsed-menu layout is active (below Bootstrap's lg breakpoint, matching
 * navbar-expand-lg): `.show` stays on #navbarNav if the viewport widens past lg with the menu
 * open, and the toggler is then hidden, so the height must be published again.
 */
function initNavHeight() {
    const navbar = document.getElementById('mainNav');
    if (!navbar || typeof ResizeObserver === 'undefined') return;

    const menu = document.getElementById('navbarNav');
    const collapsedLayout = window.matchMedia('(max-width: 991.98px)');
    const update = () => {
        if (collapsedLayout.matches && menu &&
            (menu.classList.contains('show') || menu.classList.contains('collapsing'))) return;
        document.documentElement.style.setProperty('--nav-h', navbar.offsetHeight + 'px');
    };
    new ResizeObserver(update).observe(navbar);
    if (menu) menu.addEventListener('hidden.bs.collapse', update);
    collapsedLayout.addEventListener('change', update);
}

function initMobileMenu() {
    const navLinks = document.querySelectorAll('.navbar-nav .nav-link');
    navLinks.forEach(link => {
        // Dropdown toggles must not close the menu, or the profile dropdown could not open on mobile.
        if (link.classList.contains('dropdown-toggle')) return;

        link.addEventListener('click', closeMobileMenu);
    });

    // Close mobile menu when a dropdown *item* is clicked (e.g., "My Profile")
    const dropdownItems = document.querySelectorAll('.navbar-nav .dropdown-item');
    dropdownItems.forEach(item => {
        item.addEventListener('click', closeMobileMenu);
    });
}

function closeMobileMenu() {
    const navbarCollapse = document.getElementById('navbarNav');
    if (navbarCollapse && navbarCollapse.classList.contains('show')) {
        const bsCollapse = bootstrap.Collapse.getInstance(navbarCollapse);
        if (bsCollapse) {
            bsCollapse.hide();
        }
    }
}