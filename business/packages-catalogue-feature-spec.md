# Sal's CaterSuite - Packages & Buffets Catalogue - Feature Specification

| Field | Value |
| :--- | :--- |
| **Requirement** | REQ-PKG-01 (Packages - Catering Packages Catalog); REQ-PKG-04 (Package Applicable Pax Classification) |
| **Specification version** | v1.1 |
| **Issued** | 2026-10-21; amended 2026-10-28 by AGENT 01 (BI Architect) |
| **Status** | REQ-PKG-01 and REQ-PKG-04 delivered |
| **Design reference** | `d:\Downloads\SalsCaterSuite_Designs\Package_Main\code.html` + `screen.png` - **reference only**, not a specification of data |
| **API contract** | `architecture/approved-api-contracts.md` section 12 (issued with this specification) |
| **Decisions** | DEC-067 .. DEC-072, DEC-162 .. DEC-167, DEC-173 (`governance/decision-log.md`) |
| **KPI additions** | KPI-25 .. KPI-28 (`business/kpi-catalog.md`) |
| **Tasks** | TASK-PKG-01-BE / FE (delivered); TASK-PKG-04-BE / FE (applicable-pax enhancement) |

---

## 1. Objective and Scope

### 1.1 Objective

Deliver the **Packages & Buffets Catalogue** screen (`/packages`) and the **tier form** (`/packages/new`, `/packages/:packageId/edit`) so that a catering coordinator can see every configured buffet tier, its per-pax rate, its minimum headcount and its active state, filter the catalogue by tier type, and create, edit and deactivate a tier.

REQ-PKG-01 acceptance (as catalogued): *"List active packages, filter by package type."* This specification delivers that, plus the write operations the same requirement implies ("Manage buffet packages ... with pax rates and minimum pax").

### 1.2 What the design reference shows and what the approved model can actually carry

The reference screen is built around five blocks that the approved data model **does not carry**:

| Reference block | Approved model | Decision |
| :--- | :--- | :--- |
| Per-tier "Food Cost %" and "Gross Margin" band | `Package` has no cost, no margin and no link to `Menu` | **Capability gap** (DEC-067) |
| "Course Allocations Matrix" (8 course profiles) | No package-to-dish allocation entity exists | **Capability gap** (DEC-067) |
| "Complimentary Event Inclusions" | No inclusion or rentals entity exists (`AddonDetail` is a separate add-on catalogue) | **Capability gap** (DEC-067) |
| "Venue Coverage" (14 venues mapped) | `Area` exists but is **not** related to `Package`; `PackageCoverageDto` has no producing query | **Capability gap** (DEC-067) |
| "Export Specs (ClosedXML)" | No packages export exists | **Not implemented** (DEC-071) |
| "Duplicate Tier" | No clone endpoint; a duplicate would copy course and coverage spec that does not exist | **Not implemented** (DEC-071) |
| "SignalR Price Engine" / "QuestPDF Contract Sync" telemetry cards | `NotificationHub` is not built; no package feeds any document | **Not implemented** (DEC-071) |
| "Archive Tier" | `PATCH /api/packages/{id}/toggle-active` exists | **Implemented** as an honest activate/deactivate toggle (DEC-071) |
| "Create / Edit Package" | `POST`/`PUT /api/packages` exist | **Implemented** (this specification) |

Every capability gap renders as an explicit, labelled **capability-gap notice** in the screen - never a fabricated table, never a placeholder price, never a seeded sample tier. This is the DEC-059 / DEC-044 precedent applied to the packages screen.

### 1.3 Exclusions (explicit)

- No per-tier food cost, gross margin, target band or margin variance.
- No course allocation, dish inclusion, rental or waitstaff-ratio content.
- No venue/area coverage, no setup fee, no commute surcharge.
- No packages XLSX export.
- No duplicate/clone action.
- No SignalR live-price panel.
- No seeded sample tiers (BR-13 forbids fabricated business data).
- No package-to-menu or package-to-area database change. The `Package` entity is **not** extended in this cycle.

---

## 2. Actors and Permissions

| Actor | Capability |
| :--- | :--- |
| Any authenticated user (BR-12) | View the catalogue, view the tier inspector |
| Any authenticated user | Create a tier, edit a tier, activate/deactivate a tier |

