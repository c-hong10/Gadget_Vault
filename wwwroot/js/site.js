// =====================================================
// GadgetVault — site.js
// Global client-side logic for the Dashboard shell
// =====================================================

// ─────────────────────────────────────────────────────
// THEME ENGINE
// Key: 'theme' | Values: 'dark' | 'light'
// Default: 'dark' (enterprise dashboard defaults dark)
// ─────────────────────────────────────────────────────

const DARK_COLORS = {
    body:   '#09090b',
    main:   '#0c0c0e',
    text:   '#e2e8f0',
};

const LIGHT_COLORS = {
    body:   '#f8fafc',
    main:   '#f1f5f9',
    text:   '#0f172a',
};

function applyTheme(theme) {
    const html   = document.documentElement;
    const body   = document.body;
    const main   = document.getElementById('page-main');
    const moon   = document.getElementById('icon-moon');
    const sun    = document.getElementById('icon-sun');

    if (theme === 'dark') {
        html.classList.add('dark');

        // Apply dark palette via JS (works for both inline-style and Tailwind dark: elements)
        body.style.backgroundColor = DARK_COLORS.body;
        body.style.color           = DARK_COLORS.text;
        if (main) {
            main.style.backgroundColor = DARK_COLORS.main;
            main.style.color           = DARK_COLORS.text;
        }

        // Icon: dark mode shows the Moon (you'll click to go light)
        if (moon) moon.style.display = 'block';
        if (sun)  sun.style.display  = 'none';

    } else {
        html.classList.remove('dark');

        // Apply light palette
        body.style.backgroundColor = LIGHT_COLORS.body;
        body.style.color           = LIGHT_COLORS.text;
        if (main) {
            main.style.backgroundColor = LIGHT_COLORS.main;
            main.style.color           = LIGHT_COLORS.text;
        }

        // Icon: light mode shows the Sun (you'll click to go dark)
        if (moon) moon.style.display = 'none';
        if (sun)  sun.style.display  = 'block';
    }
}

// Called by the toggle button (onclick="toggleTheme()")
function toggleTheme() {
    var current = localStorage.getItem('theme') || 'dark';
    var next    = (current === 'dark') ? 'light' : 'dark';
    localStorage.setItem('theme', next);
    applyTheme(next);
}

// Auto-run on page load after DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    var saved = localStorage.getItem('theme') || 'dark';
    applyTheme(saved);
});

// ─────────────────────────────────────────────────────
// GLOBAL CONFIRMATION MODAL
// Usage: showConfirm('Title', 'Message text', callbackFn)
// ─────────────────────────────────────────────────────

function showConfirm(title, text, onConfirm) {
    var titleEl = document.getElementById('confirmModalTitle');
    var textEl  = document.getElementById('confirmModalText');
    var modal   = document.getElementById('globalConfirmModal');
    var inner   = document.getElementById('confirmModalInner');
    var btn     = document.getElementById('confirmModalBtn');

    if (titleEl) titleEl.innerText = title;
    if (textEl)  textEl.innerText  = text;

    if (modal) modal.style.display = 'flex';
    if (inner) setTimeout(function () { inner.style.transform = 'scale(1)'; }, 10);

    if (btn) {
        btn.onclick = function () {
            closeConfirm();
            if (typeof onConfirm === 'function') onConfirm();
        };
    }
}

function closeConfirm() {
    var modal = document.getElementById('globalConfirmModal');
    var inner = document.getElementById('confirmModalInner');
    if (inner) inner.style.transform = 'scale(0.95)';
    setTimeout(function () {
        if (modal) modal.style.display = 'none';
    }, 180);
}
