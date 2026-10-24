# Workflow State — Booking Add-on Picker Modal (2026-10-24)

| Field | Value |
| :--- | :--- |
| **Cycle** | 2026-10-24 - Booking Menu Selection: category-filtered Add-ons modal and server-refreshed contract value |
| **Source** | `docs/userupdate.txt` — owner request |
| **Status** | **Closed locally 2026-10-24.** The modal UI and review corrections are delivered; existing booking-add-on API and pricing rules are unchanged. |
| **Specification** | `business/bookings-events-operations-feature-spec.md` §5.4, AC-BKG-88; DEC-170 |
| **Workplan constraints** | §3.2 existing `features/bookings` component and hook; §3.4 existing `/api/addons` and `/api/addons/categories`; §3.5 server-authoritative totals |
| **Stage** | 10 - Close (reviewed, independently validated, status and workplan realigned) |

## Approved scope and implementation plan

- Replace the always-visible full add-on catalogue with a selected-add-ons summary and an explicit **Choose add-ons** action on the existing Booking Menu Selection page.
- Open an accessible modal containing a table of active add-ons, with a category filter. Fetch categories and the add-on master only while the modal is open; use the existing optional `categoryId` filter and required booking `headcount` query parameter.
- Selection is a draft until the existing **Save selection** action submits the full replacement. Show only chosen add-ons in the page summary, preserve editable line totals, and identify unsaved changes.
- Do not calculate authoritative totals in the browser. After the add-on replacement succeeds, re-read booking details and display the server's newly frozen `amountDue` in the ledger and save confirmation (DEC-002, DEC-152).
- Keep the existing route, API surface, folder layout, business calculation, lock behavior, and full-replacement semantics. No backend changes or new project/component folder are planned.
- Accessibility: keyboard focus trap, Escape and close-button dismissal, focus restoration, labeled category control/table/selection controls; locked bookings remain read-only.

## Acceptance and validation plan

1. Initial page render does not request `GET /api/addons` or `GET /api/addons/categories`.
2. Opening the modal lazily loads categories and active add-ons using the booking headcount; category changes request only that category; empty/loading/error states are explicit.
3. Selecting/deselecting rows changes the draft; the page shows only selected lines and amount inputs. Closing/reopening retains the draft; no write occurs until save.
4. A successful save performs the existing full replacement and refreshes booking details; ledger and confirmation show the refreshed server-returned total contract value, not a locally summed estimate.
5. Locked booking is read-only. Keyboard dismissal/focus behavior and existing empty-master behavior remain correct.
6. Run the focused booking page tests, full Vitest suite, and frontend production build; report actual results. Backend tests are not implicated; run only if review finds a backend dependency.

## Delegation brief (issued after Debater approval)

- **AGENT 03:** implement the existing Booking Menu Selection feature only. Read `docs/salscatersuite_project_structure_workplan.md`; placement is constrained by §3.2, routes/API by §3.3–§3.4, and server-authoritative financial display by §3.5. No new routes/endpoints/folders/components outside the existing `features/bookings` boundary. Update tests and report changed files plus executed results.
- **AGENT 01:** maintain this specification, decision log, feature status, and workplan close-out. No business formula is changed.

## Debater Review

- **Decision:** APPROVE
- **Rationale:** The plan is the smallest plan that satisfies the owner's request (`docs/userupdate.txt`) without leaving the documented boundaries. It reuses the already-existing `GET /api/addons` (optional `categoryId`, required `headcount`), `GET /api/addons/categories`, `PUT /api/bookings/{id}/addons` (existing full-replacement semantics), and `GET /api/bookings/{id}` — no endpoint, route, feature folder, or shared component is added, so the workplan §3.2/§3.4 surfaces and §3.5 financial rules are preserved without a realignment cycle. The lazy-load + selected-only-display + server-refresh approach is the only design that satisfies workplan §3.5 (the client never recomputes an authoritative amount); no materially better or lower-risk alternative exists.
- **Findings:**
  - **[Complexity]** No new abstractions. Modal behavior stays inside the existing `features/bookings` boundary, reusing the existing feature-local `BookingAddonsPanel` and the existing `useFocusTrap` hook — no new shared `components/ui/Modal.tsx` (still `PLANNED — not yet implemented` per workplan §3.2) and no new folder. Category filtering reuses the existing optional query parameter rather than a new endpoint.
  - **[Edge cases]** Query behavior is correct: `headcount` is carried on **every** `/api/addons` request including category-filtered ones, so `lineAmountForBooking` remains server-computed (contract §16.1). Draft selection persists across close/reopen and no write occurs until **Save selection**, so the modal cannot silently mutate a booking. The authoritative `amountDue` is read back from `GET /api/bookings/{id}` after the existing `PUT` — no locally summed estimate ever reaches the ledger or the save confirmation. Empty master, loading, and error states are explicit; a locked booking stays read-only.
  - **[Compatibility]** No backend change, no migration, no entity/enum/status/calculation change, and no effect on historical frozen add-on lines, legacy `AspNetUsers` logins, or the preserved legacy `SBOSysTacV2` project. Full-replacement semantics of `PUT /bookings/{id}/addons` are unchanged, so no rollback path is required — the pre-existing selection remains the fallback state.
  - **[Tests]** Acceptance/validation plan is verifiable and covers the six behaviours that matter: no `/api/addons` or `/api/addons/categories` request on initial render; lazy load with `headcount` on the initial and each category-filtered request; draft-only selection with retained draft and no write before save; successful save followed by a booking-details refresh showing the server-returned total; locked-booking read-only; and focus trap / Escape / focus-restoration behavior. The `noHardcodedFigures` guard for `features/bookings` (workplan §3.5) continues to apply. Full Vitest suite and the frontend production build are to be run and the **actual** results reported — no backend suite is required because no backend dependency is introduced.
- **[Scope]** Stays inside the approved feature: existing route, existing API surface, existing folder layout, existing business calculation, existing lock behavior, existing full-replacement semantics. No business formula, KPI definition, or project scope is changed (DEC-002, DEC-152, DEC-170). No component unrelated to the implementation/design workflow is proposed.
- **Notes for AGENT 03 (constraints, not changes):** keep `headcount` on every `GET /api/addons` call; render only server-returned financial figures in the ledger and confirmation; keep the add-on selection state as a draft in the existing feature-local component; do not introduce a shared `Modal`/`Table`/`Select` component or a new folder without an AGENT 01 decision; report the executed Vitest and build results with actual output.

## Delivered and validated

- `BookingAddonsPanel` now exposes selected lines only and opens a responsive category-filterable modal. Master and category requests are lazy; the booking's `headcount` is present on every master request. The selection remains a draft until the existing full-replace save.
- The successful add-on write is followed by one fresh `GET /api/bookings/{id}`; the server-returned `amountDue` refreshes the ledger and confirmation. Failed reads are distinguished from failed writes, retain an explicit stale-total warning, and can be retried.
- Changed product files are confined to the existing workplan §3.2 `features/bookings` page, panel, hook and test files. No route, endpoint, project, migration, shared component, entity or business calculation changed.
- **AGENT 01 independent frontend validation:** focused `BookingMenuPage.test.tsx` **22/22 passed**; full Vitest **25 files, 383/383 passed**; `npm --prefix src/frontend/salscatersuite-ui run build` exited 0, TypeScript **0 errors**, Vite build succeeded with the existing >500 kB chunk-size advisory.
- No backend code changed; backend suites were not rerun for this UI-only change. A clean remote clone was not verified because the working tree contains pre-existing uncommitted/untracked repository work; no commit or push was performed.
- Workplan §3.2/§3.3 documentation and the stale §4 entity/migration inventory were realigned; `implementation/feature-status.md`, traceability and the workplan §9/§10/§11 were updated at Close.

---

# Previous Cycle — Add-on Master Administration (2026-10-24)

## Delivered

- **Backend:** the `Features/Addons` Application module (management list/detail and department queries, create/update/delete commands, `AddonUpsertValidator`, the admin mapper and `AddonNotFoundResult`); the `IAddonRepository` management reads/writes and `IDepartmentRepository`; the `AddonsController` management routes and the new `DepartmentsController`.
- **Endpoints:** `GET /api/addons/manage`, `GET /api/addons/manage/{id}`, `POST /api/addons`, `PUT /api/addons/{id}`, `DELETE /api/addons/{id}`, `GET /api/departments`.
- **Frontend:** the Settings hub (`/settings`), the Add-on Master (`/settings/addons`), the `useAddonManagement` hooks, `src/types/addon.types.ts`, and the Settings nav entry marked implemented.
- **Behaviour:** the delete is a **soft** delete; the description is capped at 150 and the unit at 50 (the column widths); the unit price is non-negative; an optional category/department must exist. The category master and the department lookup remain **read-only** (DEC-169).