`GET /api/packages`, `GET /api/packages/summary`, `GET /api/packages/{id}`, `POST /api/packages`, `PUT /api/packages/{id}` and `PATCH /api/packages/{id}/toggle-active` all require a bearer token and no role policy, exactly as `PackagesController` already declares. The client hides nothing that the server would allow, and the server enforces the token requirement.

---

## 3. Domain and Data

### 3.1 Package tier and applicable-pax classification (DEC-173)

`Package` (`Domain/Entities/Package.cs`):

| Field | Type | Notes |
| :--- | :--- | :--- |
| `PackageId` | `int` | Identity key. |
| `PackageCode` | `string` | The frozen business code `PKG-{TYPE}-{NN}` (DEC-162). |
| `PackageType` | `string` | Free text. The `PackageType` enum (`Regular`, `Wedding`, `Vip`, `Premier`, `Pm`, `Sd`) documents the intended vocabulary but the column is not typed to it. |
| `Description` | `string` | The tier's display name. |
| `AmountPax` | `decimal?` | The per-guest rate. `null` = not priced. |
| `MinPax` | `int?` | The minimum headcount. `null` = not set. |
| `NumPaxId` | `int` | Required FK to `PackagePaxApplicable`; a descriptive classification only, not a numeric booking rule. |
| `ExtendedLocationAmount` | `decimal?` | Per-guest extended-location amount for eligible types only (DEC-163). |
| `IsActive` | `bool` | Whether the tier is selectable. |

Plus the four `BaseEntity` audit columns (DEC-038).

#### 3.1.1 `PackagePaxApplicable` lookup

The read-only lookup has the fixed ID/label pairs below. These labels are stored and returned verbatim; the missing intervals and option-4 wording are intentional. Do not parse numeric bounds from the text or relate option 5 to `PackageType.Pm`.

| `NumPaxId` | `PackageApplicablePax` |
| :---: | :--- |
| 1 | `15 to 19 Pax` |
| 2 | `20 to 29 Pax` |
| 3 | `30 to 49 Pax` |
| 4 | `80 to 100 Above` |
| 5 | `Not Applicable to PackMeals` |

`PackagePaxApplicable` follows the project `BaseEntity`/DEC-038 audit-column convention. The five values are seeded by the schema migration, use their stated IDs, and have no create/update/delete API. Existing package rows are backfilled only with the owner-approved mapping: `p_id=1 → 4`; `p_id=2 → 2`. The migration must fail rather than infer a value for any other unmapped row.

The required selector is classification metadata only. It does not replace or validate `MinPax`, establish booking headcount limits, change `PackageType`, or affect booking eligibility, quotes, or pricing.

### 3.2 The catalogue aggregate (new, server-computed)

`GET /api/packages/summary` publishes:

| Field | Type | Definition |
| :--- | :--- | :--- |
| `totalPackages` | `int` | Every non-soft-deleted tier. |
| `activePackages` | `int` | `IsActive = true`. |
| `inactivePackages` | `int` | `IsActive = false`. |
| `pricedPackages` | `int` | Tiers with a non-null `AmountPax`. |
| `averageAmountPax` | `decimal?` | Mean of the non-null `AmountPax`, **rounded to two decimals away from zero** (DEC-003). `null` when no tier is priced. |
| `lowestAmountPax` | `decimal?` | Minimum non-null `AmountPax`. `null` when no tier is priced. |
| `highestAmountPax` | `decimal?` | Maximum non-null `AmountPax`. `null` when no tier is priced. |
| `lowestMinPax` | `int?` | Minimum non-null `MinPax`. `null` when no tier sets one. |
| `highestMinPax` | `int?` | Maximum non-null `MinPax`. `null` when no tier sets one. |
| `generatedAt` | `DateTime` | Server UTC timestamp, stamped from `IDateTime.UtcNow` so it is testable. |

**Why the server computes it.** The browser never derives a monetary aggregate (DEC-002 precedent, workplan 3.5). The reference's KPI ribbon ("Average Per Pax Rate", "Range: ... to ...") would otherwise be a client-side money computation - precisely the class of divergence DEC-025 / DEC-042 exist to prevent. The endpoint is **additive**: `GET /api/packages` keeps its current shape and its current `activeOnly` filter, so no existing consumer breaks (DEC-068).

