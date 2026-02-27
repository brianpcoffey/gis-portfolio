/**
 * site.js - Main entry point
 * Handles theme toggle with localStorage persistence and emits a themeChanged event
 */

document.addEventListener('DOMContentLoaded', function () {
    initThemeToggle();
});

/**
 * Initialize dark/light mode toggle
 */
function initThemeToggle() {
    const themeToggle = document.getElementById('themeToggle');
    const themeIcon = document.getElementById('themeIcon');

    if (!themeToggle || !themeIcon) return;

    // Set theme based on stored preference or system preference
    const storedTheme = localStorage.getItem('theme');
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const isDark = storedTheme === 'dark' || (!storedTheme && prefersDark);

    setTheme(isDark);

    // Toggle on click
    themeToggle.addEventListener('click', () => {
        const dark = !document.body.classList.contains('dark-mode');
        setTheme(dark);
        localStorage.setItem('theme', dark ? 'dark' : 'light');

        // Emit a themeChanged event so other scripts can adjust if required
        window.dispatchEvent(new CustomEvent('themeChanged', { detail: { dark } }));
    });

    /**
     * Apply theme to document
     * @param {boolean} dark - Whether to apply dark mode
     */
    function setTheme(dark) {
        if (dark) {
            document.body.classList.add('dark-mode');
            themeIcon.classList.remove('fa-moon');
            themeIcon.classList.add('fa-sun');
        } else {
            document.body.classList.remove('dark-mode');
            themeIcon.classList.remove('fa-sun');
            themeIcon.classList.add('fa-moon');
        }
    }
}
