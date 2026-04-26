/**
 * animations.js
 * Handles scroll-triggered fade-in animations with stagger support
 */

document.addEventListener('DOMContentLoaded', function () {
    initScrollAnimations();
});

/**
 * Initialize fade-in animations on scroll.
 * Elements sharing the same direct parent are staggered in sequence.
 */
function initScrollAnimations() {
    const animatableSelectors = [
        '.skill-card',
        '.about-card',
        '.contact-link'
    ];

    animatableSelectors.forEach(selector => {
        // Group siblings so stagger delay is relative within each parent
        const groups = new Map();
        document.querySelectorAll(selector).forEach(el => {
            const parent = el.parentElement;
            if (!groups.has(parent)) groups.set(parent, []);
            groups.get(parent).push(el);
        });

        groups.forEach(siblings => {
            siblings.forEach((el, i) => {
                el.classList.add('fade-in');
                el.style.transitionDelay = `${i * 80}ms`;
            });
        });
    });

    // Also stagger project cards on the all-projects grid page (not the carousel)
    const projectGrid = document.querySelector('.projects-grid');
    if (projectGrid) {
        projectGrid.querySelectorAll('.project-card').forEach((el, i) => {
            el.classList.add('fade-in');
            el.style.transitionDelay = `${i * 60}ms`;
        });
    }

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
                observer.unobserve(entry.target);
            }
        });
    }, {
        threshold: 0.08,
        rootMargin: '0px 0px -40px 0px'
    });

    document.querySelectorAll('.fade-in').forEach(el => {
        observer.observe(el);
    });
}