## Notes / open

- The master still ships **empty** (BR-13). The owner can now populate it from Settings → Addons (RSK-071).
- The department lookup is empty and has no CRUD in this cycle; the add-on can be saved with no department.
- The booking page's "Add-ons & upgrades" panel reads the same master, so a created add-on becomes selectable on a booking without a code change.

---

# Workflow State - Department & Package Enhancements (previous cycle, closed)

| Field | Value |
| :--- | :--- |
| **Cycle** | 2026-10-24 - Department master, frozen package code, extended location amount, currency inputs |
| **Source** | `docs/userupdate.txt` (the owner's note) |
| **Decisions** | DEC-162..DEC-167 |
| **Rules** | BR-45..BR-48 |
| **Stage** | 10 - Close (delivered and verified) |
| **Status** | **Closed 2026-10-24.** The `Department` master and the two department FKs, the frozen `PackageCode`, the per-guest `Package.ExtendedLocationAmount` (Regular/Premier only, added to the municipality rate in the booking surcharge), and the shared `CurrencyInput` are delivered. No `MaxPax` (DEC-167). Build **0 W / 0 E**, **977/977** unit, **136/136** integration, **366/366** Vitest, **0** TypeScript errors. The migration is additive with a deterministic code backfill; the workplan, data dictionary, business rules, contract, decision log and feature status are realigned. |

## Delivered

- **Backend:** `Department` entity + configuration; `AddonDetail.DeptId` and the `Menu.DeptId` FK; `Package.PackageCode` + `Package.ExtendedLocationAmount`; `PackageCodeGenerator`; the package validator/commands/projections; the add-on master's `deptId`/`deptName`; the quote's `packageCode`/`packageExtendedLocationRate`; the pricing composition.
- **Migration:** `20261024021725_AddDepartmentsAndPackageCodes` - additive; backfills every existing tier's code before the NOT NULL constraint and the filtered unique index; never applied to an existing or production database (DEC-020).
- **Frontend:** `components/ui/CurrencyInput.tsx` + `utils/currencyInput.ts` applied to every money field; the package form's extended-location field shown only for `Regular`/`Premier` and cleared when the type changes away; the code shown in the catalogue, inspector and form.
- **Tests:** 36 new backend unit tests (977 total), 3 new integration tests (136 total), the frontend suite's existing assertions updated to the formatted values and the code (359 total).

## Notes / open

- The `Department` master ships **empty** and has no CRUD endpoint this cycle; populating it is a separate owner task.
- The menu surface gains the **FK only**; it does not expose `deptId` in its DTOs yet.
- The add-on master remains **read-only** (RSK-066); the `deptId` it now publishes will be populated by the future catalogue writes or the legacy import.

---

# Workflow State - REQ-BKG-06 / 07 / 08 Bookings & Events Operations (previous cycle, closed)

| Field | Value |
| :--- | :--- |
| **Cycle** | 2026-10-22 - Bookings & Events Operations: dish selection, add-ons, automatic discounts |
| **Specification** | `business/bookings-events-operations-feature-spec.md` v1.0 |
| **Contracts** | `architecture/approved-api-contracts.md` §14 (extended) · **§15** (the dish selection) · **§16** (the add-ons) |
| **Agent** | AGENT 01 (BI Architect) |
| **Stage** | 10 - Close (delivered, verified and realigned) |
| **Status** | **Closed 2026-10-22.** Both delegations delivered; AGENT 01 reviewed them, fixed the defects the review found, re-ran every suite itself, and ran a **61-assertion live pass** against a disposable database. Backend build **0 W / 0 E**, **941/941** unit, **133/133** integration, **359/359** Vitest, **0** TypeScript errors. The workplan and `implementation/feature-status.md` are realigned. The two plan-sweep defects and one contract ambiguity found during delivery are recorded in the delivery log below. |

---

## 1. Verified Facts (Stage 1 - Inspect)

- The **repository was restructured under the previous cycle**: the Git root was promoted to the monorepo root and the two test projects moved from `tests/` to `src/backend/` (**DEC-125 / DEC-126**). The workplan was realigned for it; the **id space moved with it** — the owner's decisions took DEC-125/126 and the owner's risks took RSK-059/060/061. **Every id in this cycle was renumbered after the collision was found**; the new decisions start at **DEC-127** and the new risks at **RSK-062**.
- `Menu.MenuId` is a **string** - the catalogue's key is not an integer.
- `PackageCourseRule` (the tier's course rules) **already exists** with `RequiredQuota`, `PortionGrams`, `RuleDescription`, `InclusionTag` and `UpgradeAddonId`; the **qualified pool** is derived on every read and never stored (BR-19, DEC-083).
- The **BR-15 quota engine already exists** and is tested in the packages workbench - "exactly `RequiredQuota` dishes per course, and no dish from a course the tier has no rule for".
- The **selection's food cost and gross profit** already exist - the packages' selection preview (contract §13.3).
- The frontend **per-course dish picker already exists** - `PackageSelectionSimulator` (the quota, the pool, the chosen dishes, the verdict).
- `AddonDetail` exists (description, unit price, unit) but has **no category**, and `Booking` has **no** add-on link (RSK-048) and **no** dish link (RSK-049).
- `PackageType` has the members `Regular, Wedding, Vip, Premier, Pm, Sd` - there is **no** `Premium`.
- **BR-01's gross formula has carried an `AddonsTotal` term since the beginning**, and `BookingAmountDueCalculator` has carried an `addonsTotal` parameter since the beginning - it has been `0m` only because **nothing could create one** (RSK-002). This cycle makes BR-01's third term reachable for the first time.
- The **dev environment was restored** after the previous cycle's file churn had torn it down: the API is on `:5149` and the UI on `:5173`, and the additive migration is applied to the development database.

## 2. Assumptions (explicitly not treated as facts)

- No assumption is made about the **add-on master's population** - BR-13 forbids seeding business data and the legacy import has not started, so the master **ships empty** (RSK-066).
- No assumption is made about a **per-event margin** being auditable - the dish food costs are manually entered decimals (DEC-062) and the recipe engine is unbuilt (REQ-MNU-03).
- No assumption is made about the **90-99 head gap** being an omission; it is implemented literally and put to the owner (RSK-064).

## 3. The Owner's Rulings (Stage 4 - Approve, 2026-10-22)

Seven answers, recorded in the specification's section 1.2 and as DEC-127..DEC-142:

| # | Ruling | Decision |
| :--- | :--- | :--- |
| 1 | The menu **may** change after the deposit, within a **configured** window | DEC-128 |
| 2 | A dish selection **never** reprices the booking | DEC-129 |
| 3 | The selection is recorded **per booking** | DEC-127 |
| 4 | The **headcount bands**: `[80, 90)` earns 10.00/head, `[100, 1000)` earns 20.00/head | DEC-136, DEC-137 |
| 5 | The **add-on master** and the booking's frozen lines | DEC-139, DEC-140 |
| 6 | The out-of-town amount applies **only** to `Regular` and `Premier` | DEC-141 |
| 7 | After the create, **redirect to the menu-selection screen** | - |

Derived by AGENT 01: the bands are read literally with the 90-99 gap real (DEC-136); the band discount is a fixed amount against the gross, capped (DEC-137); one discount applies and a code replaces the band (DEC-138); the add-on line freezes description, amount and unit (DEC-139); the line's amount is its commercial truth, no quantity column (DEC-140); the tier restriction is enforced **server-side** (DEC-141); "Premium" means `Premier` (DEC-142); **the add-on total joins the gross** because BR-01 has always said so (DEC-135).

## 4. Deliverable Summary (Stage 3 - Specify)

| # | Deliverable | Layer |
| :--- | :--- | :--- |
| 1 | `BookingMenu`, `BookingAddOn`, `AddonCategory` + `AddonDetail.AddonCategoryId` - one additive migration | Domain / Infrastructure |
| 2 | The selection validator (BR-15, reused), the lock rule, the band discount, the tier restriction | Application |
| 3 | The read/replace commands for the selection and the add-ons; the extended create | Application |
| 4 | The add-on master reads; the register's conditional estimate; the summary's locked/open and band-discount counts | Application |
| 5 | Contract §15 and §16; the six new endpoints | WebApi |
| 6 | The menu editor, the add-on editor, the dossier card's actions, the workbench's pickers and area filter, the post-create redirect | Frontend |
| 7 | BR-35..BR-44, KPI-40..KPI-42, DEC-127..DEC-160, RSK-062..RSK-068, Scenarios 75-99 | Governance |

**No new project. No new root folder. No new top-level package.** The migration adds **three tables** and **three columns** on existing tables - `Booking.AddonsTotal`, `AddonDetail.AddonCategoryId` and `AddonDetail.PricingBasis` (DEC-151, DEC-158).

## 5. Decisions Recorded

DEC-127 (the selection junction) · DEC-128 (the menu-change window) · DEC-129 (a selection never moves the money) · DEC-130 (the tier's own rules validate it) · DEC-131 (the standard run is the default) · DEC-132 (the register's conditional estimate) · DEC-133 (the edit surface is the menu only) · DEC-134 (replaced, never patched) · DEC-135 (the add-on total joins the gross) · DEC-136 (the bands, literally) · DEC-137 (the band discount's arithmetic) · DEC-138 (one discount applies) · DEC-139 (the frozen add-on line) · DEC-140 (the amount is the truth) · DEC-141 (the tier restriction) · DEC-142 ("Premium" means `Premier`).

Recorded by the plan-review disposition (all closed in the artefacts, see section 10 below): DEC-143 (the VAT-inclusive disclosure and its configured rate) · DEC-144 (the UOM as pricing basis - superseded by DEC-158) · DEC-145 (the derived batch mass) · DEC-146 (the per-dish prep instruction) · DEC-147 (the tabs derived from the tier's rules) · DEC-148 (the BEO endpoint) · DEC-149 (the cutoff is the only lock) · DEC-150 (the capability gaps stay gaps) · DEC-151 (the stored `Booking.AddonsTotal`) · DEC-152 (the add-on write re-freezes the money) · DEC-153 (the quote prices selections and add-ons) · DEC-154 (the derived discount source) · DEC-155 (the prep instruction travels in the request; omitted clears) · DEC-156 (the pinned tier match) · DEC-157 (`GET /api/packages?areaId=`) · DEC-158 (the explicit pricing-basis column and the line-total semantics) · DEC-159 (the selection-less BEO prints the rules, never a dish list) · DEC-160 (the server publishes the band label and the VAT rate).

## 6. Constraints Carried Into Delegation

- The Application project may not reference EF Core or Identity (DEC-037); no repository interface exposes a `DbSet`, an `IQueryable` or a `DbContext`.
- The **shared calculator's argument set** does not change. The add-on total is read from the booking's **frozen lines**, and the surcharge from the **stored** column - not inferred.
- **One rounding** (DEC-115), away from the midpoint, applied once, in the Application layer.
- The **pre-existing suites must stay GREEN**: the baseline is **823/823** unit and **133/133** integration.
- The **DTO projection guard** must still pass with the new read models.
- The migration is **purely additive**; it is **generated and script-reviewed only** and is never applied to an existing or production database (DEC-020).
- **No business data is seeded** (BR-13).
- The workplan must be realigned in the same cycle.

## 7. Acceptance Criteria

AC-BKG-51 .. AC-BKG-87 in `business/bookings-events-operations-feature-spec.md` section 7.
Acceptance Scenarios 75-99 in `implementation/acceptance-test-scenarios.md`.

## 8. Risks

RSK-062 (a dish swap moves the margin without moving the price) · RSK-063 (the catalogue may re-assign a dish to another course) · RSK-064 (the 90-99 head gap) · RSK-065 (the `addonsTotal` term's first production use) · RSK-066 (the add-on master ships empty) · RSK-067 (the pricing basis rests on a catalogue column no data populates yet) · RSK-068 (the tier test is exact and the type vocabulary is unpinned).

---

## Debater Review — REQ-BKG-06 / 07 / 08 Bookings & Events Operations (2026-10-22)

**Decision: PROPOSE_BETTER_PLAN**

**Rationale:** the commercial core is well-founded and mostly proven by the owner's own artefacts — the add-on total in the gross, the VAT-inclusive disclosure, the headcount bands, the per-event prep instruction and the derived batch mass all check out against the design and the operational note — but the **add-on total has no path into the authoritative calculator** (F1, F2), the **quote and the create would diverge** (F3), **KPI-42 has no stored source** (F4), and four contract/UI surfaces are unimplementable as written (F5, F6, F7, F8). Twelve blocking findings, all fixable in the specification, the contract and the decision log before delegation; nothing here calls the commercial design into question.

### What is sound — must not change

| # | Item | Evidence it rests on |
| :--- | :--- | :--- |
| S1 | **DEC-135 — the add-on total joins the gross.** BR-01 §1.1's formula has always read `BasePackage + ExtendedAreaFee + AddonsTotal + OtherCharges` and the calculator has always carried `addonsTotal` (`BookingAmountDueCalculator.cs:50`). | The design's own ledger proves it: `Base 10-Course Package (120 × ₱820) ₱98,400.00` + `Selected Add-ons ₱15,900.00` + `Mayorga Out-of-Town Fee (120 × ₱50) ₱6,000.00` = `Total Contract Value ₱120,300.00` (`Booking_Menu_Selection/code.html`, the Contract Ledger block). The add-on subtotal ₱15,900.00 is exactly the two ticked rows. |
| S2 | **DEC-143 — the tax is a VAT-inclusive disclosure, the rate is configuration.** | The design prints `Standard 12% EVAT Included ₱12,889.29` **above** a total of ₱120,300.00 — 120,300 − 120,300/1.12 = 12,889.2857 → **12,889.29**. The VAT is inside the total, not added to it. The rate stays configuration and absent by default (DEC-008 unresolved). It must never touch `NetAmountDue` nor `Booking.TaxAmount`. |
| S3 | **DEC-144's substance — the UOM is the pricing basis.** | The design's add-on block: `+₱120/pax → ₱14,400.00` and `+₱150/pax → ₱18,000.00` and `+₱65/pax → ₱7,800.00` against 120 guests, beside `₱1,500.00 — Flat rate`. Per-guest lines are `unit × heads`; the flat line is not. (Only the **token matching** is defective — F8.) |
| S4 | **DEC-136 / DEC-137 / DEC-138 — the bands, literally; a fixed amount capped at the gross; one discount applies.** | The operational note verbatim: `>=80 and <90 : discount amount 10.00 … 89 x 10 = 890.00` and `>=100 and <1000 : discount amount 20.00 … 100 x 20 = 2000.00`. The 90-99 gap is the owner's own wording, not an omission. |
| S5 | **DEC-127 / DEC-134 — the junction, replace-not-patch, and the filtered unique index.** | The DEC-088 precedent is real and as-built: `PackageCourseRuleConfiguration.cs:61-64` — `HasIndex(e => new { e.PackageId, e.CourseId }).IsUnique().HasFilter("[is_deleted] = 0")`. The same shape gives the resurrect path. |
| S6 | **DEC-146 — the prep instruction is per booking, per dish.** | The design carries **ten** per-dish inputs — `placeholder="Add specific chef prep instruction (e.g. Walnuts on side for allergy table)..."` on every dish card (`code.html` lines 145, 187, 229, 270, 311, 352, 393, 434, 475, 516). No existing column covers it: `Menu.Note` is a catalogue column, `Booking.SpecialInstructions` is a booking-level free text (`BookingConfiguration.cs:67`). |
| S7 | **DEC-147 — the tabs are derived from the tier's rules.** | `CourseCategory` carries `CourseId` and `CourseName` and **nothing else** (`CourseCategory.cs:5-11`) — there is genuinely no grouping attribute. One tab per rule is derivable from `PackageCourseRule`. |
| S8 | **DEC-145 — the batch mass is derived, not invented.** | The design's Batch Yield panel prints `21.6 kg`, `19.2 kg`, `20.4 kg`, `24.0 kg` — exactly `portion × 120 ÷ 1000` for the 180 g, 160 g, 170 g and 200 g courses it lists. The formula is the design's own. |
| S9 | **DEC-128 — the lock window is configuration, absent by default; the lock date is never `null`.** | The design's `BEO Cutoff: Nov 21, 2026` for a `Nov 28, 2026` event is 7 days — the shape of the owner's "N days". The days figure being `null` while the date is always published is the right null discipline. |
| S10 | **The quota rule's membership half, the food-cost arithmetic and KPI-40's formula.** | `PackageCourseRulePreviewValidator.cs:32-57` already refuses a null selection, a repeated dish, an unknown dish and a dish that does not belong to the named course; `PackageCostingCalculator.cs:195-211` is the selection's food cost and `:183-186` the margin. Reusable as-is. |
| S11 | **DEC-131 — the standard run is the default *as a cost*.** | BR-19 (`business-rules.md:179`) defines it as `Σ over rules of the mean of the rule's costed pool`. As a **cost figure** it is sound and already published. (As a **dish list** it is not — F10.) |
| S12 | **The scope discipline** — the general booking edit stays out (DEC-133), the recipe engine stays out (REQ-MNU-03), the master administration stays out (RSK-066), and the four design-only artefacts (allergens, HACCP stamp, chef sign-off, tasting sheet) stay gaps (DEC-150). | The exclusions in the specification's §1.5 are honest and match the model. |

### Blocking findings

**F1 — The add-on total has no path into the authoritative calculator; DEC-099 breaks the moment an add-on exists.**
- **Claim:** the plan's constraint §6 — "The **shared calculator's argument set** does not change. The add-on total is read from the booking's **frozen lines**."
- **Evidence:** `SalsCaterSuite.Application/Services/BookingAmountDueCalculator.cs:99-110` — the authoritative overload hardcodes `addonsAmount: 0m, // no Booking ↔ AddonDetail link exists (RSK-048)`; `Booking.cs:86-96` — the surcharge is a column of its own precisely because "`Subtotal` conflates the surcharge with the add-on total and the other charges"; `Booking.cs:242` — `RecalculateFinancials` folds the add-on total into `Subtotal`, which **no** calculator read consults (`Calculate(Booking, …)` reads `PackageAmount`, `ExtendedAreaAmount`, `DiscountPercentage`, `DiscountAmount`, `TaxAmount` only); `Booking.cs` has **no** stored add-on column; the migration is "three tables and **one** nullable column" on `AddonDetail` (spec §3.3/§3.5, AC-BKG-67); `BookingPricingService.cs:134` computes `gross = Round2(baseAmount + extendedAreaAmount)` with no add-on term and `BookingPricingInput` (`:11-43`) has no add-on member.
- **Consequence:** the create path would freeze an `AmountDue` that *includes* the add-on total (`CreateBookingCommand.cs:198-202` passes `addonsAmount`), while every read path — the register, the ledger, the details, the customer statement, the calendar, the dashboard — recomputes the net due **without** it. The stored `AmountDue` and the calculator's `NetAmountDue` would differ by exactly the add-on total, the register's totals would stop reconciling with the dashboard's KPI-12 (AC-BKG-05, BR-27), and RSK-065's promised test ("the frozen `AmountDue` equals the calculator's figure for a booking with an add-on, a surcharge **and** a band discount together") **cannot pass**.
- **Required change:** decide and document the mechanism — either (a) a frozen `Booking.AddonsTotal decimal(18,2) NOT NULL DEFAULT 0` that the calculator reads (a **fourth** column; AC-BKG-67 and the migration count must be amended), or (b) an explicit change to the calculator's authoritative overload's argument set with `BookingPricingService` carrying the add-on total. Then amend BR-25's `AddonsAmount = 0` line (`business-rules.md:312`) and RSK-065's mitigation, and stop asserting that the argument set does not change unless (a) is chosen **and** the calculator reads the stored column.

**F2 — An add-on write re-prices a confirmed, deposited booking, and no rule, formula or guard is specified.**
- **Claim:** BR-41 — "the booking's total moves **only** when the operator adds or removes an add-on"; BR-39 — the add-ons follow the same menu-change window, so they may change before the lock date.
- **Evidence:** `Booking.RecalculateFinancials` (`Booking.cs:233-246`) is the **only** place `Subtotal`, `DiscountAmount` and `AmountDue` are written, and it applies no rounding; the band discount is **frozen** at creation (BR-38, DEC-137) and capped at the gross (BR-02); the specification's §3.9 validation summary and contract §16.4 say **nothing** about the booking's money after an add-on write; the overpayment guard (DEC-118) guards a *payment*, not a *re-price*.
- **Consequence:** the implementer must guess, and each guess is a money error. If the money is not re-frozen, the add-on total never reaches the total (F1). If it is: the frozen band discount may exceed the new gross (the cap must be re-applied), the stored deposit may no longer match the new net due, and the booking can become **overpaid** (collected > net due) — which BR-05 forbids and which no guard covers. The register's `Settled` state, the AR totals and the customer statement would then move on an operator action the plan does not even name.
- **Required change:** state in BR-39/BR-41, in the contract's §16.4 and in AC-BKG-74/§8.1 exactly what an add-on write does to `Subtotal`, `DiscountAmount`, `AmountDue` and the deposit; state whether the frozen band discount is re-capped at the new gross; and state the overpayment behaviour (refuse the write with a `400`? accept an advance credit? DEC-007 is still unresolved).

**F3 — The quote accepts neither the selection nor the add-ons, so the quote and the create diverge.**
- **Claim:** BR-31 — "The quote's figures are computed by the **same code path** the create path uses, so a confirmed booking's frozen snapshot equals the quote that priced it" (`business-rules.md:404`); RSK-065 repeats it as its mitigation.
- **Evidence:** contract §14.3's `BookingQuoteRequestDto` (`approved-api-contracts.md:1341-1346`) carries no `selections` and no `addons`; the specification's §4.1 extends only the **create**; `BookingPricingService` has no add-on input.
- **Consequence:** the workbench's new dish picker and add-on picker (spec §5.2) cannot be priced before the create — the operator sees a total that is short by the add-on total, and the band discount capped against a gross that is not the one the booking will carry. The quote's `GrossAmount` and the created booking's `AmountDue` would differ: a direct BR-31 breach that the live end-to-end validation would catch on day one.
- **Required change:** extend `BookingQuoteRequestDto` with `selections?` and `addons?` (the same shapes as the create) and price them through the same path — or amend BR-31 and RSK-065 and state plainly that the quote is a tier-only estimate that excludes the add-ons.

**F4 — The discount source has no stored home, so KPI-42 cannot be computed.**
- **Claim:** KPI-42 counts "the bookings whose discount came from the **headcount band**"; its formula is written over `b.DiscountSource` (`business/kpi-catalog.md:499`) and its note says the source "is published … precisely so this question can be answered **without a second money notion**".
- **Evidence:** `Booking` stores `DiscountCode` (nullable, `Booking.cs:84`) and `DiscountType` (`None | Percentage | FixedAmount`, `DiscountType.cs:3-8`) — there is **no** source field and no `HeadcountBand` member; the migration adds no `Booking` column; contract §14.1's `BookingRegisterItemDto` field list (`approved-api-contracts.md:1289-1301`) has no `discountSource`, and the specification's §4.1 adds the add-on count and the selection state but **not** the source.
- **Consequence:** the summary would have to *infer* the source (`code present → Code; else the band qualifies → HeadcountBand`). That inference is defensible today (a fixed discount without a code can only have come from the band) but it is an inference, it is unstated, and it silently mislabels any booking whose discount came from anywhere else — on the one KPI whose entire purpose is to be auditable.
- **Required change:** pin the storage — a frozen `Booking.DiscountSource` (int: `None | HeadcountBand | Code`) written once at creation, with the migration count amended — **or** state the inference rule explicitly with its blind spots; and publish the source on the register item and in the summary so KPI-42 is server-computed rather than guessed.

**F5 — The prep instruction has no place in the request contract, and the replace semantics would erase it.**
- **Claim:** AC-BKG-78 and Scenario 92 — the instruction is stored per booking, per dish, travels with the selection and never touches the catalogue (DEC-146).
- **Evidence:** contract §15.2's request body is exactly `{ "selections": [ { "courseId": 0, "menuId": "DS-101" } ] }` (`approved-api-contracts.md:1481-1485`) — **no** `prepInstruction`; contract §15.1's `selections[]` element (`{ courseId, courseName, menuId, menuName, foodCost }`, `:1464`) has **no** `prepInstruction`; the design shows the input on every dish card (S6).
- **Consequence:** the instruction can never be written — AC-BKG-78 and Scenario 92 are unimplementable. And because a `PUT` replaces the whole set (DEC-134), even a side-effect write would be destructive: every save that omitted the instruction would wipe the one the chef typed for Table 4.
- **Required change:** extend the selection request **and** response element with an optional `prepInstruction`, and pin what an omitted member means on a replace — preserve the stored value, or clear it. State which, and pin it with a test.

**F6 — The tier-type match for DEC-141 is undefined against a free-text column.**
- **Claim:** DEC-141 / BR-40 / AC-BKG-72 / Scenario 85 — the out-of-town amount applies **only** to `Regular` and `Premier`, and a surcharge on any other type is a `400` ("a rule that is only hidden is not a rule").
- **Evidence:** `Package.PackageType` is a plain free-text `string` (`Package.cs:8`); DEC-070 deliberately refuses to restrict it — "the column is free text and the legacy import may carry other values" (`PackageRequestValidator.cs:14-16`); the delivered fixtures carry `"Premier Buffet"` (`src/test/bookingFixture.ts:84`), `"Silver Buffet"` (`BookingRepositoryTests.cs:71`), `"Buffet Premium"` (`PackageRepositoryTests.cs:31`) and `"Corporate"` (`GetBookingDossierQueryTests.cs:337`).
- **Consequence:** with an exact match, a tier named **"Premier Buffet"** is refused the surcharge it is entitled to — a `400` on a legitimate sale, on the confirmed-booking path. With a "contains" match, "Buffet Premium" is treated as Premier (DEC-142 says "Premium" means `Premier`, so that reading is arguable) — but it must be **chosen**, not assumed by each implementer.
- **Required change:** pin the match (recommended: trimmed, ordinal-ignore-case, equality with the two enum names `Regular` and `Premier` verbatim; every other stored value — including "Premier Buffet" — is **not** a surcharge tier) and state the commercial consequence for the existing catalogue. If a prefix/substring match is intended, list the accepted tokens explicitly.

**F7 — The workbench's "filter by Package Area" has no server support anywhere in the plan.**
- **Claim:** spec §5.2 — "Column 1 gains the **area filter**, shown only for the `Regular` and `Premier` types. Selecting an area filters the tier list by the areas the tier covers (the coverage matrix of REQ-PKG-02)"; AC-BKG-72 and Scenario 85.
- **Evidence:** the coverage repository exposes only `GetLiveCoveragesAsync(packageId)` (`IPackageAreaCoverageRepository.cs:23`) — there is **no** area → packages query; `IPackageRepository.GetProjectionListAsync(bool? activeOnly)` (`IPackageRepository.cs:21`) has no area filter; the workbench reads only `GET /api/packages` and `GET /api/areas` (`useBookingWorkbench.ts:157-186`); the plan's endpoint list adds seven endpoints and **none** returns the tiers covering an area. The owner's note is explicit: "Select Area based on you event venue — Packages will display by based on the selected area" (`Bookings and Events Operation.txt` §2.1 a1).
- **Consequence:** the control cannot be built as specified without either N per-tier coverage calls from the browser (prohibited in spirit by the workplan's client rules) or a server capability the plan does not contain. The owner's requirement is not optional.
- **Required change:** add the server capability to the plan — a `GET /api/packages?areaId=` (or an area-coverage projection published with the tier list) — and list it in the contract, in the client endpoint table and in the workplan §3.4.

**F8 — The UOM → pricing-basis match is undefined against free text, and it is a money rule.**
- **Claim:** DEC-144 / AC-BKG-79 — a UOM of `pax` means per guest (`amount × heads`); **any other** UOM means a flat line amount.
- **Evidence:** `AddonDetail.Unit` is a **nullable free-text** `nvarchar(50)` (`AddonDetail.cs:10`; `SalsCaterSuiteDbContext.cs:95`); the master ships **empty** (RSK-066) and the legacy import has not started (DEC-032), so **no data pins the vocabulary**; the design prints "+₱150/pax" and "Flat rate" as *labels*, not as a catalogue token, so the stored `Unit` values are unknown.
- **Consequence:** "pax", "Pax", "PAX", "per pax", "head", "guest", "pax/head" and `null` are all plausible in the legacy catalogue. A case-sensitive comparison silently charges a **per-guest** add-on as a **flat** line (₱150 instead of ₱18,000) — or multiplies a flat line by the headcount. That is a money error on a signed contract, and it stays invisible until the import lands.
- **Required change:** pin the token set and the normalisation (trim + case-insensitive) and state what `null` means (flat, under the current default) — **or**, better, replace the string test with an explicit `pricingBasis` column on `AddonDetail` and admit the second new column in the migration count. DEC-144's *substance* (S3) is proven and must not change; only the token test is defective.

**F9 — The request's `amount` semantics contradict the UOM multiplication, and BR-01's own formula says "quantity × unit price".**
- **Claim:** DEC-140 — "the **amount** on the line is the commercial truth, no quantity column"; contract §16.4 — `{ "addons": [ { "addonId": 3, "amount": 1500.00 } ] }` and "the operator confirms the **amount**"; contract §16.3/§4.3 — "a UOM of `pax` means the master's amount is per guest and the line is `amount × heads`".
- **Evidence:** BR-01 §1.1 defines `AddonsTotal = Σ (AddonQuantity × AddonUnitPrice)` (`business-rules.md:11`) — a **quantity** term that the plan removes; Scenario 91 expects the frozen line amount to be **18,000.00** for a ₱150 `pax` add-on on 120 guests.
- **Consequence:** three incompatible readings of `amount` — the catalogue unit amount (the server multiplies), the line total (the server does not multiply), or an operator's negotiated line amount (the server accepts it as-is). Meanwhile the frozen `BookingAddOn.amount` is the **line total** (18,000.00) while the frozen `unit` still reads "pax", so the line reads "18,000.00 pax" — an ambiguous record on a signed contract.
- **Required change:** state the request semantics precisely. The cleanest shape is that the request carries `addonId` only and the server derives the line total from the catalogue's amount and the UOM (with an explicit, separately named override field if a negotiated amount is wanted); and amend BR-01 §1.1's `AddonsTotal` definition so the rule and the implementation say the same thing.

**F10 — The BEO cannot print the selection, and the "standard run" is not a dish list.**
- **Claim:** contract §15.5 / DEC-148 — the BEO is rendered from the booking's **selection**; a booking with **no** selection prints the tier's **standard run** as the dish list; AC-BKG-82 and Scenario 93.
- **Evidence:** `KitchenBeoDocument`'s constructor is `(BookingDto booking, List<MenuDto> menuItems)` (`KitchenBeoDocument.cs:14`) and its "Special Notes" column renders `item.Note ?? "Standard Prep"` (`:86`) — the **catalogue's** note; the per-event `prepInstruction` the selection carries has nowhere to go; BR-19 defines the standard run as `Σ over rules of the mean of the rule's costed pool` (`business-rules.md:179`) — a **cost figure**; the model holds **no** canonical "standard" dish set (the design's own Batch Yield panel lists four course masses plus a "4 Full Pans" line — the pans are a recipe-engine artefact, DEC-145).
- **Consequence:** (a) a selection-less booking has **no defined dish list** — the BEO would print nothing or invent a dish (the "cheapest dish in the pool" is not a modelled rule); (b) the kitchen's prep sheet would print the catalogue's ingredient note instead of the per-event instruction the operator typed for that table — the very thing the chef needs; (c) the document has no place for the add-on lines the kitchen may have to staff for.
- **Required change:** either drop the "standard run as the dish list" claim from §4.2a / §15.5 / Scenario 93 and define the selection-less BEO explicitly (a document that states the event runs the tier's standard run, with no dish list), or define the rule that derives the standard dish set; and extend `KitchenBeoDocument` to carry the per-event prep instruction (and decide whether the add-on lines appear on it).

**F11 — The client would have to hardcode the band bounds and the VAT rate.**
- **Claim:** spec §5.2 — column 3 shows the band discount line with its source ("*Automatic discount — 100 to 999 heads, ₱20.00 per head*"); spec §5.3 — the ledger shows the VAT disclosure; the design prints "Standard **12%** EVAT Included".
- **Evidence:** the quote publishes `discountSource` and `discountBandRatePerHead` but **not** the band bounds and **not** a band label (`approved-api-contracts.md:1350-1363` plus the spec's §4.1 extension); the VAT **rate** is configuration and is published nowhere; the bookings feature's `noHardcodedFigures` test fails any file containing the peso sign, a decimal literal, a four-digit literal or a percentage literal (`src/features/bookings/__tests__/noHardcodedFigures.test.ts:63-86`).
- **Consequence:** the **band boundaries** — a business rule — would be duplicated in the browser and on the server, and the ledger's rate would be a literal. The feature's own test would fail on delivery, and the two copies of the ladder would drift the first time the owner moves a band.
- **Required change:** publish the band label (or the bounds) and the VAT rate from the server — e.g. `discountBandLabel` and `vatPercentage` on the quote — and have the client compose the wording from them. The rate per head may be composed from `discountBandRatePerHead`; the **bounds** may not.

**F12 — The contract was not extended, and the §15.1 payload puts three figures at the wrong level.**
- **Claim:** the cycle's artefacts list "contract §14 (**extended**)" and spec §4.2 says the payload "also carries, **per course rule**: `batchMassKg` … `prepInstruction` … `includedVatAmount`".
- **Evidence:** contract §14.1's item fields (`:1289-1301`), §14.2's summary (`:1317-1327`) and §14.3's quote response and field notes (`:1350-1371`) contain **no** `addonCount`, `selectionState`, `discountSource`, `discountBandRatePerHead`, `includedVatAmount` or `batchMassKg` — the §14 extensions exist **only** in the specification; §15.1's field table (`:1452-1474`) lists `batchMassKg` (per **course**), `prepInstruction` (per **dish**) and `includedVatAmount` (per **booking**) as **top-level** members of `BookingMenuSelectionDto`; §15.3 (`:1493-1499`) adds `selections?` to the create but **not** `addons?`.
- **Consequence:** an implementer reading the contract — the authoritative API document — implements the **old** §14 and a §15 payload that cannot type-check (one scalar for a per-row value). The three §14 extensions would be delivered inconsistently or not at all, and the DTO projection guard would be written against a shape that is not the agreed one.
- **Required change:** apply the §14 extensions to `architecture/approved-api-contracts.md` §14.1/§14.2/§14.3/§14.4; re-shape §15.1 so `batchMassKg` sits on each `courses[]` element, `prepInstruction` on each `selections[]` element and `includedVatAmount` at the top level; and add `addons?` to §15.3.

### Non-blocking clarifications

- **M1 — The BR-15 engine is half reusable, and the plan overstates it.** Spec §1.4 says the quota engine "**Exists** … **Reused unchanged**". The membership / duplicate / existence half does exist and is portable (`PackageCourseRulePreviewValidator.cs:32-57`); the **quota** is a `private static` method that publishes a verdict rather than refusing (`PreviewPackageCourseRuleQuery.cs:163-213`), and it counts the **client's claimed** `courseId`, not a frozen one. Extract **one** shared BR-15 validator so the preview's verdict and the booking's refusal cannot drift — and correct §1.4's wording.
- **M2 — "Selection state" needs a definition.** `None | Selected | Locked` (spec §4.1): is `Selected` "one or more live rows"? does `Locked` override `Selected`? Publish exactly one state, never two flags.
- **M3 — `batchMassKg`'s rounding is undefined, and a zero portion is a null trap.** DEC-003 rounds "money and percentages" to two decimals; a kg figure is neither. State the rounding, and state whether a rule whose portion is **0 g** publishes `0.00` kg or `null` — `PackageCourseRule.cs:36` permits `0` and `null` ("not specified").
- **M4 — The new column lengths and the FK delete behaviours are unpinned.** The spec declares `BookingMenu.menuId` as `nvarchar(100)` while `Menu.menuid` is `nvarchar(50)` (`MenuConfiguration.cs:14`; `business/data-dictionary.md:109`). Align to 50 (or amend the dictionary), and pin the delete behaviours: `Booking` → **Cascade** (stated), `Menu` / `CourseCategory` / `AddonDetail` → **Restrict**, as `PackageCourseRuleConfiguration.cs:44-57` does.
- **M5 — The three tables' names are not in the data dictionary.** The conventions are `pcr_id` / `p_id` / `pac_id` (`PackageCourseRuleConfiguration.cs:28-36`). `business/data-dictionary.md` must gain the three tables — and the `AddonCategory` master — in the same cycle (§5 of the workplan).
- **M6 — The two new configuration keys need a home in the Application layer.** `IBookingPolicyProvider` (`IBookingPolicyProvider.cs:17-31`) has two members and a `DefaultBookingPolicyProvider` that returns `null` for both. State whether it gains `MenuSelectionLockDaysBeforeEvent` and `VatPercentage` (both `null` by default) or a second provider is added.
- **M7 — The workbench has no package-type filter today, and the "area filter" is a different control from the existing municipality selector.** The workbench reads `GET /packages` and lets the operator pick a tier (`useBookingWorkbench.ts:157-168`); the owner's note keys the area dropdown on the **selected type** (`Bookings and Events Operation.txt` §2.1). Say how the type is chosen, how the dropdown's visibility is driven, and whether the existing **municipality** selector (`WorkbenchLogisticsBlock`) is also hidden for a non-surcharge tier — DEC-141's `400` would otherwise break the delivered workbench for a Wedding tier, which is the common case.
- **M8 — The register population must load the new collections.** `BookingRepository.GetRegisterPopulationAsync` is `AsNoTracking()` + `Include` (`:146-158`); the add-on count and the selection state need the two new navigations loaded, and DEC-132's conditional estimate is now computed **per booking** with a selection — which breaks the RSK-054 "once per distinct tier" optimisation for the bookings that have one.
- **M9 — Concurrency on the replace path.** Two operators replacing the same selection concurrently can race the soft-delete/resurrect path; `Booking.RowVersion` is a concurrency token on the booking, not on the selection rows (`BookingConfiguration.cs:112-115`). State the behaviour (last write wins, or a `409`).
- **M10 — The frontend test plan names a page that does not exist.** §8.3 lists `BookingAddonsPage.test.tsx`, but §5.4 puts the add-ons in the menu editor's right column and §5.1 defines only `/bookings/:transactionId/menu`.
- **M11 — KPI-40's margin basis should be the booking's frozen rate.** The formula is written over `AmountPax` (the tier's **current** rate, `kpi-catalog.md:473`); a booking created with an override or a tier since re-priced would publish a margin that moves with the catalogue — a BR-04 leak for a per-booking figure. Use the frozen `Booking.PricePerHead` and state the basis.
- **M12 — The add-on category is unreachable in this cycle.** The master is read-only (RSK-066), so no add-on can ever carry a category and `GET /api/addons/categories` is permanently empty; AC-BKG-73 is vacuously satisfiable. Say so plainly instead of implying a working grouping.
- **M13 — The governance record is stale.** `WORKFLOW_STATE.md:1` says "REQ-BKG-05 / 07 / 08" (should be **06**); `:59` cites "Scenarios 75-88" and "BR-35..BR-41" (the files hold Scenarios **75-93** and BR-35..**BR-42**); `:65` lists DEC-127..DEC-142 (the log holds DEC-127..**DEC-150**); and the workplan has already been given the two configuration keys (§7.4, DEC-128/DEC-143) while §2.2-§2.5, §3.2-§3.4, §4 and §9 still do not carry the new entities, the controller, the endpoints, the route or the migration — the document is internally inconsistent today (workplan §0.5 and §11).
- **M14 — There is no structural decision-log entry for this cycle.** Three entities, a new repository interface **and** implementation for the add-on master, a new `AddonsController`, seven endpoints, a new route and a new migration are structural per the workplan §0.3, and the previous cycle's plan review raised exactly this (its F6) and it was answered with DEC-114. Record one before delegation.

### Closing verdict

**PROPOSE_BETTER_PLAN.** The commercial design is sound and unusually well evidenced — S1 to S12 show that the add-on-in-the-gross reading, the VAT-inclusive disclosure, the UOM-as-pricing-basis, the literal bands, the per-event prep instruction, the derived tabs and the derived batch mass are each corroborated by the owner's operational note or by the design's own arithmetic, and none of them should be re-opened. What is not sound is the **mechanical path** by which those rules reach the product. One root cause dominates: **the add-on total is not wired into the authoritative money chain** (F1), and because an add-on write re-prices a confirmed, deposited booking (F2), the omission propagates into the quote (F3), the frozen-snapshot guarantee (DEC-099), the register/dashboard reconciliation (BR-27) and the KPI that measures the new automatic rule (F4). Around it sit four surfaces that cannot be built as specified — the prep instruction has no field in the request (F5), the tier-type match is undefined against a free-text column that already holds "Premier Buffet" (F6), the area filter has no server support anywhere in the plan (F7), and the UOM test is a case-sensitive string comparison on money (F8) — plus three contract defects (F9, F11, F12) that would otherwise be implemented twice, differently. None of the twelve requires a new commercial ruling from the owner: nine are specification-and-contract corrections, and three (F6, F8, F9) are match-rule decisions that AGENT 01 can take under the rulings already recorded. **Recommendation:** apply F1-F12 to the specification (reissue as v1.1), the contract (§14 extended, §15.1 re-shaped, §15.3 completed), BR-25/BR-39/BR-41 and the decision log; add the structural entry (M14); then re-submit the plan for a second review before delegation. The pre-existing suites (823/823 unit, 133/133 integration, 327/327 Vitest) must stay green, the migration must remain purely additive and script-reviewed only, and no business data may be seeded.

---

---

## AGENT 01's Disposition — The Debater's Twelve Blocking Findings (2026-10-22)

Every finding was verified against the as-built code before it was applied. **All twelve are closed**, and five rulings are added (DEC-151..DEC-160).

| # | Finding | Resolution |
| :--- | :--- | :--- |
| **F1** | The add-on total had no path into the authoritative calculator — `BookingAmountDueCalculator.cs:104` hardcodes `addonsAmount: 0m` and `Booking` stores no add-on total, so DEC-099's one-source guarantee would have broken the moment an add-on existed. | **Closed by DEC-151.** The add-on total becomes a **frozen column on the booking**, `Booking.AddonsTotal` (`decimal(18,2) NOT NULL DEFAULT 0`), and the shared overload **reads it** instead of passing `0m`. The overload's **argument set does not change** — the `addonsTotal` parameter has been there since the beginning; it simply had nothing to read. The migration gains a **column**. |
| **F2** | An add-on write re-prices a confirmed, deposited booking, and nothing said how `Subtotal`, `DiscountAmount`, `AmountDue` or the deposit react, or what happens when the booking becomes overpaid. | **Closed by DEC-152.** The whole money reaction is now specified: the add-on total is recomputed and the snapshot **re-frozen** (BR-04 already anticipates "line-item addition"), the **frozen** discount is **re-capped** at the new gross but **not** recomputed, the **deposit is not** touched, and a re-price that would leave the collected amount **above** the new net amount due is **refused** with a `400` — the operator must settle the credit first. |
| **F3** | The quote accepted neither the selection nor the add-ons, so the quote and the create would diverge — a direct BR-31 breach. | **Closed by DEC-153.** `BookingQuoteRequestDto` gains `selections?` and `addons?`, and the quote prices them through the **same** path as the create. |
| **F4** | `b.DiscountSource` in KPI-42 had no stored home. | **Closed by DEC-154.** The source is **derived**, not stored, and the derivation is stated: a `DiscountCode` present means `Code`; otherwise a qualifying headcount band means `HeadcountBand`; otherwise `None`. The derivation is **exact** — the band is the only source of a fixed discount without a code — and the source is **published** on the register item and in the summary. |
| **F5** | The prep instruction had no place in the request, and a replace that omitted it would wipe what the chef typed. | **Closed by DEC-155.** The selection element becomes `{ courseId, menuId, prepInstruction? }` in both the request and the response, and an **omitted** member **clears** the instruction — the selection is a **full replace**, so the client always sends the instruction it holds. |
| **F6** | DEC-141's tier match was undefined against a **free-text** `Package.PackageType` that carries values like "Premier Buffet". | **Closed by DEC-156.** The match is pinned: **trimmed, ordinal-ignore-case, equality** with the two enum names `Regular` and `Premier` **verbatim**. Every other stored value — including "Premier Buffet" — is **not** a surcharge tier, and the refusal carries a message that says so. The catalogue's type values must be normalised to the enum vocabulary (DEC-070's suggestion list); until they are, the restriction is deterministic and the remedy is data hygiene. |
| **F7** | The "filter by Package Area" had **no server support** — there is no area-to-packages query. | **Closed by DEC-157.** `GET /api/packages` gains an optional `areaId` filter that returns the active tiers **covering** that area, and the client endpoint table and the workplan carry it. |
| **F8** | The UOM-as-pricing-basis was a case-sensitive string test on **money**, against an unpinned free-text column. | **Closed by DEC-158.** The pricing basis becomes an **explicit column**, `AddonDetail.PricingBasis` (`Flat` | `PerGuest`, nullable = not stated), and the **UOM** stays a descriptive unit of measure. A `null` basis is **flat** — the safe direction, because it never multiplies a price that was not meant to be multiplied. The migration gains a second column. |
| **F9** | The request's `amount` semantics contradicted the multiplication, and BR-01's own formula says `Σ (AddonQuantity × AddonUnitPrice)` — a quantity the plan removed. | **Closed by DEC-158** together with a correction to **BR-01 §1.1**: the add-on total is the sum of the **frozen line totals**, and the per-guest quantity is expressed **within** the line (`unit price × heads`). The request carries `addonId` and an optional **line total**; the server derives the default from the catalogue's amount and the basis, and uses the supplied total when the operator has negotiated one. |
| **F10** | The BEO could not print the selection, and the "standard run" is a **cost figure**, not a dish list. | **Closed by DEC-159.** The claim is **dropped**: a selection-less BEO states that the event runs the tier's standard run and prints the **course rules**, with **no** dish list — inventing one is not an option. The document is extended to carry the **per-event prep instruction** and the **add-on lines**, because those are the kitchen's work. |
| **F11** | The client would have had to hardcode the band bounds and the VAT rate — and the feature's own `noHardcodedFigures` test would have failed on delivery. | **Closed by DEC-160.** The server publishes the **band label** and the **VAT rate**; the client composes the wording from them. The **bounds** are never duplicated into the browser. |
| **F12** | The contract was never actually extended, and §15.1 put three figures at the wrong level. | **Closed.** Contract **§14.1/§14.2/§14.3/§14.4** now carry the extensions, **§15.1** is re-shaped (`batchMassKg` on each `courses[]` element, `prepInstruction` on each `selections[]` element, `includedVatAmount` at the top level), and **§15.3** gains `addons?`. |

**The clarifications are applied too:** M1 (one shared BR-15 validator, and §1.4's wording corrected) · M2 (exactly one selection state — `None` | `Selected` | `Locked`, with `Locked` winning) · M3 (`batchMassKg` rounds to two decimals away from the midpoint, and a rule whose portion is **0 g** publishes `null`, never `0.00`) · M4 (`menuId` aligned to **50**, and the delete behaviours pinned: **Cascade** for `Booking`, **Restrict** for `Menu`, `CourseCategory` and `AddonDetail`) · M5 (the three new tables and the `AddonCategory` master are added to `business/data-dictionary.md` in this cycle) · M6 (`IBookingPolicyProvider` gains `MenuSelectionLockDaysBeforeEvent` and `VatPercentage`, both `null` by default) · M7 (the workbench gains a **package-type filter**; the area dropdown's visibility follows the **selected type**; and the existing **municipality** selector is hidden for a tier that cannot carry the out-of-town amount — otherwise the delivered workbench would break with a `400` on a Wedding tier) · M8 (the register population loads the two new navigations, and DEC-132's conditional estimate is computed **per booking** for the bookings that have a selection) · M9 (the replace path is **last write wins**; `Booking.RowVersion` guards the booking, not the selection rows) · M10 (the frontend test plan names the right file) · M11 (KPI-40's margin basis is the booking's **frozen** `PricePerHead`, never the tier's current rate) · M12 (the add-on category is **unreachable** in this cycle, and the record says so plainly) · M13 (the stale governance record corrected).

**None of the findings calls the commercial design into question.** Every one of them was a defect in **how the plan was written**, not in what it decided — and the two that would have cost real money (F1's missing path and F8's string test on money) are exactly the kind a live review exists to find.

---

## Delivery Log — Stage 7 (Implement), 2026-10-22

**Pre-delegation repair.** Two defects left by the earlier regex sweep were found in the specification and repaired in place, because the sub-agents read it as the authoritative brief:

| Location | Defect | Repair |
| :--- | :--- | :--- |
| `business/bookings-events-operations-feature-spec.md` §3.5 | The fourth money-reaction bullet ended mid-sentence at `...above the net due and $1`, swallowing the sentence's close. | Restored: *"...because BR-05 forbids a payment above the net due, and the operator must settle the credit first."* |
| the same file, §3.6 and §7 | The `### 3.6 The extended-location restriction` heading had become `$2The extended-location restriction`, and the `## 7. Acceptance Criteria` heading was **absent** entirely. | Both headings restored; the section list now runs 1 → 10 with no gap and no orphan replacement token. |

A repo-wide sweep confirms **no other** `$n` replacement token remains in any governance artefact, and **no** U+FFFD replacement character exists in any of them. The only `$n` sequence left in the tree is LaTeX in BR-01 (`$0.00$`), which is correct.

**Delegation.** Both task briefs were relaunched in parallel after the previous attempt failed at the transport layer (`Endpoint is unavailable`, no work performed):

| Task | Agent | Brief rests on |
| :--- | :--- | :--- |
| **TASK-BKG-06-BE** | AGENT 02 | Contract §14/§15/§16; spec §1.3b/§3/§4/§7/§8.1/§8.2/§9; DEC-127..DEC-160; the data dictionary's three new tables; the workplan's layer boundaries |
| **TASK-BKG-06-FE** | AGENT 03 | Contract §14/§15/§16 (coded **against the contract**, not the current API); spec §5/§7/§8.3; the `Booking_Menu_Selection` design; the workplan's route and endpoint tables |

Both briefs forbid editing `business/`, `architecture/`, `governance/`, `implementation/`, `docs/` and the other agent's layer, require the **pre-existing suites to stay green** (823 unit / 133 integration / 327 Vitest), and require each report to name the workplan sections its files fall under and to flag any workplan/code discrepancy.

**Next:** review both deliveries against the acceptance criteria, then live-validate on a disposable database, then realign the workplan (§2.2, §3.3, §3.4, §9, §10, §11) and close the cycle.

---

## Delivery Outcome — Stage 8/9/10 (Review, Validate, Close), 2026-10-22

### What the review found and what was fixed

The two deliveries were not accepted on their reports. AGENT 01 re-ran every suite and read the money-critical paths. Five defects were found and fixed in place; none changed a business rule.

| # | Defect | Fix |
| :--- | :--- | :--- |
| 1 | **The build was never run by AGENT 02** (the session ended at the usage limit). The code compiled, but the suite had never executed. | Build **0 W / 0 E**; the first run exposed **3 failing unit tests**, all in the new `ReplaceBookingMenuSelectionCommandTests`. |
| 2 | A cleared selection asserted `SelectionState.Selected` — a booking with no live row is **`None`** (BR-28, M2). | Assertion corrected to `None`. |
| 3 | The 500-character prep-instruction test built its request with the *extra-entry* helper, so the duplicate-dish guard fired first and the test never reached the length rule (the rule itself was implemented correctly). | The test now uses `CompliantSelectionWithAppetizerPrep`, and the refusal is exercised for real. |
| 4 | `PUT /bookings/{id}/addons` returned a **null category name** for a line added by that same write: the in-memory aggregate carries a new `BookingAddOn` whose `AddonDetail` navigation is not hydrated. | The command re-reads the saved aggregate through `GetWithLineItemsAsync` (which includes the catalogue and its category) before presenting, and the unit fake was completed to hydrate `AddonDetail.AddonCategory` like the real repository. |
| 5 | The menu ledger **inferred** the discount source from the frozen snapshot and could not print the band's bounds, because contract §15.1 published neither. | Contract §15.1 now publishes `discountSource`, `discountBandLabel` and `discountBandRatePerHead`; the DTO, the presenter and the ledger were extended, and the inference is now only a fallback while the payload loads. A fixture and a test pin the band label rendering. |

Two further findings were **contract ambiguities**, not code defects, and were closed rather than worked around:

- **`GET /api/addons` had no headcount parameter** although `lineAmountForBooking` depends on it. The backend had guessed `headcount` with a default of `1`; the frontend had guessed `pax`. A default of one guest would publish a per-guest unit price indistinguishable from a genuine one-guest booking (BR-20), so the parameter is now **required** in contract §16.1, absent or `< 1` is a `400`, and the client was renamed to `headcount` and no longer fires the query without a positive headcount.
- **The specification's own sweep damage**: the `### 3.6` heading and the `## 7. Acceptance Criteria` heading had been lost (rendered as `$2` and swallowed by a `---` rule). Both were restored before delegation, along with a truncated sentence in §3.5.

### Validation evidence (executed by AGENT 01)

| Suite | Result |
| :--- | :--- |
| Backend build (`SalsCaterSuite.slnx`) | **0 warnings / 0 errors** |
| Unit tests | **941 / 941** (baseline 823 + 118 new) |
| Integration tests | **133 / 133** (baseline preserved) |
| Frontend `tsc --noEmit` | **0 errors** |
| Frontend `vite build` | clean (the pre-existing chunk-size advisory only) |
| Vitest | **359 / 359** (baseline 327 + 31) |
| Live end-to-end (`run-live-validation-bkg06.ps1`) | **61 / 61 assertions**, disposable database dropped afterwards |

The live pass is the one that matters most, because it is the only one that could prove the two things the green suites cannot see:

1. **The add-on total reaches the authoritative calculator.** A 120-head Premier booking at ₱820 with a ₱50 out-of-town rate and a ₱150/head add-on quoted **₱122,400.00 gross − ₱2,400.00 band = ₱120,000.00 net**, and the created booking's frozen `amountDue` equalled that figure **to the cent** — the `addonsAmount: 0m` placeholder at `BookingAmountDueCalculator.cs:104` is finally gone, and `Booking.AmountDue` and the calculator agree **with an add-on present**.
2. **The add-on write re-freezes the money.** Adding a ₱1,500 flat line moved the net to ₱121,500.00 and left the deposit untouched; reducing a fully-paid booking's lines below the collected amount was **refused with a 400 and wrote nothing**.

The live run also pinned the VAT-inclusive memo (₱12,857.14 at 12%, **inside** the total, never added), the server-published band label and rate, the derived discount source, the batch masses (21.6 kg and 24.0 kg), the prep instruction round-trip, the quota breach, the lock date and its refusal, the register's four new fields, the summary's `bandDiscount`/`menuWindows`/`vatPercentage`, and the BEO PDF.

### Artefacts realigned in the same cycle

- `docs/salscatersuite_project_structure_workplan.md` - §2.2 (entities, enums and the three columns), §2.4 (the new repository), §2.5 (the controller), §3.2 (the 44-file bookings tree), §3.3 (the route), §3.4 (the seven client endpoints and the `areaId` filter), §4 (the sixth migration), §9 (the delivered row), the header's **Last aligned** row, the change log and §11 (a new verification block). The new configuration keys were already recorded in §7.4.
- `implementation/feature-status.md` - the REQ-BKG-06/07/08 row is **DELIVERED**, with the counts and the remaining risks.
- `business/bookings-events-operations-feature-spec.md` - §4.2 now lists the discount disclosure fields and §4.3 marks `headcount` required.
- `architecture/approved-api-contracts.md` - §16.1's query-parameter table and §15.1's discount fields.

### What is still open (for the owner, not for the engineers)

- The **add-on master ships empty** (BR-13/RSK-066). The whole add-on half is **inert until the catalogue is populated**; the picker's empty state says so rather than fabricating rows.
- The **VAT rate** and the **menu-change window** are configuration and **absent by default** (DEC-143, DEC-128). With neither set, the disclosure line is omitted and the lock date equals the event date - correct, but the owner should state the intended values.
- The **90-99 head gap** is implemented literally (DEC-136). If it was meant to be closed, that is an owner ruling, not a bug.
- A tier typed **"Premier Buffet"** is **not** eligible for the out-of-town amount (DEC-156). The catalogue's type values need normalising to the enum vocabulary; until then the refusal is deterministic and the message names both eligible types.
- The **fifteen** questions in the specification's §9 remain unanswered; none blocks what was delivered.

**No new project, no new root folder, no new package, no re-seeded business data, and no destructive operation.** The migration is purely additive and was applied only to disposable databases.
