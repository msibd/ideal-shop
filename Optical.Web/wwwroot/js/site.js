// Show / hide password.
//
// Delegated from the document so it works for any field marked up with a
// [data-password-toggle] button, on any page, without per-page wiring.
document.addEventListener('click', function (event) {
    const toggle = event.target.closest('[data-password-toggle]');
    if (!toggle) {
        return;
    }

    const field = toggle.closest('.input-group')?.querySelector('input');
    if (!field) {
        return;
    }

    const reveal = field.type === 'password';
    field.type = reveal ? 'text' : 'password';

    toggle.setAttribute('aria-pressed', String(reveal));
    toggle.setAttribute('aria-label', reveal ? 'Hide password' : 'Show password');

    const showIcon = toggle.querySelector('[data-icon="show"]');
    const hideIcon = toggle.querySelector('[data-icon="hide"]');
    if (showIcon && hideIcon) {
        showIcon.hidden = reveal;
        hideIcon.hidden = !reveal;
    }

    // Keep the caret where the user was typing.
    field.focus();
});

// Collapsible card sections.
//
// Bootstrap drives the open/close itself; this only remembers which sections
// the user closed, so a collapsed widget stays collapsed on the next visit.
// Storage is per browser and best-effort — a private window that refuses
// localStorage simply gets every section expanded.
(function () {
    const STORAGE_KEY = 'optical.collapsed';

    function readClosed() {
        try {
            const stored = JSON.parse(localStorage.getItem(STORAGE_KEY));
            return Array.isArray(stored) ? stored : [];
        } catch {
            return [];
        }
    }

    function writeClosed(keys) {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(keys));
        } catch {
            // Nothing to do: the section still collapses, it just will not be remembered.
        }
    }

    function setClosed(key, isClosed) {
        const keys = readClosed().filter(k => k !== key);
        if (isClosed) {
            keys.push(key);
        }
        writeClosed(keys);
    }

    // Restore before paint where possible, so a closed section does not flash open.
    document.addEventListener('DOMContentLoaded', function () {
        for (const key of readClosed()) {
            const section = document.getElementById(key);
            const toggle = document.querySelector(`[data-collapse-key="${CSS.escape(key)}"]`);

            if (section && toggle) {
                section.classList.remove('show');
                toggle.setAttribute('aria-expanded', 'false');
            }
        }
    });

    // Bootstrap's collapse events bubble, so one listener covers every section.
    document.addEventListener('hidden.bs.collapse', e => setClosed(e.target.id, true));
    document.addEventListener('shown.bs.collapse', e => setClosed(e.target.id, false));
})();
