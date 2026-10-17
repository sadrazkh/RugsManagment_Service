# Theme refinement — 2026-10-18

The dark theme used near-black brown surfaces and bright salmon accents. It now uses layered ink/charcoal surfaces, warm ivory text, muted copper primary actions and restrained gold/teal accents. Semantic text, chart series, aging colors, autofill and scrollbars follow the dark palette; light-theme brand colors remain intact.

Two small geometric Persian rug motifs reuse the existing SVG sprite: a low-opacity medallion in the Home greeting and a narrow diamond border below the sidebar brand. The rug icon gains a central lozenge and fringes. Decorations are aria-hidden, do not accept pointer events and are omitted from print. No ornament is repeated behind forms or tables; there is no animation, bitmap or new dependency.

The PWA static cache version advances to v6 so existing installations receive the updated sprite.

Verification:

- Full frontend suite566/566; strict Vue typecheck and production build pass.
- Release Web/Razor build: zero warnings/errors.
- Existing PWA/source unit checks9/9 pass. SVG parses as valid XML.
- Dark token calculations: normal/muted text minimum8.86/6.17:1 across the six surfaces; semantic/action text minimum4.94:1; chart series minimum3.45:1; foreground/action-fill pairs minimum7.90:1.
- CUA checked Home, workflow cards and the cost-entry dialog in dark mode, and Home in light mode. Default394px and1024px desktop layout have no document horizontal overflow; the decorative medallion stays away from text. Temporary viewport override was reset.
- Relative to the previous build, main CSS adds440 compressed bytes and the existing sprite adds437 compressed bytes (877 bytes combined, gzip). No additional asset request is needed for the motifs.
- Preview uses a separate disposable `rugs_theme_ui_1018` database. Owner data is untouched. Screenshots are in the task outputs directory.