**Null semantics.** `null` means *unknown*, never zero (contract rule 6). The client renders an em dash for a null aggregate and never `0` or `0.00`.

### 3.3 Validation (BR-11)

The write commands currently accept anything: a blank type, a blank description, a negative rate or a zero minimum headcount all reach the database. This specification closes that gap with one validator shared by the create and the update command (`PackageRequestValidator`), following the `MenuRequestValidator` precedent:

| Rule | Message |
| :--- | :--- |
| `PackageType` required, max **50** characters | "Package type is required." / "Package type must not exceed 50 characters." |
| `Description` required, max **250** characters | "Package description is required." / "Package description must not exceed 250 characters." |
| `NumPaxId` required and must reference a live `PackagePaxApplicable` row (DEC-173) | "Applicable pax classification is required." / "Applicable pax classification was not found." |
| `AmountPax` zero or greater when supplied | "Rate per guest must be zero or greater." |
| `MinPax` at least 1 when supplied | "Minimum pax must be at least 1." |

**The limits are the column limits.** `PackageConfiguration` declares `p_type` as `nvarchar(50)` and `p_descripton` as `nvarchar(250)`. A validator that allowed 100 characters would pass a payload that the database then rejects as an unhandled `500` - the exact defect BR-11 exists to close. The `MenuRequestValidator` is column-aligned for the same reason (DEC-069).

The type is **not** restricted to the `PackageType` enum members: the column is free text and legacy rows may carry other values (DEC-070). Rejections return `400 { "errors": [...] }`; a missing tier on update returns `404`.

The client's own form validation uses **the same wording**, so the operator reads one message whether the request was rejected in the browser or by the server.

### 3.4 Soft-delete scoping (DEC-074)

Every entity carries `IsDeleted` (DEC-038) but there is **no global query filter** (DEC-045). The packages catalogue therefore filters it in its own read projections, exactly as the dashboard (DEC-045) and the Customer Master (DEC-050) do:

| Projection | Filter | Why |
| :--- | :--- | :--- |
| `GET /api/packages` (the list projection) | `IsDeleted == false` | It is the catalogue's list; a deleted tier must not be counted or shown. |
| `GET /api/packages/{id}` (the detail projection) | `IsDeleted == false` | Same catalogue, same reasoning. A soft-deleted tier answers `404`. |
| `GET /api/packages/summary` | `IsDeleted == false` | The counts and the statistics must describe the same population the list shows. |
| `IPackageRepository.GetByIdAsync` (the **entity** fetch) | **unchanged** | `CreateBookingCommand` resolves the booking's price per head from it (BR-04). Filtering it would silently change the booking-creation path, which needs its own decision and is out of this scope. |

Without this scoping the screen would show "Configured Tiers = 3" beside four cards.

---

## 4. API Surface

