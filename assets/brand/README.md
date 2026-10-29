# SQuilL brand assets

Generated 2026-10-29 from the SQuilL design canvas (kit `SQuilL-assets.zip`).

- `logo/svg/`: symbol (color on dark/light, simplified 16-24 px version, mono black/white), horizontal logo, stacked logo, wordmark. Text is outlined, no font required.
- `logo/png/`: symbol from 16 to 1024 px on light and dark (transparent background), logos at 64 and 256 px high.
- `app-icons/windows/`: `squill.ico` (16-256 px), PNG per size, SVG source.
- `app-icons/macos/`: `squill.icns`, `SQuilL.iconset`, 1024 SVG on the 824/1024 template.
- `app-icons/linux/`: `hicolor` tree (SVG + PNG 16-512) and `squill.desktop`.
- `favicon/`: `favicon.svg` (follows the light/dark theme), `favicon.ico`, `apple-touch-icon.png`, `<head>` snippet.

## Usage rules

- Colors: petals #9488F5 (dark background) / #5B4CD6 (light background); core #E8E9EE / #16171C.
- Below 32 px, use the simplified symbol (oval petals, no core).
- Clear space: 1/4 of the symbol height on all four sides. Minimum size: 16 px symbol, 72 px wide wordmark.
- Forbidden: distorting, rotating, gradients, drop shadows, multicolored petals, changing the petal count, environment colors (red/orange/green) on the logo.
- Wordmark: Inter Tight (SIL OFL), weight 600 for S, Q and L, 500 for "uil", tracking -3%.

Design tokens live in `tokens/squill.tokens.json`, not here.
