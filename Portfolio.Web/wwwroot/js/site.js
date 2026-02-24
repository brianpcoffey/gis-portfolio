// Global dark/light mode toggle for all pages

document.addEventListener('DOMContentLoaded', function () {
    const themeToggle = document.getElementById('themeToggle');
    const themeIcon = document.getElementById('themeIcon');

    function setTheme(dark) {
        const body = document.body;
        if (dark) {
            body.classList.add('dark-mode');
            themeIcon.classList.remove('fa-sun', 'fa-solid', 'text-warning');
            themeIcon.classList.add('fa-moon', 'fa-regular', 'text-light');
            themeToggle.classList.remove('btn-outline-light');
            themeToggle.classList.add('btn-outline-secondary');
        } else {
            body.classList.remove('dark-mode');
            themeIcon.classList.remove('fa-moon', 'fa-regular', 'text-light');
            themeIcon.classList.add('fa-sun', 'fa-solid', 'text-warning');
            themeToggle.classList.remove('btn-outline-secondary');
            themeToggle.classList.add('btn-outline-light');
        }
    }

    if (themeToggle && themeIcon) {
        themeToggle.addEventListener('click', () => {
            const dark = !document.body.classList.contains('dark-mode');
            setTheme(dark);
            localStorage.setItem('theme', dark ? 'dark' : 'light');
        });

        setTheme(localStorage.getItem('theme') === 'dark');
    }
});