| Method | Route | Purpose | Change |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/packages?activeOnly=` | The tier list | **Existing** - shape unchanged |
| `GET` | `/api/packages/summary` | The catalogue aggregate | **New** (DEC-068) |
| `GET` | `/api/packages/{id}` | One tier | **Existing** |
| `POST` | `/api/packages` | Create a tier | **Existing** - now validated |
| `PUT` | `/api/packages/{id}` | Update a tier | **Existing** - now validated, `404` vs `400` distinguished |
| `PATCH` | `/api/packages/{id}/toggle-active` | Activate / deactivate | **Existing** |

`/api/packages/summary` is a literal segment and can never be captured by `/api/packages/{id:int}`.

---

## 5. UI Specification

### 5.1 Routes

| Route | Element | State |
| :--- | :--- | :--- |
| `/packages` | `PackageCataloguePage` | New |
| `/packages/new` | `PackageTierFormPage` (create) | New |
| `/packages/:packageId/edit` | `PackageTierFormPage` (edit) | New |

The navigation entry "Packages & Buffets" (`/packages`) becomes `implemented: true`. Every other route is unchanged.

### 5.2 The catalogue screen (`/packages`)

Follows the delivered anatomy of the Menu & Food Costing Master: a banner, a four-card KPI ribbon, a filter bar, a card list in the left column, a sticky inspector in the right column, a footer, and distinct loading / error / empty states.

1. **Banner** - breadcrumb, the title "Packages & Buffets Catalogue", the scope line, a "Recalculate" action, and a "+ New Package" action. No export button (no endpoint).
2. **KPI ribbon** - four cards, every figure from `GET /api/packages/summary`:
   - **Configured Tiers** - `totalPackages`; sub-line "N active / M inactive"; a coverage track of the active share.
   - **Average Per-Pax Rate** - `averageAmountPax`; sub-line "Across N priced tiers"; `null` renders an em dash.
   - **Per-Pax Rate Range** - `lowestAmountPax` - `highestAmountPax`; em dash when no tier is priced.
   - **Minimum Headcount** - `lowestMinPax`; sub-line "Highest floor: N heads"; when no tier sets a floor the sub-line reads "No tier sets a minimum headcount" and the unit is suppressed, so the readout never shows a dash followed by a unit.
3. **Filter bar** - a free-text search over the tier name and type, a **tier-type tab strip** (All + one tab per distinct type present in the catalogue, each with its live count), an **active/inactive** filter and a **sort** control (rate high-low, rate low-high, name A-Z).
4. **Tier cards** - one card per tier: the tier type as a badge, the description as the heading, the per-guest rate, the minimum headcount, and the active state as a labelled chip (never colour alone).
5. **Inspector** (right column, sticky) - the selected tier's identity, its commercial terms (rate, minimum headcount, state), the actions (Edit Tier / Deactivate-or-Activate), and the **four capability-gap notices**.
6. **Empty states** - an empty catalogue and a filter that matches nothing are **different** states and say so.
7. **Footer** - the server's `generatedAt` and the API connection state, exactly like the delivered screens.

### 5.3 The tier form (`/packages/new`, `/packages/:packageId/edit`)

The fields below each have an explicit label, a hint, and an inline validation message where applicable:

| Field | Control | Required | Notes |
| :--- | :--- | :--- | :--- |
| Tier type | text input with a `<datalist>` | yes | Free text. The datalist offers the six `PackageType` member names **verbatim** (`Regular`, `Wedding`, `Vip`, `Premier`, `Pm`, `Sd`) so that a suggestion is always a value the server already understands. The type is never rewritten (DEC-070). |
| Tier name / description | text input | yes | What the coordinator calls the tier |
| Rate per guest | number input | no | A plain number. An **empty** field is submitted as `null` ("not priced"), never as `0`. |
| Minimum pax | number input | no | A plain number. An **empty** field is submitted as `null` ("not set"), never as `0`. |
| Applicable pax classification | API-fed select | yes | Options come from `GET /api/packages/pax-applicables`; the selected lookup ID is sent as `numPaxId`. The label is not interpreted as a numeric range. |
| Extended location amount | currency input | conditional | Existing DEC-163 behavior: shown only for `Regular`/`Premier`; nullable and per guest. |

- The form validates on the client for the obvious cases, using **the server's own wording** ("Package type is required.", "Package description is required.", "Rate per guest must be zero or greater.", "Minimum pax must be at least 1."), so the operator reads one message wherever the request is rejected.
- The form always renders the server's `errors[]` verbatim (contract section 7), keeping the typed input after a rejected save.
- Save navigates back to `/packages` and the catalogue is invalidated.
- `Cancel` returns to `/packages` without a write.

### 5.4 Accessibility and presentation rules

- Currency is always `₱` with two decimals through `utils/currencyFormatter.ts`.
- Every status chip carries a text label beside its tone (WCAG 1.4.1).
- The type tab strip is a `role="group"` of toggle buttons with `aria-pressed`, each announcing its count.
- The inspector is `aria`-labelled and the cards are buttons with an accessible name that includes the tier name and rate.
- No horizontal overflow at 375 / 768 / 1024 / 1440 px.
- A feature-folder `noHardcodedFigures` scan fails the build if a peso, a thousands separator, a decimal literal, a four-digit literal or a percentage literal is hardcoded in the packages feature.

---

## 6. KPI Impact

| KPI | Definition | Status |
| :--- | :--- | :--- |
| KPI-25 | Configured tier count (active / inactive split) | PROPOSED |
| KPI-26 | Catalogue average per-pax rate | PROPOSED |
| KPI-27 | Catalogue per-pax rate range (lowest / highest) | PROPOSED |
| KPI-28 | Catalogue minimum-headcount floor range (lowest / highest) | PROPOSED |

Full definitions are appended to `business/kpi-catalog.md`. They are catalogue statistics, not revenue, and they never enter a booking total.

---

## 7. Acceptance Criteria

| ID | Criterion |
| :--- | :--- |
| AC-P01 | `/packages` renders the banner, the four KPI cards, the filter bar, the tier cards and the inspector from the API payloads. |
| AC-P02 | Every KPI figure is the server's `GET /api/packages/summary` value; the browser computes no monetary aggregate. A `null` aggregate renders an em dash, never `0`. |
| AC-P03 | The tier list is the server's `GET /api/packages` list; the tier type tab strip shows one tab per distinct type present, each with its live count. |
| AC-P04 | Searching, type filtering, the active filter and the sort are applied client-side to the server's list; no filter is sent to an endpoint that does not accept one. |
| AC-P05 | Selecting a card loads that tier into the inspector; the inspector shows the rate, the minimum headcount and the active state. |
| AC-P06 | The four capability-gap notices are rendered where the reference shows food cost/margin, course allocations, event inclusions and venue coverage. No fabricated rate, course list, inclusion or venue is rendered anywhere. |
| AC-P07 | An empty catalogue and a filtered result that matches nothing produce **different** messages; neither fabricates a tier. |
| AC-P08 | A failed load renders the error banner with the server's message and a Retry action; nothing is invented while the catalogue is unavailable. |
| AC-P09 | "Deactivate Tier" calls `PATCH /api/packages/{id}/toggle-active` and the catalogue is refreshed; the button label follows the tier's current state. |
| AC-P10 | "Edit Tier" opens `/packages/:packageId/edit` pre-filled from `GET /api/packages/{id}`; "New Package" opens `/packages/new`. |
| AC-P11 | The form rejects a blank type, a blank name, a negative rate, a minimum pax below 1, and a missing applicable-pax classification; the server's `errors[]` are rendered verbatim while the typed input is preserved. |
| AC-P12 | A successful create returns `201` and the catalogue shows the new tier; a successful update returns `204`. |
| AC-P13 | The packages feature folder contains no hardcoded peso, thousands separator, decimal, four-digit or percentage literal. |
| AC-P14 | Every route renders with zero console errors, and there is no horizontal overflow at 375 / 768 / 1024 / 1440 px. The overflow check is performed in a live browser at those four widths (the same headless-Chrome method used for the dashboard, the Customer Master and the Menu Master); Playwright is not installed in this repository, so no automated end-to-end suite is claimed. |
| AC-P15 | The reference's export, duplicate, SignalR and QuestPDF telemetry affordances are absent. |
| AC-P16 | The form loads the five fixed applicable-pax choices from the authenticated lookup endpoint; edit mode preselects the saved `numPaxId`; loading/error states are explicit; the browser does not duplicate the option labels. |
| AC-P17 | The API returns the fixed choices in ID order. Missing/zero `numPaxId` on create/update returns `400 { errors: ["Applicable pax classification is required."] }`; an unknown ID returns `400 { errors: ["Applicable pax classification was not found."] }`; neither persists or changes anything. |
| AC-P18 | The migration inserts the five approved lookup rows and applies only `p_id=1 → 4` and `p_id=2 → 2`; an unexpected unmapped package row causes a safe failure. The migration is script-reviewed only and never applied to a persistent database in this cycle. |
| AC-P19 | The applicable-pax classification has no effect on package type, `MinPax`, booking eligibility, quote payloads, or pricing. |

---

## 8. Required Tests

**Backend (TASK-PKG-01-BE)**

- `PackageCatalogueSummaryTests` - the aggregate over a known population: counts, the average, the lowest/highest rate, the headcount floor range, and the `null` cases (no priced tier, no floor).
- `PackageRequestValidatorTests` - every rejection message; a valid payload passes.
- `PackageCommandTests` (extended) - a rejected payload persists nothing and returns the messages; the not-found case is distinguished from a validation failure.
- `PackageRepositoryTests` (extended) - the aggregate over the seeded population.
- Integration: `GET /api/packages/summary` returns `200` with the envelope; `POST` with an invalid payload returns `400 { "errors": [...] }`.

**Additional backend coverage (REQ-PKG-04 / DEC-173)**

- Lookup query/repository tests prove the five fixed IDs and labels are returned in ID order.
- Validator/command tests cover missing, zero, and unknown `numPaxId` for both create and update, proving rejected writes persist nothing.
- Integration tests cover authenticated lookup access, DTO projections, write validation, and the migration mapping/guard on a disposable database.
- Review generated SQL and model-snapshot parity; do not apply the migration to a persistent database.

**Frontend (TASK-PKG-01-FE)**

- `PackageCataloguePage.test.tsx` - the KPI cards, the type tabs, the card list, the inspector, the capability-gap notices, both empty states, the error state with retry, the toggle action and the navigation to the form.
- `PackageTierFormPage.test.tsx` - the pre-filled edit form, the client-side rejection messages, the server's verbatim `errors[]`, and the successful save.
- `noHardcodedFigures.test.ts` - the feature scan.
- `PackageTierFormPage.test.tsx` additionally covers the required API-fed applicable-pax selector, edit preselection, loading/error behavior, and payload inclusion of `numPaxId`.

---

## 9. Open Questions for the Owner

These do **not** block the delivery; they are recorded for a later cycle.

1. **Tier type vocabulary.** Should `Package.PackageType` be constrained to the six `PackageType` enum members, or kept free text? A typed column would need a migration and a data-cleanup decision for legacy rows.
2. **Per-tier food cost.** Should a tier carry its own food cost, or should it be derived from the dishes allocated to it? Either answer requires the package-to-dish allocation entity, which does not exist.
3. **Venue coverage.** Should a tier be linked to the `Area` table, and should an area carry a per-tier setup surcharge?
4. **Minimum booking value.** Should the catalogue publish `AmountPax x MinPax` as a tier's minimum contract value? It is arithmetically obvious but is a commercial figure and therefore needs an owner ruling before it is displayed.

---

## 10. Task Split

| Task | Owner | Deliverables |
| :--- | :--- | :--- |
| TASK-PKG-01-BE | AGENT 02 (.NET Backend) | `GET /api/packages/summary` (DTO + query + repository method + controller action), `PackageRequestValidator`, the validated create/update commands, DI registration, unit tests, one integration test. |
| TASK-PKG-01-FE | AGENT 03 (React Frontend) | `src/features/packages/**`, `src/types/package.types.ts`, the `packages` endpoints in `src/api/endpoints.ts`, the route table entries, the navigation flag, the fixture and the tests. |

Both tasks cite `docs/salscatersuite_project_structure_workplan.md` sections 2, 3 and 4 as the placement constraint, and both are bound by its alignment gate.

---

## 11. Implementation Confirmations (AGENT 01, Phase 8)

The implementer's open questions were answered as follows, and the answers are now the approved behaviour.

| # | Question | Ruling |
| :--- | :--- | :--- |
| 1 | Should the four capability-gap notices also appear on the tier form? | **Yes.** DEC-067 requires the notice *wherever* the reference shows an unsourced block, and the reference's tier editor shows the food-cost band and the course matrix. `MenuRecipeFormPage` carries the same notices for the same reason. |
| 2 | May the type-tab counts be counted in the browser? | **Yes.** A count is not a monetary figure, and `GET /api/packages` returns the whole catalogue, so the count describes the server's own list exactly. No server-published per-type count is needed. |
| 3 | Do the tab counts describe the whole catalogue or the filtered scope? | **The whole catalogue.** "All Tiers (n)" does not move when a type or the search narrows the list - the delivered `MenuCourseTabs` behaves the same way ("All Dishes (181)" stays fixed). |
| 4 | May the tier type be trimmed before it is sent? | **Yes.** Surrounding whitespace is removed; the value itself is never rewritten to a suggestion (DEC-070). |
| 5 | Is "Highest floor: - heads" acceptable? | **No.** It was corrected: when no tier sets a floor the sub-line reads "No tier sets a minimum headcount" and the unit is suppressed, so a dash is never followed by a unit. |
| 6 | Should the menus scan's bare-benchmark rule (`32\|35`) be added? | **No.** It encodes the *menu* food-cost benchmark. The packages feature has no benchmark, and the general rules (peso sign, thousands separator, decimal, four-digit, percentage, benchmark assignment) are all carried over. |
| 7 | Should the edit form be seeded from the catalogue row instead of a fresh `GET /api/packages/{id}`? | **No.** The form loads the authoritative record, exactly as the menu form loads `GET /api/menus/{id}`. A cached row could be stale. |

## 12. Validation Evidence (Phase 9, AGENT 01)

**Build and suites (re-run independently by AGENT 01 after the review corrections):**

| Suite | Result |
| :--- | :--- |
| `dotnet build src/backend/SalsCaterSuite.slnx` | 0 warnings / 0 errors |
| `dotnet test src/backend/SalsCaterSuite.UnitTests` | **429 / 429** passed |
| `dotnet test src/backend/SalsCaterSuite.IntegrationTests` | **84 / 84** passed (real SQL Server LocalDB, disposable database) |
| `npm --prefix src/frontend/salscatersuite-ui run build` | `tsc` clean, `vite build` clean |
| `npm --prefix src/frontend/salscatersuite-ui run test` | **221 / 221** passed (17 files) |

**Live API reconciliation** (the API was started against a **disposable** LocalDB database - the development database was not touched, and the disposable one was dropped afterwards):

| Check | Expected | Actual |
| :--- | :--- | :--- |
| `GET /api/packages/summary` over 8 tiers (7 priced) | average = 7450.00 / 7 = **1064.29** | `averageAmountPax: 1064.29` (rounded away from zero) |
| The count, the rate range and the floor range | 8 / 7 active / 1 inactive / 7 priced; 100.00 - 1850.00; 10 - 150 | identical |
| `GET /api/packages` | the same population as the summary | 8 tiers, `is_deleted = 0` in the SQL |
| `PATCH /api/packages/{id}/toggle-active` | the summary follows the tier | 6 active / 1 inactive, average unchanged |
| `POST` with a blank description, a negative rate and a minimum pax of 0 | `400 { "errors": [...] }`, nothing persisted | the three exact messages; the catalogue count was unchanged |
| `POST` with a 51-character type | `400` (the column is `nvarchar(50)`) | "Package type must not exceed 50 characters." |
| `POST` with a 50-character type | accepted | `201 { packageId }` |
| `GET` / `PATCH` / `PUT` on a missing tier | `404 { "errors": [...] }` | the envelope on all three |

**Live UI validation** (headless Chrome against the running API and the Vite dev server):

| Check | Result |
| :--- | :--- |
| Zero console errors on `/packages` | **none** |
| The KPI ribbon renders the server's figures | "Configured Tiers 8 Tiers / 7 active / 1 inactive", "Average Per-Pax Rate PHP 1,064.29 / Across 7 priced tiers", "Per-Pax Rate Range", "Minimum Headcount 10 HEADS / Highest floor: 150 heads" |
| The four capability-gap notices are present | present, each naming the missing concept |
| No fabricated content | no food cost, no gross margin, no course list, no venue list, no export and no duplicate affordance appears in the DOM |
| Horizontal overflow (AC-P14) | **none** at 375 / 768 / 1024 / 1440 px - measured as `scrollWidth == clientWidth` at every width, on `/packages`, `/packages/new` and `/packages/:id/edit` |
| The tier form | renders the four labelled fields, the hints, the datalist of the six known names, the `maxlength` of 50 and 250, and the four capability-gap notices |

**Defects found in review and corrected before closing:**

| # | Defect | Correction |
| :--- | :--- | :--- |
| R1 | The validator allowed a 100-character tier type; the column is `nvarchar(50)`, so a 51-100 character type would have reached the database as an unhandled `500` | The limit is now the column limit (50) with the matching message (DEC-075) |
| R2 | The catalogue's list and detail projections did not filter `IsDeleted`, while the summary did - the screen would have shown "Configured Tiers = 3" beside four cards | The soft-delete filter is scoped to the two read projections; the entity fetch used by the booking path is deliberately untouched (DEC-074) |
| R3 | The client-side form messages ("Tier name is required.") differed from the server's ("Package description is required."), and the type input allowed 100 characters | The client now uses the server's wording, and the input carries the column's `maxlength` (DEC-075) |
| R4 | The tier-type chip used the shared `Badge`, which is `whitespace-nowrap`; a 50-character free-text type pushed the card 9px past the viewport at 375 px | A local `PackageTypeChip` keeps the visual language and wraps instead (DEC-076) |
| R5 | `GET /api/packages/{id}` and `PATCH .../toggle-active` answered `404` with an empty body, against contract section 7 | Both now carry the standard `{ "errors": [...] }` envelope; the status codes are unchanged |
