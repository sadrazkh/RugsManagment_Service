document.getElementById('document-print')?.addEventListener('click', () => window.print());
document.getElementById('document-back')?.addEventListener('click', event => { event.preventDefault(); history.back(); });
if (document.body.dataset.autoprint === 'true') window.addEventListener('load', () => { document.fonts.ready.then(() => window.print()); });
