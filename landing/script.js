/* Tabu landing — theme toggle, i18n, and dynamic GitHub Releases data */

(() => {
  "use strict";

  const REPO = "JahelCuadrado/tabu";
  const STORAGE_KEY_THEME = "tabu.theme";
  const STORAGE_KEY_LANG = "tabu.lang";

  // ---------- THEME ----------
  const root = document.documentElement;
  const themeBtn = document.getElementById("theme-toggle");

  const getInitialTheme = () => {
    const saved = localStorage.getItem(STORAGE_KEY_THEME);
    if (saved === "dark" || saved === "light") return saved;
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  };

  const applyTheme = (theme) => {
    root.classList.toggle("dark", theme === "dark");
    localStorage.setItem(STORAGE_KEY_THEME, theme);
    document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute("content", theme === "dark" ? "#08080f" : "#ffffff");
  };

  applyTheme(getInitialTheme());
  themeBtn?.addEventListener("click", () => {
    applyTheme(root.classList.contains("dark") ? "light" : "dark");
  });

  // ---------- I18N ----------
  const translations = {
    en: {
      "nav.features": "Features",
      "nav.download": "Download",
      "nav.faq": "FAQ",
      "nav.cta": "Get Tabu",
      "hero.badge": "New release available",
      "hero.title1": "A faster way to switch",
      "hero.title2": "windows on Windows.",
      "hero.subtitle":
        "Tabu docks a clean tab bar at the top of every monitor so you can flip between apps like browser tabs. Multi-monitor, multilingual, and disappears the moment you don't need it.",
      "hero.cta": "Download for Windows",
      "hero.requirements":
        "Windows 10/11 · 64-bit · No .NET install required · Free & open source",
      "hero.preview.label": "Live preview",
      "hero.preview.title": "Your windows, neatly arranged at the top of every screen.",
      "hero.preview.subtitle": "Click to switch · Middle-click to close · Auto-hide optional",
      "features.eyebrow": "What's inside",
      "features.title": "Built for people with too many windows.",
      "features.subtitle": "Lightweight, native, and quietly powerful. No bloat, no telemetry.",
      "features.f1.title": "Multi-monitor by design",
      "features.f1.desc":
        "A docked AppBar appears on every screen and reserves real estate so other windows never overlap it.",
      "features.f2.title": "One-click window switching",
      "features.f2.desc":
        "All your top-level windows shown as tabs, grouped per monitor. Click to focus, middle-click to close.",
      "features.f3.title": "Auto-hide that stays out of the way",
      "features.f3.desc":
        "Hide the bar until you need it. Reveals when you point to the top edge — like macOS, but better.",
      "features.f4.title": "Themes & accent colors",
      "features.f4.desc":
        "Light or dark, with custom accent. Matches your taste, not the other way around.",
      "features.f5.title": "10 languages out of the box",
      "features.f5.desc":
        "English, Spanish, French, German, Italian, Portuguese, Russian, Japanese, Chinese and Korean.",
      "features.f6.title": "Launch at startup, optional",
      "features.f6.desc":
        "Toggle from Settings. Stored per-user — no admin rights, no surprises, no telemetry.",
      "download.eyebrow": "Get Tabu",
      "download.title": "Choose how you want to run it.",
      "download.subtitle":
        "Both downloads are free, signed with your trust, and self-contained — zero dependencies.",
      "download.installer.title": "Windows Installer",
      "download.installer.desc":
        "One-click setup with desktop icon and optional autostart. Per-user install, no admin needed.",
      "download.portable.title": "Portable .zip",
      "download.portable.desc":
        "Single-file executable. Unzip and run from any folder or USB drive. No install, no traces.",
      "download.recommended": "Recommended",
      "download.smartscreen":
        "On first launch Windows SmartScreen may show a warning because the binary isn't code-signed yet. Click <em>More info → Run anyway</em>.",
      "faq.eyebrow": "FAQ",
      "faq.title": "Frequently asked questions",
      "faq.q1": "Is Tabu really free?",
      "faq.a1":
        "Yes. Tabu is open source under the GPL-3.0 license. No ads, no telemetry, no premium tier.",
      "faq.q2": "Does it work on Windows 11?",
      "faq.a2": "Yes. Tabu requires Windows 10 1809 (build 17763) or newer, on x64.",
      "faq.q3": "Why does Windows show a SmartScreen warning?",
      "faq.a3":
        "The binary is not code-signed yet. Click <em>More info → Run anyway</em>. Once enough people download it, SmartScreen will stop warning.",
      "faq.q4": "Where are settings stored?",
      "faq.a4": "In <code>%LocalAppData%\\Tabu\\settings.json</code>. Removing that file resets Tabu.",
      "faq.q5": "Can I contribute?",
      "faq.a5":
        'Absolutely. PRs and issues are welcome on <a class="link" href="https://github.com/JahelCuadrado/tabu" target="_blank" rel="noopener">GitHub</a>.',
      "footer.issues": "Report an issue",
    },
    es: {
      "nav.features": "Características",
      "nav.download": "Descargar",
      "nav.faq": "FAQ",
      "nav.cta": "Obtener Tabu",
      "hero.badge": "Nueva versión disponible",
      "hero.title1": "Una forma más rápida de",
      "hero.title2": "cambiar entre ventanas.",
      "hero.subtitle":
        "Tabu ancla una barra de pestañas en la parte superior de cada monitor para que cambies entre apps como si fueran pestañas. Multi-monitor, multilingüe y se aparta sola cuando no la necesitas.",
      "hero.cta": "Descargar para Windows",
      "hero.requirements":
        "Windows 10/11 · 64-bit · Sin instalar .NET · Gratis y de código abierto",
      "hero.preview.label": "Vista previa",
      "hero.preview.title": "Tus ventanas, ordenadas en la parte superior de cada pantalla.",
      "hero.preview.subtitle":
        "Clic para cambiar · Clic central para cerrar · Auto-ocultar opcional",
      "features.eyebrow": "Qué incluye",
      "features.title": "Hecho para quienes tienen demasiadas ventanas.",
      "features.subtitle":
        "Ligero, nativo y silenciosamente potente. Sin bloatware, sin telemetría.",
      "features.f1.title": "Multi-monitor de serie",
      "features.f1.desc":
        "Una AppBar anclada aparece en cada pantalla y reserva el espacio para que ninguna ventana la tape.",
      "features.f2.title": "Cambia de ventana con un clic",
      "features.f2.desc":
        "Todas tus ventanas como pestañas, agrupadas por monitor. Clic para enfocar, clic central para cerrar.",
      "features.f3.title": "Auto-ocultar discreto",
      "features.f3.desc":
        "Oculta la barra hasta que la necesites. Aparece al apuntar al borde superior — como macOS, pero mejor.",
      "features.f4.title": "Temas y colores de acento",
      "features.f4.desc":
        "Claro u oscuro, con acento personalizable. Se adapta a ti, no al revés.",
      "features.f5.title": "10 idiomas listos para usar",
      "features.f5.desc":
        "Inglés, español, francés, alemán, italiano, portugués, ruso, japonés, chino y coreano.",
      "features.f6.title": "Inicio con Windows, opcional",
      "features.f6.desc":
        "Actívalo en Ajustes. Se guarda por usuario — sin permisos de admin, sin sorpresas, sin telemetría.",
      "download.eyebrow": "Descargar Tabu",
      "download.title": "Elige cómo quieres ejecutarlo.",
      "download.subtitle":
        "Ambas descargas son gratis, autocontenidas y sin dependencias.",
      "download.installer.title": "Instalador de Windows",
      "download.installer.desc":
        "Instalación de un clic con icono de escritorio y arranque opcional con Windows. Por usuario, sin admin.",
      "download.portable.title": "Portable .zip",
      "download.portable.desc":
        "Ejecutable único. Descomprime y ejecuta desde cualquier carpeta o USB. Sin instalación, sin rastros.",
      "download.recommended": "Recomendado",
      "download.smartscreen":
        "Al abrirlo por primera vez, SmartScreen puede mostrar una advertencia porque el binario aún no está firmado. Haz clic en <em>Más información → Ejecutar de todas formas</em>.",
      "faq.eyebrow": "FAQ",
      "faq.title": "Preguntas frecuentes",
      "faq.q1": "¿Tabu es realmente gratis?",
      "faq.a1":
        "Sí. Tabu es open source bajo licencia GPL-3.0. Sin anuncios, sin telemetría, sin versiones premium.",
      "faq.q2": "¿Funciona en Windows 11?",
      "faq.a2": "Sí. Requiere Windows 10 1809 (build 17763) o superior, en x64.",
      "faq.q3": "¿Por qué Windows muestra una advertencia de SmartScreen?",
      "faq.a3":
        "El binario aún no está firmado. Haz clic en <em>Más información → Ejecutar de todas formas</em>. Cuando suficientes personas lo descarguen, dejará de avisar.",
      "faq.q4": "¿Dónde se guardan los ajustes?",
      "faq.a4":
        "En <code>%LocalAppData%\\Tabu\\settings.json</code>. Borrar ese archivo restablece Tabu.",
      "faq.q5": "¿Puedo contribuir?",
      "faq.a5":
        'Por supuesto. PRs e issues son bienvenidos en <a class="link" href="https://github.com/JahelCuadrado/tabu" target="_blank" rel="noopener">GitHub</a>.',
      "footer.issues": "Reportar un problema",
    },
  };

  const langButtons = document.querySelectorAll(".lang-btn");

  const applyLang = (lang) => {
    const dict = translations[lang] || translations.en;
    document.documentElement.lang = lang;
    document.querySelectorAll("[data-i18n]").forEach((el) => {
      const key = el.getAttribute("data-i18n");
      if (dict[key]) el.innerHTML = dict[key];
    });
    langButtons.forEach((b) => b.classList.toggle("is-active", b.dataset.lang === lang));
    localStorage.setItem(STORAGE_KEY_LANG, lang);
  };

  const getInitialLang = () => {
    const saved = localStorage.getItem(STORAGE_KEY_LANG);
    if (saved && translations[saved]) return saved;
    return (navigator.language || "en").toLowerCase().startsWith("es") ? "es" : "en";
  };

  langButtons.forEach((b) => b.addEventListener("click", () => applyLang(b.dataset.lang)));
  applyLang(getInitialLang());

  // ---------- DYNAMIC YEAR ----------
  document.getElementById("year").textContent = new Date().getFullYear();

  // ---------- GITHUB RELEASES ----------
  const formatBytes = (bytes) => {
    if (!Number.isFinite(bytes)) return "";
    const mb = bytes / (1024 * 1024);
    return mb >= 100 ? `${mb.toFixed(0)} MB` : `${mb.toFixed(1)} MB`;
  };

  const updateAsset = (asset, nameEl, sizeEl, linkEl) => {
    if (!asset) return;
    if (nameEl) nameEl.textContent = asset.name;
    if (sizeEl) sizeEl.textContent = formatBytes(asset.size);
    if (linkEl) linkEl.href = asset.browser_download_url;
  };

  const fetchLatestRelease = async () => {
    try {
      const res = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, {
        headers: { Accept: "application/vnd.github+json" },
      });
      if (!res.ok) return;
      const data = await res.json();
      const version = data.tag_name || "v1.0.0";

      document.getElementById("latest-version").textContent = version;
      document.getElementById("footer-version").textContent = version;

      const installer = data.assets?.find((a) => /setup.*\.exe$/i.test(a.name));
      const portable = data.assets?.find((a) => /portable.*\.zip$/i.test(a.name));

      updateAsset(
        installer,
        document.getElementById("installer-name"),
        document.getElementById("installer-size"),
        document.getElementById("dl-installer"),
      );
      updateAsset(
        portable,
        document.getElementById("portable-name"),
        document.getElementById("portable-size"),
        document.getElementById("portable-link"),
      );

      // Hero CTA -> installer if available, else releases page
      const heroBtn = document.getElementById("hero-download");
      if (installer && heroBtn) {
        heroBtn.href = installer.browser_download_url;
        document.getElementById("hero-size").textContent = formatBytes(installer.size);
      }

      // Portable card href fix (we used dl-portable as the card itself)
      const portableCard = document.getElementById("dl-portable");
      if (portable && portableCard) portableCard.href = portable.browser_download_url;
    } catch {
      /* swallow — keep static fallbacks */
    }
  };

  const fetchRepoStats = async () => {
    try {
      const res = await fetch(`https://api.github.com/repos/${REPO}`, {
        headers: { Accept: "application/vnd.github+json" },
      });
      if (!res.ok) return;
      const data = await res.json();
      const el = document.getElementById("star-count");
      if (el && Number.isFinite(data.stargazers_count)) {
        el.textContent = `★ ${data.stargazers_count}`;
      }
    } catch {
      /* ignore */
    }
  };

  fetchLatestRelease();
  fetchRepoStats();
})();
