/**
 * Sal's CaterSuite — design tokens.
 *
 * Source of truth: `business/dashboard-feature-spec.md` §9.1, which mirrors the
 * `DESIGN.md` export of the `stitch_catersuite_dashboard_design` reference.
 *
 * Loaded by `src/style.css` through the `@config` directive (Tailwind CSS v4,
 * DEC-098), so this file stays the single source of truth for the tokens while
 * the build pipeline moves to v4.
 *
 * SCAN SCOPE: v4 resolves source files from CSS, not from this file — the
 * v3 `content` array is no longer read. The scan scope is pinned by the
 * `@source` directives in `src/style.css`; keep the two in step if either
 * changes.
 *
 * NOTE ON RADII: `DESIGN.md` ships two different radius statements.
 *   * the `rounded:` YAML block      -> sm 4px / DEFAULT 8px / md 12px / lg 16px / xl 24px
 *   * the "Shape Geometry Rules" text -> `rounded` 4px / `rounded-lg` 8px / `rounded-xl` 12px
 *                                        / `rounded-2xl` 16px / `rounded-full` pill
 * The second one is what `code.html` actually uses, and it maps 1:1 onto Tailwind's
 * default radius scale. The default scale is therefore preserved (so the reference
 * markup renders identically) and the `DESIGN.md` scale is additionally exposed under
 * explicit `radius-*` token names.
 */

/** @type {import('tailwindcss').Config} */
export default {
  theme: {
    extend: {
      colors: {
        /* Canvas & surfaces */
        artisan: {
          canvas: '#FAF8F5',
          surface: '#FFFFFF',
          subtle: '#F4EFEB',
          border: '#E8E1D9',
          'border-contrast': '#DDD5CC',
        },
        /* Espresso roasted-grain ink */
        espresso: {
          900: '#1A120B',
          850: '#231810',
          800: '#2D1F15',
          700: '#433022',
          600: '#634835',
          400: '#8C7362',
          200: '#C7B9AC',
        },
        /* Culinary emerald — confirmed / affirmative */
        culinary: {
          50: '#F0FDF4',
          100: '#DCFCE7',
          200: '#BBF7D0',
          500: '#22C55E',
          600: '#16A34A',
          700: '#15803D',
          800: '#166534',
        },
        /* Caramel amber — pending / variance / overdue */
        caramel: {
          50: '#FFFBEB',
          100: '#FEF3C7',
          200: '#FDE68A',
          500: '#F59E0B',
          600: '#D97706',
          700: '#B45309',
        },
        /* Material surface containers */
        surface: {
          container: '#efeeeb',
          'container-low': '#f5f3f0',
          'container-high': '#eae8e5',
        },
        /* Material error tokens — used by the "Cancelled" / "No Show" status chips */
        error: '#ba1a1a',
        'error-container': '#ffdad6',
        'on-error-container': '#93000a',
      },

      fontFamily: {
        sans: ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        mono: ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
        'headline-xl': ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        'headline-lg': ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        'headline-md': ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        'headline-sm': ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        eyebrow: ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        'body-lg': ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        'body-md': ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        'body-sm': ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        'data-lg': ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
        'data-md': ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
        'data-sm': ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
        caption: ['"Plus Jakarta Sans"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      },

      fontSize: {
        'headline-xl': [
          '30px',
          { lineHeight: '36px', letterSpacing: '-0.025em', fontWeight: '800' },
        ],
        'headline-xl-mobile': [
          '24px',
          { lineHeight: '30px', letterSpacing: '-0.02em', fontWeight: '800' },
        ],
        'headline-lg': [
          '24px',
          { lineHeight: '32px', letterSpacing: '-0.02em', fontWeight: '800' },
        ],
        'headline-md': [
          '20px',
          { lineHeight: '28px', letterSpacing: '-0.015em', fontWeight: '700' },
        ],
        'headline-sm': [
          '16px',
          { lineHeight: '24px', letterSpacing: '-0.01em', fontWeight: '700' },
        ],
        eyebrow: ['11px', { lineHeight: '16px', letterSpacing: '0.06em', fontWeight: '800' }],
        'body-lg': ['16px', { lineHeight: '26px', fontWeight: '400' }],
        'body-md': ['14px', { lineHeight: '22px', fontWeight: '400' }],
        'body-sm': ['12px', { lineHeight: '18px', fontWeight: '500' }],
        'data-lg': ['28px', { lineHeight: '32px', letterSpacing: '-0.02em', fontWeight: '800' }],
        'data-md': ['14px', { lineHeight: '20px', fontWeight: '600' }],
        'data-sm': ['12px', { lineHeight: '16px', fontWeight: '500' }],
        caption: ['11px', { lineHeight: '14px', fontWeight: '500' }],
      },

      spacing: {
        gutter: '1.25rem',
        'gutter-mobile': '0.75rem',
        margin: '2rem',
        'margin-mobile': '1rem',
        'space-xs': '0.25rem',
        'space-sm': '0.5rem',
        'space-md': '0.75rem',
        'space-lg': '1.25rem',
        'space-xl': '2rem',
      },

      borderRadius: {
        'radius-micro': '0.25rem',
        'radius-control': '0.5rem',
        'radius-card': '0.75rem',
        'radius-hero': '1rem',
        'radius-pill': '9999px',
      },

      boxShadow: {
        'elevation-1': '0 1px 3px rgba(35, 24, 16, 0.04), 0 1px 2px rgba(35, 24, 16, 0.02)',
        'elevation-2': '0 4px 16px -2px rgba(35, 24, 16, 0.05), 0 2px 6px -1px rgba(35, 24, 16, 0.03)',
        'elevation-3': '0 8px 24px -4px rgba(22, 163, 74, 0.16)',
        /* The reference markup uses shadow-sm / shadow-md / shadow-lg — map them onto
           the warm espresso-tinted elevation ladder instead of Tailwind's cold greys. */
        sm: '0 1px 3px rgba(35, 24, 16, 0.04), 0 1px 2px rgba(35, 24, 16, 0.02)',
        md: '0 4px 16px -2px rgba(35, 24, 16, 0.05), 0 2px 6px -1px rgba(35, 24, 16, 0.03)',
        lg: '0 8px 24px -4px rgba(22, 163, 74, 0.16)',
      },
    },
  },
  plugins: [],
}
