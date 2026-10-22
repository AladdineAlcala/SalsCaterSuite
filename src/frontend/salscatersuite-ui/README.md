# Sal's CaterSuite — Operations Dashboard (frontend)

React 19 + Vite 8 + TypeScript 6 + Tailwind CSS 3.4 client for Sal's CaterSuite.

This cycle delivers the **app shell**, the **authentication** flow and the
**Operations Dashboard** screen, rendered exclusively from `GET /api/dashboard`
(`architecture/approved-api-contracts.md` §9, `business/dashboard-feature-spec.md` v1.0).

---

## Scripts

Run these from this folder. To run the **UI and the API together**, use
`npm run dev` from the repository root instead (see the root `README.md`).

| Command | Purpose |
| :--- | :--- |
| `npm run dev` | Vite dev server on <http://localhost:5173> with the `/api` proxy. |
| `npm run build` | `tsc` (type check, zero errors) + `vite build` into `dist/`. |
| `npm run preview` | Serves the production build with the same `/api` proxy. |
| `npm run test` | Vitest + React Testing Library suite (jsdom). |
| `npm run test:watch` | Vitest in watch mode. |

## Dev proxy and the API target

The browser always calls the API on **its own origin** (`/api/...`). Vite
forwards that call to the backend, which means:

* no CORS pre-flight is needed (the API's CORS policy is not exercised);
* the API's `UseHttpsRedirection` middleware is bypassed, because the proxy
  talks plain HTTP to the Kestrel HTTP listener.

The proxy target defaults to `http://localhost:5149` and can be overridden per
machine with the `VITE_API_PROXY_TARGET` environment variable:

```powershell
# PowerShell
$env:VITE_API_PROXY_TARGET = "http://localhost:7087"; npm run dev

# cmd
set VITE_API_PROXY_TARGET=http://localhost:7087 && npm run dev
```

The proxy is configured in `vite.config.ts` with `changeOrigin: true` and
`secure: false` (so a self-signed development certificate does not break the
request).

## Design tokens

`tailwind.config.js` maps the `DESIGN.md` token set onto Tailwind:

| `DESIGN.md` group | Tailwind token |
| :--- | :--- |
| `artisan-canvas / surface / subtle / border / border-contrast` | `artisan-canvas`, `artisan-surface`, `artisan-subtle`, `artisan-border`, `artisan-border-contrast` |
| `espresso-900 … espresso-200` | `espresso-900`, `espresso-850`, `espresso-800`, `espresso-700`, `espresso-600`, `espresso-400`, `espresso-200` |
| `culinary-50 … culinary-800` | `culinary-50/100/200/500/600/700/800` |
| `caramel-50 … caramel-700` | `caramel-50/100/200/500/600/700` |
| Material `surface-container*` | `surface-container`, `surface-container-low`, `surface-container-high` |
| Material `error*` (status chips) | `error`, `error-container`, `on-error-container` |
| Type scale (`headline-xl` … `caption`) | `font-headline-xl` / `text-headline-xl` … `font-caption` / `text-caption` (plus `data-lg/md/sm` on JetBrains Mono) |
| Spacing (`gutter`, `margin`, `space-xs..xl`) | `gutter`, `gutter-mobile`, `margin`, `margin-mobile`, `space-xs/sm/md/lg/xl` |
| Radii | `radius-micro/control/card/hero/pill` (Tailwind's own `rounded*` scale is preserved because it is what the reference markup uses) |
| Elevation 1 / 2 / 3 | `shadow-elevation-1`, `shadow-elevation-2`, `shadow-elevation-3` (also aliased onto `shadow-sm` / `shadow-md` / `shadow-lg`) |

## Structure

```
src/
├── api/            axios client, error normalisation, token store
├── components/     ui/ (Button, Card, Badge, ProgressTrack, IconBadge, Skeleton)
│                   feedback/ (SpinnerLoader, AlertBanner, EmptyState)
│                   navigation/ (AppSidebar, TopBar, UserChip, navItems)
├── context/        AuthContext
├── features/       auth/ (LoginPage)
│                   dashboard/ (DashboardPage + components + hooks)
│                   placeholders/ (NotImplementedPage)
├── layouts/        AppLayout (shell), AuthLayout (login shell)
├── routes/         AppRoutes, ProtectedRoute
├── types/          dashboard.types, auth.types
├── utils/          currencyFormatter, dateFormatter, progressRatio, bookingStatus
└── test/           Vitest setup and fixtures
```

## Rules the code enforces

* **No hardcoded figure.** Every number, percentage, currency value and date on
  the dashboard comes from the API payload. A test
  (`src/features/dashboard/__tests__/noHardcodedFigures.test.ts`) scans the
  dashboard feature folder for peso literals and metric constants.
* **No financial total is computed in the browser.** The client formats and
  displays; ratios the API already returns are shown as given, and progress
  widths are only clamped to 0–100.
* **`null` renders `—`**, never `0` and never `100%`.
* **Status is never colour alone** — every chip carries a text label and an
  icon.
* **No fake features.** The global search field and the notification bell are
  inert and labelled as unavailable; there is no search endpoint and no
  notification hub in the solution. Every navigation entry other than the
  Dashboard resolves to a labelled "not yet implemented" page.
