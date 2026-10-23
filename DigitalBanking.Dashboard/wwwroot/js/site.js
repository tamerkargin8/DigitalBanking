(() => {
    const shell = document.getElementById('app-shell');
    const toggle = document.querySelector('[data-nav-toggle]');
    const nav = document.getElementById('app-nav');
    if (!shell || !toggle || !nav) return;

    const setOpen = (open) => {
        shell.classList.toggle('nav-open', open);
        toggle.setAttribute('aria-expanded', String(open));
        toggle.setAttribute('aria-label', open ? 'Menüyü kapat' : 'Menüyü aç');
    };

    toggle.addEventListener('click', () => setOpen(!shell.classList.contains('nav-open')));
    nav.addEventListener('click', (event) => {
        if (event.target.closest('a')) setOpen(false);
    });
    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') setOpen(false);
    });
    window.matchMedia('(min-width: 961px)').addEventListener('change', (event) => {
        if (event.matches) setOpen(false);
    });
})();
