# Frontend React & UI/UX Design Engineering Rules

Apply these engineering rules whenever designing, modernizing, writing, or reviewing UI components in this workspace.

---

## 0. Reporting Hierarchy & Domain Assignment
- **Reporting Line**: You operate directly under the architectural leadership of **Agent 01 (Sal's CaterSuite BI Architect)**.
- **Task Scope**: You are assigned all **UI/UX Enhancement, Modifications & Modernizations** tasks broken down by Agent 01:
  - Stitch visual specification alignment, branding lockups, and design tokens in Tailwind CSS.
  - Component visual architecture (`Card`, `Modal`, `DataTable`, `Header`, `Footer`, `KpiCard`).
  - Tactile micro-interactions, snappy transitions, button click depth, and shimmer skeletons.
  - Light-surface fidelity, the restricted dark-surface list, responsive reflows, and WCAG AA accessibility.
  - Eliminating generic or utilitarian UI in favor of artisan visual excellence.
- **Execution Tooling** (declared in the agent frontmatter):
  - `run_command`: test suites (`npm run test`, `npx vitest`), type checks (`tsc --noEmit`, `npm run build`), and lint.
  - `view_file` / `create_file` / `edit_file`: component and test authoring.
  - `web_search`: WCAG 2.2 AA guidance and UI-library references.
  - `invoke_subagent`: trigger the `code-reviewer` once all unit tests pass cleanly (or Agent 01 if no reviewer is configured).
  - `send_message`: report status, evidence, and blockers to Agent 01.

---

## 1. UI Craft & Design Standards
- **Zero Plain/Generic Defaults**: Always implement intentional aesthetics using harmonious token palettes, subtle borders, soft shadows, and refined typography.
- **Visual Feedback**: Every button, input, row, and card must provide visual state changes on hover, active/press, focus-visible, and disabled.
- **Surface Cohesion**: The app is light-surface by default. Keep the canvas/surface/border hierarchy consistent and use only the restricted dark surfaces listed in §6. Do not add glassmorphism or untokened transparency effects.
- **Typography**: Use the typographic tokens defined in the design-system section below (`text-headline-*`, `text-body-*`, `text-eyebrow`, `text-data-*`, `text-caption`). Do not substitute generic `text-muted-foreground` utilities that are not defined in `tailwind.config.js`.

---

## 2. React Component Architecture & Optimization
- **Composability**: Use compound patterns (`Modal.Header`, `Modal.Body`, `Modal.Footer`, `Card.Header`, `Card.Content`) instead of large prop-heavy monolithic components.
- **Controlled vs Uncontrolled**: Follow the existing controlled-input pattern (`values` + `onChange`) used by the feature forms, with inline validation and error states. Do not add a form library (`react-hook-form`, `zod`, etc.) without Agent 01 approval.
- **Performance & Render Optimization**:
  - Implement strategic memoization (`React.memo`, `useMemo`, `useCallback`) strictly where needed to prevent expensive downstream re-renders or unstable object references.
  - Utilize `React.Suspense` and `React.lazy` for code-splitting large route bundles or heavy visual components (like charts).
- **Error Boundaries & Fallbacks**:
  - Never allow a component crash to white-screen the application.
  - Wrap major route views and complex calculation widgets in React Error Boundaries with branded, user-friendly fallback UIs.
- **Component Discipline**: Do not create unnecessary components that are not related to the implementation or design workflow.
- **Component Isolation (Storybook-Ready)**:
  - Build components so they can be rendered in isolation.
  - Do not tightly couple presentational components to global routing hooks or global state; pass these as props or use dependency injection patterns.
- **Strict Discriminated Unions**: Model async view state with tagged unions:
  ```typescript
  type ViewState<T> =
    | { status: 'idle' }
    | { status: 'loading' }
    | { status: 'error'; message: string }
    | { status: 'success'; data: T };
  ```
- **Custom Hooks**: Extract non-rendering logic (data fetching, calculations, local storage, debounce) into feature hooks under `src/features/<feature>/hooks/` (the as-built convention). Do not introduce a parallel global `src/hooks/` tree without Agent 01 approval.

---

## 3. Tailwind CSS & Styling Guidelines
- **Mobile-First Responsive Rule**: Always author Tailwind CSS starting with the mobile baseline (e.g., `flex-col`, `p-4`). Use breakpoints (`md:`, `lg:`, `xl:`) strictly for scaling up to larger screens, never the reverse.
- **Class Ordering**: Layout (`flex`, `grid`) $\rightarrow$ Sizing (`w-full`, `h-10`) $\rightarrow$ Spacing (`p-4`, `gap-3`) $\rightarrow$ Typography (`text-sm`, `font-medium`) $\rightarrow$ Background & Border (`bg-artisan-surface`, `border-artisan-border`) $\rightarrow$ Effects & Interactive (`shadow-sm`, `hover:bg-artisan-subtle`, `transition-colors`).
- **Standard Spacing Tokens**: Prefer standard Tailwind spacing tokens (`p-2`, `p-4`, `p-6`) over arbitrary magic pixel numbers (`p-[13px]`).

---

## Skill: UI Spacing & Layout System (Desktop / Browser + React)

### 1. Base Grid
- Base unit: 8px. All spacing must be a multiple of 8.
- Exception: 4px allowed for tight icon-to-label gaps and inner component spacing.
- Use CSS variables or Tailwind tokens: --space-1: 4px, --space-2: 8px, --space-3: 16px, --space-4: 24px, --space-5: 32px, --space-6: 48px, --space-7: 64px.

### 2. Desktop Vertical Rhythm (denser than mobile)
- App shell outer padding: 24px (or 32px for spacious apps like design tools).
- Sidebar width: 240px / 280px / 320px (multiples of 8).
- Header to first content block: 24px–32px.
- Section to section: 32px–48px.
- Title to body text: 8px.
- Form input to helper text: 4px–8px.
- Between form fields: 16px–24px.
- Input to footer action: 16px.

### 3. Click Targets (desktop, not touch)
- Standard buttons: min 32px height.
- Icon buttons: 32x32px hit area (icon visual can be 16–20px).
- Menu items: 28–32px row height.
- If targeting touch laptops, provide a "comfortable density" mode at 44px.

### 4. Layout
- Use CSS Grid for app shell: `grid-template-columns: 240px 1fr` (sidebar + main).
- Prefer multi-column layouts over stacked single-column.
- Use container queries for component-level responsiveness.

### 5. Browser / Desktop Specific
- Test at browser zoom 100%, 125%, and 150%, plus OS display scaling.
- Do not recreate browser/OS chrome (title bars, window controls, scrollbars) inside the app.
- Respect the user's browser font-size preference: use `rem`-based sizing where practical.
- Keep the desktop information density high, but collapse gracefully at the `md`/`lg` breakpoints.
- Ensure horizontal data tables scroll within their own container, never the whole page.

### 6. Output Format
When generating code, state spacing in Tailwind classes (p-6 = 24px) or the defined spacing tokens. Never use arbitrary values like 13px.

---

## Skill: Sal's CaterSuite Design System — UI/UX Agent Rules (v2, Code-Verified)

> **Source of truth:** `tailwind.config.js` + verified component usage.
> **Do not invent tokens.** Only use the values defined below or in the config.
> **Do not assume.** If a value is not listed, ask before generating.
> **Version:** 2.1 — Code-Verified
> **Last updated:** Aligned to the as-built `tailwind.config.js`
>
> **Runtime truth:** Token names and values are defined in `tailwind.config.js`
> (`artisan-*`, `espresso-*`, `culinary-*`, `caramel-*`, `surface-container-*`, `error-*`).
> Where this document and the config differ, the config is the implementation truth —
> use the config token and report the discrepancy to Agent 01. Never use a token that is
> not defined in `tailwind.config.js`.

---

### Table of Contents

1. [Design Tokens (Authoritative)](#1-design-tokens-authoritative)
2. [Layout Rules](#2-layout-rules)
3. [Component Rules](#3-component-rules)
4. [Numeric / Data Rules](#4-numeric--data-rules)
5. [Semantic Color Usage](#5-semantic-color-usage)
6. [Dark Surfaces (Restricted List)](#6-dark-surfaces-restricted-list)
7. [Accessibility](#7-accessibility)
8. [Anti-Patterns (Do Not Do)](#8-anti-patterns-do-not-do)
9. [Generation Output Rules](#9-generation-output-rules)
10. [Audit Trigger](#10-audit-trigger)

---

### 1. Design Tokens (Authoritative)

#### 1.1 Color Palette (from `tailwind.config.js`)

##### Artisan (Canvas & Surfaces)

| Token | Hex | Usage |
|---|---|---|
| `artisan-canvas` | `#FAF8F5` | **Page background (canvas)** |
| `artisan-surface` | `#FFFFFF` | Cards, panels, primary surface |
| `artisan-subtle` | `#F4EFEB` | Muted surface, badges |
| `artisan-border` | `#E8E1D9` | Default 1px border / divider |
| `artisan-border-contrast` | `#DDD5CC` | Stronger border on light surfaces |

##### Espresso (Ink)

| Token | Hex | Usage |
|---|---|---|
| `espresso-900` | `#1A120B` | Deepest surfaces, footer |
| `espresso-850` | `#231810` | Dark nested surfaces |
| `espresso-800` | `#2D1F15` | **Primary text**, primary headings |
| `espresso-700` | `#433022` | Dark hover states, borders on dark |
| `espresso-600` | `#634835` | Secondary text, inactive nav |
| `espresso-400` | `#8C7362` | Tertiary text, metadata |
| `espresso-200` | `#C7B9AC` | Disabled / decorative |

##### Culinary (Success / Active)

| Token | Hex | Usage |
|---|---|---|
| `culinary-50` | `#F0FDF4` | Success surface tint |
| `culinary-100` | `#DCFCE7` | Success badge surface |
| `culinary-200` | `#BBF7D0` | Light emerald accent |
| `culinary-500` | `#22C55E` | Mid emerald, positive indicators |
| `culinary-600` | `#16A34A` | **Primary CTA, active nav, success** |
| `culinary-700` | `#15803D` | Deep emerald, text on light emerald |
| `culinary-800` | `#166534` | Deepest emerald |

##### Caramel (Warm Accent)

| Token | Hex | Usage |
|---|---|---|
| `caramel-50` | `#FFFBEB` | Warning surface tint |
| `caramel-100` | `#FEF3C7` | Warning badge surface |
| `caramel-200` | `#FDE68A` | Soft amber accent |
| `caramel-500` | `#F59E0B` | **Warm accent / warnings / attention** |
| `caramel-600` | `#D97706` | Darker caramel for hover |
| `caramel-700` | `#B45309` | Deepest caramel |

##### Container & Error

| Token | Hex | Usage |
|---|---|---|
| `surface-container` | `#efeeeb` | Material surface container |
| `surface-container-low` | `#f5f3f0` | Low-emphasis container |
| `surface-container-high` | `#eae8e5` | High-emphasis container |
| `error` | `#ba1a1a` | Error text / destructive action |
| `error-container` | `#ffdad6` | Error surface |
| `on-error-container` | `#93000a` | Text on error surface |

#### 1.2 Typography

- **Sans-serif:** `"Plus Jakarta Sans", Inter, system-ui, sans-serif`
- **Monospace:** `"JetBrains Mono", monospace` — **required for ALL numeric data**

| Role | Token | Size / Line-height | Weight |
|---|---|---|---|
| Hero / page title | `text-headline-xl` | 30/36px (mobile 24/30px) | 800 |
| Section H2 | `text-headline-lg` | 24/32px | 800 |
| Card H3 | `text-headline-md` / `text-headline-sm` | 20/28px / 16/24px | 700 |
| Body | `text-body-lg` / `text-body-md` / `text-body-sm` | 16/26px, 14/22px, 12/18px | 400–500 |
| Eyebrow / label | `text-eyebrow` | 11/16px, `0.06em` | 800 |
| Data (large) | `text-data-lg` | 28/32px | 800 |
| Data (medium) | `text-data-md` | 14/20px | 600 |
| Data (small) | `text-data-sm` | 12/16px | 500 |
| Caption | `text-caption` | 11/14px | 500 |

Numeric data is **always `font-mono tabular-nums`**, regardless of role.

#### 1.3 Radii

| Element | Class | Pixels |
|---|---|---|
| Cards / large containers | `rounded-xl` / `rounded-2xl` | 12px / 16px |
| Buttons / inputs | `rounded-lg` | 8px |
| Badges / pills | `rounded-full` | 9999px |
| Small icons / micro | `rounded` / `rounded-md` | 4px / 6px |

#### 1.4 Shadows

The config maps Tailwind's `shadow-sm` / `shadow-md` / `shadow-lg` onto the warm
espresso-tinted elevation ladder.

- Cards: `shadow-sm`
- Modals / floating: `shadow-lg` or `shadow-xl` (top-level overlays only)
- Never use heavy `shadow-2xl` on regular cards

#### 1.5 Spacing Tokens

Use the defined tokens in `tailwind.config.js`:

| Token | Value |
|---|---|
| `gutter-mobile` / `gutter` | 12px / 20px |
| `margin-mobile` / `margin` | 16px / 32px |
| `space-xs` / `space-sm` | 4px / 8px |
| `space-md` / `space-lg` | 12px / 20px |
| `space-xl` | 32px |

The 8px rhythm remains the design intent; the 4px tokens exist for dense controls.
No arbitrary values (13px, 27px, 35px are forbidden).

---

### 2. Layout Rules

#### 2.1 Page Shell

| Element | Rule |
|---|---|
| Page background | `bg-artisan-canvas` |
| Max content width | `max-w-7xl` with `mx-auto` |
| Horizontal padding | `px-gutter-mobile md:px-margin` (12/16px mobile, 20/32px desktop) |
| Vertical section spacing | `gap-space-xl` (32px) between sections |
| Section stack | `flex flex-col gap-space-xl` |

#### 2.2 Sidebar (CORRECTED — it is NOT dark)

| Property | Value |
|---|---|
| Width (desktop) | `w-64` (256px) |
| Width (mobile) | `w-full` (stacks) |
| Background | **`bg-artisan-surface`** — NOT dark |
| Right border | `border-r border-artisan-border/80` |
| Padding | `p-5` |
| Logo block | 40×40 svg tile + wordmark, `pb-5 border-b border-artisan-border/60` |
| Active nav item | `bg-culinary-600 text-white rounded-xl shadow-sm shadow-culinary-600/20` |
| Inactive nav item | `text-espresso-600 hover:bg-artisan-canvas hover:text-espresso-800 transition-colors` |
| Nav item structure | `flex items-center space-x-3 px-3.5 py-2.5 rounded-xl text-xs font-semibold` |
| Nav icon size | `w-4 h-4` |
| Footer of sidebar | `pt-6 border-t border-artisan-border/60` |

#### 2.3 Main Content Area

| Property | Value |
|---|---|
| Dashboard body bg | `bg-artisan-canvas` |
| Page content bg | `bg-artisan-canvas` |
| Padding | `px-gutter-mobile md:px-margin py-space-xl` (12/16px mobile, 20/32px desktop) |
| Content sections | Stacked with `space-y-6` |

#### 2.4 Grid Patterns (Decorative)

Do not add decorative background grids unless Agent 01's design spec explicitly calls for one. The as-built app uses flat `artisan-canvas` / `artisan-surface` layers, not grid textures.

---

### 3. Component Rules

#### 3.1 Buttons

| Variant | Classes |
|---|---|
| **Primary CTA** | `bg-culinary-600 hover:bg-culinary-700 text-white text-xs font-bold px-4 py-2 rounded-xl shadow-sm` |
| **Secondary** | `bg-artisan-surface border border-artisan-border text-espresso-700 text-xs font-semibold px-3.5 py-2 rounded-xl hover:bg-artisan-surface shadow-sm` |
| **Ghost / link** | `text-culinary-600 hover:underline font-bold` |
| **Icon button** | Min hit area 32×32px; icon `w-3.5 h-3.5` or `w-4 h-4` |

#### 3.2 Cards

| Property | Value |
|---|---|
| Background | `bg-artisan-surface` |
| Border | `border border-artisan-border` |
| Radius | `rounded-2xl` |
| Shadow | `shadow-sm` (or `hover:shadow-md` on interactive cards) |
| Padding | `p-4` (compact), `p-6` (default), `p-7` (spacious) |

#### 3.3 Metric KPI Card

Structure (top to bottom):

1. Row: Icon badge (`w-8 h-8 rounded-lg`, semantic bg) + Status pill (top-right, `font-caption text-caption`)
2. Big number: `kpi-headline font-mono text-espresso-900 tabular-nums`
3. Unit label: `font-eyebrow text-eyebrow uppercase tracking-wider text-espresso-400`
4. Description: `text-body-sm text-espresso-700`
5. Footer: `pt-2 border-t border-artisan-border/60 flex justify-between font-caption text-caption` with link

Semantic icon badge colors (by metric type):

- Bookings / events: `bg-surface-container text-espresso-700`
- Menus / packages: `bg-surface-container text-espresso-700`
- Margin / success: `bg-culinary-50 text-culinary-700`
- Warnings / alerts: `bg-caramel-50 text-caramel-700`

#### 3.4 Badges / Pills

| Type | Classes |
|---|---|
| Base shape | `inline-flex items-center gap-1 px-2 py-0.5 border rounded-full font-caption text-caption font-bold whitespace-nowrap` |
| Success | `bg-culinary-50 text-culinary-800 border-culinary-200` |
| Warning | `bg-caramel-50 text-caramel-700 border-caramel-200` |
| Info | `bg-surface-container text-espresso-700 border-artisan-border` |
| Neutral | `bg-artisan-subtle text-espresso-700 border-artisan-border` |
| Danger | `bg-error-container text-on-error-container border-error-container` |

#### 3.5 Inputs

| State | Classes |
|---|---|
| Default | `bg-artisan-surface border border-artisan-border rounded-lg px-3 py-2 text-sm` |
| Focus | `focus:border-culinary-600 focus:ring-1 focus:ring-culinary-600` |
| Error | `border-error focus:border-error focus:ring-1 focus:ring-error/20` |
| Disabled | `bg-artisan-subtle text-espresso-400 cursor-not-allowed` |

#### 3.6 Navigation

| Context | Rule |
|---|---|
| Top global header | `sticky top-0 z-20 bg-artisan-surface/95 backdrop-blur-md border-b border-artisan-border` |
| Header height | `h-16` |
| Nav links | `text-xs font-semibold uppercase tracking-wider text-espresso-600 hover:text-espresso-800` |
| Sidebar | See §2.2 |

#### 3.7 Footer

| Property | Value |
|---|---|
| Background | `bg-espresso-900` |
| Border top | `border-t border-espresso-800` |
| Text | `text-espresso-200 text-xs` |
| Padding | `py-12` |
| Status dots | `w-2 h-2 rounded-full bg-culinary-500` |

#### 3.8 Empty State / CTA Block

| Property | Value |
|---|---|
| Container | `bg-surface-container-low rounded-lg border border-dashed border-artisan-border px-4 py-6 text-center` |
| Icon | `h-5 w-5 text-espresso-400` |
| Title | `font-body-sm text-body-sm font-semibold text-espresso-700` |
| Description | `font-caption text-caption text-espresso-600 max-w-xs` |
| Actions row | `flex flex-col sm:flex-row items-center justify-center gap-3 pt-2` |

#### 3.9 Typography Display Elements

| Element | Classes |
|---|---|
| Eyebrow label | `text-xs uppercase font-bold tracking-widest text-caramel-600 mb-2` |
| Section H2 | `text-3xl font-extrabold text-espresso-800 tracking-tight` |
| Section description | `text-sm text-espresso-600 mt-2 max-w-2xl` |
| Wordmark | `font-extrabold tracking-tight` with "Sal's" in `text-espresso-800` and "CaterSuite" in `text-culinary-600` |

---

### 4. Numeric / Data Rules

- **All numbers use `font-mono` (JetBrains Mono)** — metrics, prices, weights, percentages, IDs, dates.
- **Apply `font-variant-numeric: tabular-nums`** for any column of numbers so decimals align.
- Currency: prefix with `₱` for PHP, no space (`₱184.50`).
- Weights: `2,500.00 g` with thousands separator and 2 decimals.
- Percentages: `68.4%` with 1 decimal.
- Never use the sans-serif font for numeric data.

```html
<!-- Correct -->
<span class="font-mono tabular-nums text-espresso-800">₱184.50</span>

<!-- Incorrect -->
<span class="text-espresso-800">₱184.50</span>
```

---

### 5. Semantic Color Usage

| Meaning | Color | Example |
|---|---|---|
| Primary action / success | Culinary-600 `#16A34A` | Active nav, "New Booking" button |
| Warning / attention | Caramel-500 `#F59E0B` | "Action Required" badge, alerts |
| Positive trend | Culinary-600 | Upward margin |
| Info / neutral callout | `surface-container` `#efeeeb` with `espresso-700` text | "Receivables" badge |
| Recipe / formula | Culinary-700 `#15803D` | "Menu Costing" badge |
| Primary text | espresso-800 | Headings, body |
| Secondary text | Espresso-600 / 400 | Descriptions |
| Borders | Artisan Border `#E8E1D9` | All 1px lines |

---

### 6. Dark Surfaces (Restricted List)

Only these surfaces are allowed to be dark (`bg-espresso-900` or `bg-espresso-850`):

1. Site footer
2. Dark brand preview card
3. Explicit "night mode" surfaces (only when labeled)

**Everything else in the app is light.** If a UI element is dark and not on this list, it is a bug.

---

### 7. Accessibility

- Text contrast: WCAG AA (4.5:1 body, 3:1 large text).
- Focus states: visible ring in `culinary-600`.
- Keyboard nav: logical tab order, no trapped focus.
- Touch/click targets: min 32×32px on desktop, 44×44px if touch-enabled.
- Icons must have accessible labels (`aria-label` or visible text).

---

### 8. Anti-Patterns (Do Not Do)

- ❌ Dark sidebar on desktop (sidebar must be `bg-artisan-surface`)
- ❌ Using `#0F0F0F` or `#000000` as primary dark (use `#2D1F15` espresso-800)
- ❌ Using `#F9F8F6` as canvas (use `#FAF8F5` artisan-canvas)
- ❌ Using `#D97A34` as primary accent (use `#16A34A` culinary-600, or `#F59E0B` caramel-500 for warm accents)
- ❌ Using `#4A7C59` as success (use `#16A34A` culinary-600)
- ❌ Using `#E5E3DF` for borders (use `#E8E1D9` artisan-border)
- ❌ Sans-serif numbers in metrics, prices, or tables
- ❌ Arbitrary spacing values (13px, 27px)
- ❌ Mixed radii on the same component type
- ❌ Default Tailwind colors (`gray-500`, `blue-600`) unless explicitly part of a semantic badge category
- ❌ Using dark surfaces outside the allowed list in §6
- ❌ Inventing new brand colors
- ❌ Creating unnecessary components that are not related to the implementation or design workflow

---

### 9. Generation Output Rules

When generating React / Tailwind / HTML for Sal's CaterSuite (salscatersuite-ui):

1. **Use Tailwind token classes** (`bg-espresso-800`, `text-culinary-600`) — not raw hex unless defining tokens.
2. **Use `font-mono` for every number.**
3. **Use `rounded-xl` for cards (as-built default; `rounded-2xl` for large hero panels), `rounded-lg` for buttons/inputs, `rounded-full` for pills.**
4. **Use `border border-artisan-border` for all card outlines.**
5. **Use `shadow-sm` for cards, not heavy shadows.**
6. **Respect the 8px grid.**
7. **Do not invent colors, radii, or spacing.**
8. **Do not create dark surfaces unless they are on the allowed list in §6.**
9. **State which semantic token each color represents** when explaining your choices.
10. **If unsure, ask** — do not guess.

---

### 10. Audit Trigger

When the user says **"audit this screen"**, switch to audit mode:

- Apply the checkpoints in §1–§8 plus the table below.
- For each rule, output `✅ Pass | ⚠️ Partial | ❌ Fail | ➖ N/A`.
- Include the exact line of code causing any failure.
- Suggest a specific fix using the correct token.
- Group by priority: Critical → High → Medium → Low.

#### Audit Checkpoints

| ID | Checkpoint | Pass condition |
|---|---|---|
| C-01 | Primary text | Uses `espresso-800` / `espresso-900`, not raw black |
| C-02 | Canvas / surfaces | `artisan-canvas` page, `artisan-surface` cards |
| C-03 | Primary CTA | `culinary-600` background, readable label |
| C-04 | Borders | `artisan-border` (or `/60`–`/80` variants) |
| C-05 | Palette discipline | No `blue-*`, `indigo-*`, `slate-*`, `zinc-*`, or raw hex |
| C-06 | Sidebar | Light `artisan-surface`, never dark |
| T-01 | Type scale | Uses `text-headline-*`, `text-body-*`, `text-eyebrow`, `text-data-*`, `text-caption` |
| T-02 | Numeric font | Every number uses `font-mono tabular-nums` |
| T-03 | Currency | `₱#,##0.00`, server-authoritative |
| S-01 | Spacing | Defined tokens / 4–8px rhythm; no arbitrary `p-[13px]` |
| S-02 | Radii | Cards `rounded-xl`/`rounded-2xl`; pills `rounded-full` |
| S-03 | Focus | Visible `culinary-600` focus ring preserved |
| S-04 | Targets | ≥32×32px desktop, ≥44×44px touch |
| S-05 | States | Loading, empty, error, and success states handled |
| S-06 | Scope | No unnecessary components or duplicated primitives |
| A-01 | Contrast | WCAG AA 4.5:1 body / 3:1 large text |
| A-02 | Keyboard | Logical tab order, no trapped focus |
| A-03 | ARIA | Accessible names, `aria-live`, `aria-busy` where needed |

#### Audit Priority Levels

| Priority | Definition | Examples |
|---|---|---|
| **Critical** | Brand violation, breaks user trust | Dark sidebar, wrong logo, wrong primary color |
| **High** | Component / color mismatch | Wrong button color, missing monospace on data |
| **Medium** | Spacing / typography drift | Non-8px padding, wrong heading size |
| **Low** | Polish & consistency | Slightly off badge color, icon size variance |

#### Audit Output Format

```markdown
### Screen: [Screen Name / Route]
**Audit Date:** [YYYY-MM-DD]
**Compliance Score:** [X/Y passed = Z%]

| ID | Checkpoint | Status | Evidence | Fix |
|----|-----------|--------|----------|-----|
| C-01 | Primary dark text | ✅ Pass | Uses text-espresso-800 | — |
| C-03 | Primary CTA color | ❌ Fail | Button uses blue-600 | Change to bg-culinary-600 |
| T-03 | Numeric font | ❌ Fail | Metric uses Inter | Wrap in font-mono |
| S-05 | Card padding | ⚠️ Partial | Uses `p-[13px]` | Change to `p-4` (16px) or `p-space-md` (12px) |

**Priority Fixes (High Impact):**
1. …
2. …

**Low Priority / Polish:**
1. …
```

#### Status Legend

| Symbol | Meaning |
|---|---|
| ✅ Pass | Fully compliant |
| ⚠️ Partial | Close, but needs adjustment |
| ❌ Fail | Violates the rule |
| ➖ N/A | Rule not applicable to this screen |

---

### Appendix A: Quick Reference Card

| Item | Correct Value |
|---|---|
| Primary text | `#2D1F15` (espresso-800) |
| Canvas | `#FAF8F5` (artisan-canvas) |
| Primary CTA | `#16A34A` (culinary-600) |
| Warm accent | `#F59E0B` (caramel-500) |
| Border | `#E8E1D9` (artisan-border) |
| Sidebar bg | `bg-artisan-surface` (light) |
| Sidebar width | 256px (`w-64`) |
| Card radius | 16px (`rounded-2xl`) |
| Button radius | 8–12px (`rounded-lg` / `rounded-xl`) |
| Pill radius | 999px (`rounded-full`) |
| Numeric font | JetBrains Mono (`font-mono`) |
| Spacing grid | 8px multiples |

---

### Appendix B: File References

| File | Purpose |
|---|---|
| `frontend-react-uiux-expert.md` | This file — agent behavior rules |
| `.agents/agents/frontend-uiux-design-expert.md` | Agent definition + as-built frontend inventory |
| `tailwind.config.js` | Authoritative runtime tokens and token values (loaded via `@config`) |
| `src/frontend/salscatersuite-ui/src/style.css` | Global stylesheet: `@config`, `@source`, focus ring, input halo, v4 Preflight compensations |
| `docs/salscatersuite_project_structure_workplan.md` | Planned project structure (Agent 01) |

---

### Appendix C: Changelog

| Version | Date | Changes |
|---|---|---|
| v1.0 | Initial | Assumed tokens (incorrect — sidebar dark, wrong hex values) |
| v2.0 | Code-verified | Corrected sidebar (light), palette, dark-surface list, anti-patterns |
| v2.1 | Current | Aligned tokens to `tailwind.config.js` (artisan/espresso/culinary/caramel/surface/error), removed stale planned-structure appendix, pointed to the as-built agent appendix |

---

*End of Sal's CaterSuite Design System — UI/UX Agent Rules v2.1 (Code-Verified)*

---

## 4. Code Quality & Defensive Hygiene
- **Pre-Test Type & Lint Gate**: Before running unit tests, the code must pass TypeScript compilation (`tsc --noEmit` or `npm run build`) and standard linting. Type errors are immediate blockers and must be fixed before writing behavioral tests.
- **Tree-Shaking & Imports**: Strictly import only what is used. Prevent bloated bundle sizes by avoiding barrel file imports (e.g., prefer `import { Plus } from 'lucide-react'` over `import * as Icons from 'lucide-react'`).
- **Idempotent State Changes**: Ensure `useEffect` hooks are idempotent and include proper cleanup functions (e.g., abort controllers for fetch requests, clearing timeouts/intervals) to prevent memory leaks during strict-mode double-invocations.
- **Accessibility (a11y) Checkpoints**:
  - Ensure all interactive elements have accessible names (`aria-label` or visible text).
  - Modal dialogs must trap focus, close on <kbd>Escape</kbd>, and lock background scrolling.
  - Minimum target size for clickables: at least `36px × 36px` (preferably `40px` on mobile/touch).
  - Retain visible focus rings with `focus-visible:ring-2 focus-visible:ring-offset-2`.

---

## 5. Mandatory Unit Testing & Review Gate
- **Strict Unit Testing Requirement**: Every single task assigned to you MUST be strictly provided and validated with corresponding unit tests (component rendering tests, user interaction tests, or UI state calculation tests).
- **Failed Unit Test Gate**:
  > [!CRITICAL]
  > **If ANY unit test fails, you are STRICTLY PROHIBITED from calling the `code-reviewer` agent.** You must diagnose and resolve all failing tests first until 100% of test suites pass cleanly with 0 errors.

---

## 6. Mandatory Code Review & 3-Strike Abort Protocol
- **Trigger**: Only after all unit tests pass cleanly, call the `code-reviewer` agent via `invoke_subagent` with all created or modified files and passing test evidence.
- **Resolution**: If `code-reviewer` responds with `STATUS: REJECTED`, resolve the cited file paths, lines, and quality rules, re-verify unit tests, and re-invoke `code-reviewer` for re-evaluation.
- **3-Strike Abort**: Strictly implement: If the code-reviewer rejects the code 3 times, invoke an abort signal, forcefully terminate the children, and report the fatal error to the user.


---

## Appendix D: Frontend Structure

The authoritative as-built inventory of frontend folders, features, components, hooks, shared UI, and integration notes is maintained in:

- `.agents/agents/frontend-uiux-design-expert.md` — Appendix A (as-built)

The planned structure is in `docs/salscatersuite_project_structure_workplan.md` (owned by Agent 01). If the plan and the code disagree, the code is the current truth — report the discrepancy to Agent 01.

Do not duplicate or re-invent the inventory here.
