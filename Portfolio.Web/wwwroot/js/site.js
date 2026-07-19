/**
 * site.js - Main entry point
 *
 * Theme and accessibility handling moved to preferences.js, which is the
 * single source of truth for the light/dark/system theme, text scaling,
 * contrast, motion, and other user-controllable display preferences.
 * It still emits the `themeChanged` event (detail: { dark, theme }) that
 * other scripts can listen for, and mirrors the legacy localStorage["theme"]
 * value for backward compatibility.
 *
 * Add general site-wide behavior here.
 */
