/**
 * navigation.js
 * Handles smooth scrolling, sticky navbar, and mobile menu
 */

document.addEventListener('DOMContentLoaded', function () {
    initSmoothScroll();
    initStickyNavbar();
    initMobileMenu();
});

/**
 * Enable smooth scrolling for anchor links
 */
function initSmoothScroll() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;

            const target = document.querySelector(targetId);
            if (target) {
                e.preventDefault();
                const navHeight = document.querySelector('.navbar')?.offsetHeight || 0;
                const targetPosition = target.getBoundingClientRect().top + window.scrollY - navHeight;

                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });

                // Close mobile menu if open
                closeMobileMenu();
            }
        });
    });
}

/**
 * Add/remove class to navbar on scroll for styling
 */
function initStickyNavbar() {
    const navbar = document.getElementById('mainNav');
    if (!navbar) return;

    window.addEventListener('scroll', () => {
        if (window.scrollY > 100) {
            navbar.classList.add('navbar-scrolled');
        } else {
            navbar.classList.remove('navbar-scrolled');
        }
    });
}

/**
 * Handle mobile menu close on link click
 */
function initMobileMenu() {
    const navLinks = document.querySelectorAll('.navbar-nav .nav-link');
    navLinks.forEach(link => {
        link.addEventListener('click', closeMobileMenu);
    });
}

/**
 * Close mobile menu by removing show class
 */
function closeMobileMenu() {
    const navbarCollapse = document.getElementById('navbarNav');
    if (navbarCollapse && navbarCollapse.classList.contains('show')) {
        const bsCollapse = bootstrap.Collapse.getInstance(navbarCollapse);
        if (bsCollapse) {
            bsCollapse.hide();
        }
    }
}