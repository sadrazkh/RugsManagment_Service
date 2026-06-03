---
name: Heritage Loom
colors:
  surface: '#fbf9f5'
  surface-dim: '#dbdad6'
  surface-bright: '#fbf9f5'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f5f3ef'
  surface-container: '#efeeea'
  surface-container-high: '#eae8e4'
  surface-container-highest: '#e4e2de'
  on-surface: '#1b1c1a'
  on-surface-variant: '#58413f'
  inverse-surface: '#30312e'
  inverse-on-surface: '#f2f0ed'
  outline: '#8c716e'
  outline-variant: '#e0bfbc'
  surface-tint: '#ac332f'
  primary: '#6b0008'
  on-primary: '#ffffff'
  primary-container: '#8c1b1b'
  on-primary-container: '#ff9c93'
  inverse-primary: '#ffb3ac'
  secondary: '#775a19'
  on-secondary: '#ffffff'
  secondary-container: '#fed488'
  on-secondary-container: '#785a1a'
  tertiary: '#273342'
  on-tertiary: '#ffffff'
  tertiary-container: '#3d4959'
  on-tertiary-container: '#acb8cb'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#ffdad6'
  primary-fixed-dim: '#ffb3ac'
  on-primary-fixed: '#410003'
  on-primary-fixed-variant: '#8a1a1a'
  secondary-fixed: '#ffdea5'
  secondary-fixed-dim: '#e9c176'
  on-secondary-fixed: '#261900'
  on-secondary-fixed-variant: '#5d4201'
  tertiary-fixed: '#d7e3f8'
  tertiary-fixed-dim: '#bbc7db'
  on-tertiary-fixed: '#101c2b'
  on-tertiary-fixed-variant: '#3c4858'
  background: '#fbf9f5'
  on-background: '#1b1c1a'
  surface-variant: '#e4e2de'
typography:
  display-lg:
    fontFamily: beVietnamPro
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: beVietnamPro
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
  headline-lg-mobile:
    fontFamily: beVietnamPro
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  title-md:
    fontFamily: beVietnamPro
    fontSize: 20px
    fontWeight: '500'
    lineHeight: 28px
  body-lg:
    fontFamily: ibmPlexSans
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: ibmPlexSans
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  label-sm:
    fontFamily: jetbrainsMono
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0.05em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  base: 4px
  xs: 8px
  sm: 16px
  md: 24px
  lg: 40px
  xl: 64px
  container-max: 1440px
  gutter: 24px
---

## Brand & Style

The visual identity of the design system bridges the gap between centuries-old artisanal tradition and the precision of modern inventory management. The design narrative is one of **"Digital Craftsmanship,"** where the software feels as intentional and structured as the weave of a Tabriz or Isfahan carpet.

The style is **Modern Corporate with Heritage Accents**. It utilizes a clean, card-based layout to organize complex data, while employing a sophisticated, high-contrast color palette inspired by natural dyes. Every element aims to evoke reliability and luxury, ensuring that high-value assets are managed in an environment that reflects their worth.

**Key Principles:**
- **Symmetry & Order:** Layouts follow a rigorous grid, mirroring the architectural balance of carpet patterns.
- **Materiality:** Soft ivory surfaces provide a warm, organic alternative to sterile white interfaces.
- **Clarity:** Despite the data-heavy nature of the system, generous whitespace and clear hierarchy prevent cognitive overload.

## Colors

The palette is rooted in the "Persian Red" tradition, using deep madder-root hues for primary actions and royal status. 

