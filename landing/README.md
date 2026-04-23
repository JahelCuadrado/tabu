# Tabu landing page

A static, single-page landing built with Tailwind (Play CDN) and vanilla JS.
Deployed to GitHub Pages by `.github/workflows/deploy-landing.yml` whenever
files in this folder change on `main`.

## Local preview

Open `index.html` directly in a browser, or serve it for nicer caching:

```powershell
cd landing
python -m http.server 5173
# then open http://localhost:5173
```

## Files

- `index.html` — markup, sections, and Tailwind config.
- `styles.css` — small custom layer on top of Tailwind utilities.
- `script.js` — theme toggle, EN/ES i18n, GitHub Releases API hydration.

## Notes

- Versions, asset filenames and sizes shown on the page are hydrated at runtime
  from `https://api.github.com/repos/JahelCuadrado/Tabu/releases/latest`.
  Static fallbacks are baked in so the page is fully readable offline / when
  the API rate limit is hit.
- No build step. No dependencies to install. No telemetry.
