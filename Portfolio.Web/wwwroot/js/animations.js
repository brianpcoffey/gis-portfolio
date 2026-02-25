/**
 * animations.js
 * Handles scroll-triggered animations and hover effects
 */

document.addEventListener('DOMContentLoaded', function () {
    initScrollAnimations();
    initProjectCardEffects();
});

/**
 * Initialize fade-in animations on scroll
 */
function initScrollAnimations() {
    // Add fade-in class to animatable elements
    const animatableSelectors = [
        '.skill-card',
        '.project-card',
        '.about-card',
        '.contact-link'
    ];

    animatableSelectors.forEach(selector => {
        document.querySelectorAll(selector).forEach(el => {
            el.classList.add('fade-in');
        });
    });

    // Create Intersection Observer
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
                observer.unobserve(entry.target);
            }
        });
    }, {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    });

    // Observe all fade-in elements
    document.querySelectorAll('.fade-in').forEach(el => {
        observer.observe(el);
    });
}

/**
 * Add interactive hover effects to project cards
 */
function initProjectCardEffects() {
    const projectCards = document.querySelectorAll('.project-card');

    projectCards.forEach(card => {
        card.addEventListener('mouseenter', function () {
            this.style.setProperty('--hover-scale', '1.02');
        });

        card.addEventListener('mouseleave', function () {
            this.style.setProperty('--hover-scale', '1');
        });

        // Add subtle tilt effect on mouse move
        card.addEventListener('mousemove', function (e) {
            const rect = this.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;
            const centerX = rect.width / 2;
            const centerY = rect.height / 2;

            const rotateX = (y - centerY) / 20;
            const rotateY = (centerX - x) / 20;

            this.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) translateY(-10px) scale(1.02)`;
        });

        card.addEventListener('mouseleave', function () {
            this.style.transform = '';
        });
    });
}