- **Primary (Royal Red):** Used for critical actions, branding, and active navigational states. It represents the "soul" of the carpet.
- **Secondary (Heritage Gold):** Reserved for status indicators, badges, and premium markers. It signifies value and quality assurance.
- **Neutral (Ivory & Charcoal):** The Ivory background (#FDFBF7) reduces eye strain compared to pure white, creating a more tactile, parchment-like feel. Dark Charcoal (#1A1A1A) ensures maximum legibility for bilingual text.
- **Functional Tones:** Success states use a muted cypress green, while warnings utilize a burnt orange to stay within the warm, traditional spectrum.

## Typography

This design system uses a dual-font strategy to accommodate English and Persian (Persian implementation should use **Vazirmatn** or **IRANSans** for parity).

- **Headlines (Be Vietnam Pro):** A modern, geometric sans-serif that feels clean and authoritative. It handles large-scale numbers and titles with professional elegance.
- **Body (IBM Plex Sans):** Chosen for its technical precision and readability in dense data environments. It provides a "workhorse" feel for inventory lists and specifications.
- **Data Labels (JetBrains Mono):** Used for SKU numbers, dimensions, and technical specifications to emphasize the "management" aspect of the system.

**Bilingual Handling:**
When switching to Persian (RTL), line heights should be increased by 20% to accommodate the script's ascenders and descenders. Text alignment must flip globally.

## Layout & Spacing

The system uses a **12-column fluid grid** for desktop and a **4-column grid** for mobile. 

- **Grid Logic:** On desktop, the sidebar is fixed at 280px, while the content area expands. Cards should span 3, 4, 6, or 12 columns depending on content density.
- **Rhythm:** A 4px baseline grid ensures vertical consistency. Standard component padding is set to `md` (24px) to maintain a spacious, premium feel.
- **Responsiveness:**
  - **Desktop (1024px+):** Full visibility of tables and side-navigation.
  - **Tablet (768px - 1023px):** Sidebar collapses into a hamburger menu; tables become horizontally scrollable cards.
  - **Mobile (<767px):** Single column layout. Large headlines scale down to `headline-lg-mobile`. Margins reduce to 16px.

## Elevation & Depth

To maintain the "Modern Enterprise" feel, the system avoids heavy shadows in favor of **Tonal Layers** and **Refined Outlines**.

- **Level 0 (Background):** Surface Ivory (#F9F5F1).
- **Level 1 (Cards/Containers):** Pure White (#FFFFFF) with a 1px border in a soft muted gold or light grey (#E0D7CD).
- **Level 2 (Hover/Active):** A very soft, diffused shadow (0px 4px 20px rgba(26, 26, 26, 0.05)) to indicate interactivity without breaking the flat aesthetic.
- **Image Depth:** High-quality carpet photography should use a subtle inner stroke to simulate a "frame," making the images pop against the ivory background.

## Shapes

The shape language is **Soft (0.25rem / 4px)**. 

While the system is professional, sharp corners are avoided to keep the interface approachable. Small radii are used for buttons, input fields, and small cards. 

- **Standard Radius:** 4px (Soft).
- **Large Radius:** 8px for main dashboard containers and modal windows.
- **Interactive Elements:** Buttons maintain a structured rectangular form with minimal rounding to preserve the "architectural" feel of the system.

## Components

### Buttons & Controls
- **Primary Button:** Deep Royal Red background with White text. No gradient. High-contrast hover state (slightly darker red).
- **Secondary Button:** Clear background with a 1.5px Royal Red border.
- **Tertiary/Ghost:** Charcoal text with no border; used for "Cancel" or low-priority actions.

### Inventory Cards
Cards display a high-resolution carpet thumbnail on the left (or top on mobile), with key metadata (Origin, Knot Density, SKU) on the right. Use the **Heritage Gold** for the "Certified" badge icon.

### Data Tables
- **Header:** Dark Charcoal background with White label-sm text.
- **Rows:** Alternating subtle Ivory and White stripes. 
- **Cell Content:** Use `jetbrainsMono` for numeric data like price and dimensions to ensure column alignment.

### Progress Tracking
A horizontal "Stepper" component for carpet restoration or shipping phases. Active steps are indicated with a Gold solid circle; completed steps with a Red checkmark; and pending steps with a light grey outline.

### Image Placeholders
Large, dashed-border areas using a custom "woven" pattern icon in the center to encourage high-quality asset uploads.