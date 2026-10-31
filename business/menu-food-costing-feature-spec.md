# Menu & Food Costing Master + Create New Recipe — Feature Specification

| Field | Value |
| :--- | :--- |
| Feature | Menu & Food Costing Master (dish catalogue + recipe inspector) and Create/Edit Recipe |
| Version | 1.0 |
| Status | **APPROVED for implementation** (BI Architect) |
| Date | 2026-10-20 |
| Design reference | `d:\Downloads\SalsCaterSuite_Designs\Menu&FoodCosting_Main\` and `…\Menu&FoodCosting_New\` (`code.html`, `screen.png`, `DESIGN.md`) |
| Closes | REQ-MNU-01 (menu catalogue CRUD — backend exists, no UI), REQ-MNU-02 (food costing engine — backend exists, no UI) |
| New requirement | REQ-MNU-03 — **recipe/ingredient/yield engine — BLOCKED, awaiting owner decisions (§8)** |

---

## 1. Scope

### 1.1 In scope

1. **Menu & Food Costing Master** at route `/menus` — the dish catalogue with
   server-computed costing, four KPI cards, course tabs, search / status / sort,
   a dish card grid, a recipe inspector, pagination and an Excel export.
2. **Create / Edit Recipe** at routes `/menus/new` and `/menus/:menuId/edit` — the
   dish master record with a **server-computed** margin preview.
3. **Backend corrections** required to make the above truthful (§7).

### 1.2 Explicitly out of scope — the recipe / ingredient / yield engine

The design reference's Recipe Inspector is built around a **100-pax batch ingredient
breakdown**: a FIFO unit rate per ingredient, gross quantity, loss/trim percentage,
net yield, extended cost, pre-cook trimming loss, convection roasting loss, a
portion-size buffer, a plated portion weight, a plating photo, allergen tags,
package-tier suitability, a commis line, plating and critical-control-point
directives, a recipe version, and a supplier-price simulation.

**None of this exists in the approved data model.** The `Menu` entity is
`MenuId`, `CourseId`, `MenuName`, `DeptId`, `Note`, `Price`, `FoodCost`, `Image`,
`DateAdded`, `DateUpdated`. There is no ingredient, SKU, supplier, purchase price,
yield, trim, shrinkage, portion, allergen, plating, station, batch, lot/FIFO or
recipe-version concept anywhere in the domain model, the data dictionary, the
business rules, the KPI catalogue or the glossary.

Every one of those is a **financial formula that only the owner may rule on** — how
a FIFO rate becomes a net yield, how a 100-pax batch amortises into a per-pax food
cost, which cost basis the BR-07 benchmark is measured against. Building them
without a ruling would invent the business rules, which the governance rules
forbid.

**Therefore this delivery implements the real costing and records the engine as a
blocked capability (§8).** The Recipe Inspector shows the dish's authoritative
costing and an explicit, labelled capability-gap notice where the ingredient
breakdown would be. Nothing is faked, stubbed or seeded.

### 1.3 Design fidelity

The screens reproduce the reference layout, design language and information
architecture: the banner + action ribbon, four KPI cards, the course tab strip,
the search/status/sort bar, the **card grid** catalogue (not a table), the sticky
recipe inspector, the pagination footer, and — on the form screen — the identity /
pricing / plating column pair, the yield panel, the ingredient table and the sticky
cost bar. The existing `DESIGN.md` token set is used unchanged; no new Tailwind
token is introduced.

---

## 2. Actors and permissions

| Actor | Capability |
| :--- | :--- |
| Any authenticated user | View the catalogue and its costing, preview a margin, create a dish, edit a dish. |

No new role policy is introduced. `BR-12` restricts only cancellation/refund and
payment recording, and this feature performs neither.

---

## 3. Authoritative figures

Every number is computed **server-side** (DEC-002). The browser formats and displays;
it computes no margin, no percentage and no cost figure.

| Figure | Formula | Source | Authority |
| :--- | :--- | :--- | :--- |
| Selling price | `Menu.Price` | `Menu` | — |
| Food cost | `Menu.FoodCost` | `Menu` | A **manually entered** decimal (no ingredient model — see §1.2) |
| Gross profit | `Price − FoodCost` | — | `MenuCostingService` |
| Food cost % | `round(FoodCost ÷ Price × 100, 2)` | — | `MenuCostingService` |
| Gross margin % | `round((Price − FoodCost) ÷ Price × 100, 2)` | — | `MenuCostingCalculator` |
| Target food cost % | `Dashboard:Benchmarks:TargetFoodCostPercentage` | configuration | DEC-040 |
| **Catalogue average food cost %** | `Σ FoodCost ÷ Σ Price × 100` over the margin population | `Menu` | **KPI-22 (new)** |
| **Best-margin course** | The course with the highest `Σ(Price−FoodCost) ÷ ΣPrice × 100` | `Menu` | **KPI-23 (new)** |
| **Margin alert** | A dish whose food cost % exceeds the target | `Menu` | **KPI-24 (new)** |
| Costing coverage | costed ÷ total | `Menu` | **KPI-14** (exists) |
| Catalogue margin | `Σ(Price−FoodCost) ÷ ΣPrice × 100` | `Menu` | **KPI-15** (exists) |
| Food cost share per course | `Σ FoodCost` per course ÷ catalogue total | `Menu` | **KPI-16** (exists) |

**Rounding.** Every percentage on these screens is rounded to two decimal places with
**`MidpointRounding.AwayFromZero`** — the same rule the operations dashboard's
`Round` helper already uses. `MenuCostingService` currently uses the two-argument
`Math.Round`, which is **banker's rounding** and can differ from the dashboard by
0.01 on an exact `.005` midpoint. §7.5 requires that to be corrected, so that the
master screen, the menu-costing service and the dashboard all round identically.

**The margin population** is non-deleted menus with `Price > 0` **and**
`FoodCost > 0` — the same population as KPI-15, so the menu screen and the
operations dashboard can never disagree (DEC-040, AC-B8).

**Null semantics.** `averageFoodCostPercentage`, `averageFoodCostHeadroomPercentage`
and `bestMarginCourse` are **`null`** when the margin population is empty. The
client renders an em dash, never `0` and never `0%` (contract §9.2 rule 6).
`costingCoveragePercentage` is `0` when the catalogue is empty — that is a
population ratio with a defined zero (KPI-14's own zero-denominator rule).

**The benchmark travels with the payload.** `targetFoodCostPercentage` and
`isBenchmarkConfigured` are published by the server, read from the single
`IFoodCostBenchmarkProvider` the dashboard also uses. The client never hardcodes
32 % or 35 %.

---

## 4. Screen specification — Menu & Food Costing Master (`/menus`)

### 4.1 Banner

- Breadcrumb: `Catering Operations / Kitchen Commissary / Menu & Food Costing`.
- H1 **Menu & Food Costing Master**, sub: "Dish catalogue, automated food cost
  percentage calculations, and catalogue margin telemetry."
- Benchmark badge: `Benchmark Target: ≤ {targetFoodCostPercentage}%` — the
  **server's** value. When `isBenchmarkConfigured` is false the badge says
  "Default target (not configured)".
- Actions: **Recalculate** (refetch), **Export Excel** (active — downloads the
  current filtered set), **+ New Recipe** (opens `/menus/new`).

### 4.2 KPI cards (four) — all server-computed

| # | Card | Value | Sub-line | Source |
| :--- | :--- | :--- | :--- | :--- |
| 1 | **Total Menu Items** | `summary.totalDishes` | "{costedDishes} Costed • {uncostedDishes} Uncosted" + a coverage track | KPI-14 |
| 2 | **Average Food Cost** | `summary.averageFoodCostPercentage` | "{headroom} headroom" / "Over target" + "Target ≤ {target}%" | KPI-22 |
| 3 | **Best-Margin Course** | `summary.bestMarginCourse.courseName` | "{grossMarginPercentage} margin • {averageSellingPrice} avg" | KPI-23 |
| 4 | **Margin Alerts** | `summary.marginAlertCount` | "dishes above the {target}% food-cost target" | KPI-24 |

> **The reference's fourth card, "Cost Volatility Alert" (e.g. "Butter +14%",
> "Ribeye +8.5%", "3 Audits"), is not implemented.** The catalogue stores one
> current `FoodCost` per dish; there is **no purchase price, no supplier, no price
> history and no audit log**, so a volatility or price-change figure cannot be
> derived. The card is replaced by **Margin Alerts** — the count of dishes whose
> food cost % exceeds the configured benchmark — which is real, actionable, and
> powers the reference's own *"Margin Alert (>32%)"* status filter. Recorded as
> **DEC-060**.

### 4.3 Course tabs

`All Dishes (n)` plus one tab per `CourseCategory`, each with its **live dish
count**. The counts come from `courses` and therefore **narrow with the search**
and the status filter — only the course filter is ignored — so the tab strip
always describes the population the operator is actually looking at. `n` for "All
Dishes" is `courses.total`; each tab's count is `courses.items[].dishCount`.
Every `CourseCategory` gets a tab, including one with no dishes.

> **The reference's tab taxonomy is "Beef & Carvery / Seafood & Marine / Poultry &
> Pork / Pasta & Starches / Vegetables & Salads / Artisan Desserts / Beverages &
> Bar" — dish *types*.** The approved `CourseCategory` rows are **course
> positions**: Appetizer, Soup, Salad, Main Course, Dessert, Beverage (the six
> seeded rows). The tabs therefore use the real course categories, with the
> reference's tab interaction (a horizontally scrollable strip, counts, active
> pill). Recorded as **DEC-061**.

### 4.4 Search, status and sort

- **Search** — case-insensitive substring over `menuId`, `menuName` and `note`.
- **Status** — `All` / `Costed` / `Uncosted` / `Margin Alert` (food cost % above
  the target). An unrecognised value is ignored, not a `400`.
- **Sort** — `FoodCostAsc` (default) / `FoodCostDesc` / `PriceDesc` / `PriceAsc` /
  `MarginDesc` / `NameAsc`.
- The reference's **"Columns"** button is **not rendered** — the card grid has a
  fixed set of fields and there is no column-visibility model.

### 4.5 Dish card grid

The reference uses a **card grid**, not a table. Each card shows:

| Element | Source |
| :--- | :--- |
| Dish code | `menuId` (mono, e.g. `DS-104`) |
| Course chip | `courseName` |
| Food-cost % chip | `foodCostPercentage`, toned by `isWithinTarget` |
| Dish name | `menuName` |
| Selling price / food cost | `sellingPrice` / `foodCost` (mono) |
| Gross profit | `grossProfit` (mono) and `grossMarginPercentage` |

The selected card carries the active border and ring, exactly as the reference.

> **The reference's "Portion: 220g / pax" line is not rendered** — there is no
> portion attribute on `Menu`. The reference's "VIP Tier" chip is not rendered
> either — there is no customer- or dish-tier concept.

### 4.6 Recipe inspector (right column, 5/12)

- Header: the dish code, the dish name, and the **unit cost** (`foodCost`) and the
  selling price.
- **Portion Price Allocation** block: `sellingPrice` per pax, the food cost % with
  the server's value, the gross margin % and the gross profit ₱, a **progress
  track** for the food cost against the target (server's `foodCostPercentage` vs
  `targetFoodCostPercentage`), and the **variance** against the target.
- The dish's own record: its course, its note and its image.
- Actions: **Edit Recipe** → `/menus/{menuId}/edit`; **Print** is **not**
  implemented (there is no dish print/BEO document — the existing
  `KitchenBeoDocument` is a booking BEO, not a dish card).
- **Where the reference shows the 100-pax ingredient breakdown, the yield
  calibration panel and the plating card, this screen shows an explicit
  capability-gap notice** naming what is missing and pointing at REQ-MNU-03. It is
  never a fake table.

### 4.7 Pagination

Server-side. `page` (1-based) and `pageSize` (12 / 24 / 48, default 12), clamped
server-side. The footer shows "Showing {first}–{last} of {totalCount} recipes" and
numbered page buttons. The client never slices.

### 4.8 Empty and error states

- No dishes → an empty state explaining that the catalogue is empty, with the
  **+ New Recipe** action.
- A filtered set with no matches → an empty state naming the active filter.
- A failed load → an error banner with a retry, and **never** fabricated content.

---

## 5. Screen specification — Create / Edit Recipe (`/menus/new`, `/menus/:menuId/edit`)

### 5.1 Layout

The reference's two-column composition: a **left column** (dish master identity,
pricing and margin simulator, plating and prep directives) and a **right column**
(the summary cards, the yield panel and the ingredient formulation table), over a
**sticky cost bar**.

### 5.2 Dish Master Identity (left, top)

| Field | Maps to | Required | Rules |
| :--- | :--- | :--- | :--- |
| Recipe ID | `menuId` | yes | 1–50 chars; upper-cased by the server; unique (`409` on duplicate) |
| Official Banquet Menu Name | `menuName` | yes | 1–150 chars |
| Course Category | `courseId` | yes | one of the seeded `CourseCategory` rows |
| Commis Line / Lead | `deptId` | no | **not rendered** — `DeptId` is an unmapped legacy integer with no lookup table |
| Batch Scale Basis | — | — | **not rendered** — no batch model |
| Package Tier Suitability | — | — | **not rendered** — no tier model |
| Allergen Safety Tags | — | — | **not rendered** — no allergen model |
| Notes | `note` | no | 1–500 chars |
| Plating Photo | `image` | no | a URL string (the `Menu.Image` column) |

### 5.3 Pricing & Margin Simulator (left, middle)

- **Selling price per pax** → `price` (≥ 0).
- **Food cost** → `foodCost` (≥ 0).
- A **live margin preview**: the food cost %, the gross profit ₱, the gross margin
  %, the variance against the benchmark, and whether it is within target.

  **The preview is computed by the server**, through `POST /api/menus/costing/preview`
  (§6.2), so the preview and the saved record can never disagree (DEC-002). The
  browser does not compute a margin.
- The reference's "Simulate +10% Ingredients" action is **not implemented** — it
  presupposes an ingredient cost base that does not exist (§1.2).
- The reference's "Recommended Package Assignment" is **not implemented** — there is
  no package-assignment rule and no package catalogue rule to satisfy.

### 5.4 Summary cards (right, top)

Benchmark target, selling price, food cost, gross profit, and the food cost %
against the target — all from the preview response, which carries the server's
benchmark.

### 5.5 Yield panel, ingredient table, plating directives (right and left, lower)

**Not implemented** — each belongs to the recipe/ingredient engine (§1.2). Each is
replaced by an explicit capability-gap notice naming the missing data and the
blocking decision. Nothing is fabricated.

### 5.6 Sticky cost bar

Shows the **server's** preview figures: food cost %, gross profit ₱ per pax, the
gross margin %, and the food cost % against the target. Actions: **Save Draft /
Save Recipe** and, when editing, the delete is **not** offered on this screen —
`DELETE /api/menus/{id}` is a **soft delete** (DEC-063), and exposing a
destructive-sounding action that silently only flags a row would mislead the user.

### 5.7 Saving

- **Create** → `POST /api/menus` → `201 { menuId }` → navigate back to `/menus`
  with the list refreshed.
- **Edit** → `PUT /api/menus/{id}` → `204` → navigate back.
- A `400` renders the server's `errors[]` verbatim and **preserves the typed
  input**. A `409` on a duplicate recipe id is shown as a field-level message.

---

## 6. API contract

Base URL `/api`. Bearer JWT. Errors use `{ "errors": [ "..." ] }`.

### 6.1 `GET /api/menus/master`

| Query parameter | Type | Default | Behaviour |
| :--- | :--- | :--- | :--- |
| `search` | string? | — | Case-insensitive substring over `menuId`, `menuName`, `note` |
| `courseId` | int? | — | Filters to one course; ignored when absent |
| `status` | string? | — | `Costed` \| `Uncosted` \| `MarginAlert` (case-insensitive); anything else is ignored |
| `sort` | string? | `FoodCostAsc` | `FoodCostAsc` \| `FoodCostDesc` \| `PriceDesc` \| `PriceAsc` \| `MarginDesc` \| `NameAsc`; anything else falls back to the default |
| `page` | int | 1 | Clamped to ≥ 1 |
| `pageSize` | int | 12 | Clamped to 1…100 |

`200` response:

```json
{
  "summary": {
    "totalDishes": 181,
    "costedDishes": 142,
    "uncostedDishes": 39,
    "costingCoveragePercentage": 78.4,
    "averageFoodCostPercentage": 28.6,
    "averageFoodCostHeadroomPercentage": 3.4,
    "targetFoodCostPercentage": 32.0,
    "targetMarginPercentage": 68.0,
    "isBenchmarkConfigured": true,
    "averageSellingPrice": 320.00,
    "marginAlertCount": 6,
    "bestMarginCourse": {
      "courseId": 4,
      "courseName": "Main Course",
      "grossMarginPercentage": 74.2,
      "averageSellingPrice": 485.00,
      "dishCount": 42
    }
  },
  "courses": {
    "total": 181,
    "items": [
      { "courseId": 1, "courseName": "Appetizer", "dishCount": 12 }
    ]
  },
  "items": [
    {
      "menuId": "DS-104",
      "menuName": "Prime Roast Angus Ribeye with Truffle Demi-Glace",
      "courseId": 4,
      "courseName": "Main Course",
      "note": null,
      "image": null,
      "sellingPrice": 420.00,
      "foodCost": 138.60,
      "grossProfit": 281.40,
      "foodCostPercentage": 33.00,
      "grossMarginPercentage": 67.00,
      "targetFoodCostPercentage": 32.0,
      "isWithinTarget": false,
      "marginVariancePercentage": 1.00
    }
  ],
  "page": 1,
  "pageSize": 12,
  "totalCount": 181,
  "totalPages": 16,
  "generatedAt": "2026-10-20T09:15:00.1234567Z"
}
```

**`summary` describes the whole filtered result set, not the current page.**
**`courses` describes everything except the course filter** — the search and the
status filter still narrow it, so the tab counts follow the search the way the
Customer Master's quick-view counts do. `courses.total` is the "All Dishes (n)"
tab count; `courses.items[].dishCount` is each tab's count. Dishes with no
course are counted in `courses.total` but have no tab, so
`Σ items[].dishCount` may be less than `courses.total`.

| Field | Meaning |
| :--- | :--- |
| `summary.costingCoveragePercentage` | KPI-14. `0` when the catalogue is empty. |
| `summary.averageFoodCostPercentage` | KPI-22. `null` when the margin population is empty. |
| `summary.averageFoodCostHeadroomPercentage` | `target − actual`, in percentage points. `null` when the actual is `null`; **negative** when over target. |
| `summary.bestMarginCourse` | KPI-23. `null` when the margin population is empty. |
| `summary.marginAlertCount` | KPI-24. Dishes whose food cost % exceeds the target. |
| `items[].marginVariancePercentage` | `foodCostPercentage − targetFoodCostPercentage`, in percentage points. Positive is over target. |
| `items[].isWithinTarget` | `foodCostPercentage ≤ targetFoodCostPercentage`. |

### 6.2 `POST /api/menus/costing/preview`

Request `{ "sellingPrice": 380.00, "foodCost": 104.20 }`.
Response `200`:

```json
{
  "sellingPrice": 380.00,
  "foodCost": 104.20,
  "grossProfit": 275.80,
  "foodCostPercentage": 27.42,
  "grossMarginPercentage": 72.58,
  "targetFoodCostPercentage": 32.0,
  "targetMarginPercentage": 68.0,
  "isWithinTarget": true,
  "marginVariancePercentage": -4.58
}
```

`400 { "errors": [...] }` when the prices are negative or the request is malformed.
When `sellingPrice` is `0`, `foodCostPercentage` and `grossMarginPercentage` are
`0` (the same rule `MenuCostingService` applies) and the client renders them
without implying a margin.

### 6.3 `GET /api/menus/master/export`

Streams the **current filtered result set** (the same `search` / `courseId` /
`status` / `sort` parameters, **not** paged) as an XLSX workbook with
`Content-Disposition: attachment; filename="MenuFoodCosting-{yyyyMMdd}.xlsx"`.
Columns: Dish Code, Dish Name, Course, Selling Price, Food Cost, Gross Profit,
Food Cost %, Gross Margin %, Target %, Within Target, Notes.

### 6.4 `POST /api/menus` and `PUT /api/menus/{id}` — tightened

`POST` accepts `{ menuId, courseId, menuName, note, image, price, foodCost }` and
returns `201 { menuId }`. `PUT` accepts the same body and returns `204`. Both are
validated **server-side before persistence** (BR-11) and return
`400 { "errors": [ "..." ] }` on rejection.

| Field | Rule | Column |
| :--- | :--- | :--- |
| `menuId` | required, 1–50 chars, unique (`409` on duplicate) | `nvarchar(50)` |
| `courseId` | required, must exist in `CourseCategory` | FK |
| `menuName` | required, 1–150 chars | `nvarchar(150)` |
| `note` | optional, ≤ 500 chars | `nvarchar(500)` |
| `image` | optional, ≤ 250 chars (a URL) | `nvarchar(250)` |
| `price` | ≥ 0 | `decimal(18,2)` |
| `foodCost` | ≥ 0 | `decimal(18,2)` |

**Three corrections to the existing write path are required (§7.3, §7.4, §7.5):**
the duplicate check must compare the **upper-cased** id, because the command
persists `menuId.ToUpper()`; a duplicate must be reported in the standard
`{ "errors": [ ... ] }` envelope rather than the current `{ "message": ... }`; and
`image` must become writable, because the column exists but **no** command or
projection currently populates it, so a plating photo could not otherwise be saved.

`GET /api/menus` (the legacy unpaged list) is **unchanged**.

---

## 7. Required backend corrections

1. **BR-11 — the menu write commands are unvalidated.** `CreateMenuCommand` and
   `UpdateMenuCommand` inject **no** FluentValidation validator, so a blank dish
   name, a negative price or an over-length name reaches the database and can
   surface as a `500`. Both must run a validator and return `400 { "errors": [...] }`.
2. **Soft delete.** The menu master query filters `IsDeleted == false` inside its
   own queries, following DEC-045's dashboard-scoped pattern and DEC-050's
   Customer Master precedent. No global query filter is introduced. The existing
   `GetAllCostingsAsync` does **not** filter soft-deleted rows — the master
   screen must not inherit that.
3. **`GET /api/menus` is unpaged.** It is retained for compatibility, but the
   master screen uses the new paged endpoint.
4. **The duplicate-id check compares the raw id, but the command persists the
   upper-cased id.** `CreateMenuCommand` checks `ExistsAsync(request.MenuId)` and
   then saves `request.MenuId.ToUpper()`. Creating `"m001"` and then `"M001"`
   therefore **bypasses the check**, and the second insert fails as an unhandled
   `DbUpdateException` — a `500`, not a `409`. The check must compare the
   normalised (upper-cased) id.
5. **Rounding differs between the two costing paths.** `MenuCostingService` uses
   the two-argument `Math.Round`, which is **banker's rounding**, while the
   dashboard's `Round` helper uses `MidpointRounding.AwayFromZero`. On an exact
   `.005` midpoint the two can disagree by 0.01. `MenuCostingService` must use
   `AwayFromZero` so the menu screen, the costing service and the dashboard round
   identically. (A behaviour change, but a 0.01-alignment one, and it removes a
   real divergence.)
6. **The duplicate error uses the wrong envelope.** `POST /api/menus` returns
   `409 { "message": "..." }`, while the approved error contract (contract §7) is
   `{ "errors": [ "..." ] }` for every business and validation failure. The `409`
   must use the standard envelope.
7. **`Menu.Image` is unreachable.** The column exists (`nvarchar(250)`) and the
   entity has the property, but **no** command writes it and **no** projection
   populates it, so it is always `null` over HTTP and a plating photo could not
   be saved. `CreateMenuRequest` must gain an `image` field and both commands
   must apply it.
8. **`MenuResponse.ProfitMargin` is a get-only computed property that EF cannot
   project**, so it serialises as `0` on `GET /api/menus` and `GET /api/menus/{id}`
   even when the margin is not zero. A field that always reports zero is worse
   than no field. **The property is removed** from `MenuResponse`; the master and the
   costing endpoints publish the real figure, and the existing test that asserted
   it — which only ever passed because the hand-written fake computed it in
   memory — is updated. Recorded as RSK-030.

---

## 8. REQ-MNU-03 — the recipe / ingredient / yield engine (BLOCKED)

**Status: BLOCKED — awaiting owner decisions.** This is *not* a deferral of
effort; it is a set of financial rules that only the owner may set. The screen
ships the real costing and names the gap in the UI.

The decisions required, each with a recommended answer:

| # | Question | Recommendation |
| :--- | :--- | :--- |
| 1 | What is an **ingredient**? (SKU, name, unit of measure, base unit, pack size) | A `CommissaryIngredient` master keyed by SKU, with a base unit per ingredient and a pack size for conversion. |
| 2 | What is the **FIFO rate**? Purchase price per base unit, at what date, and does it come from a purchase ledger? | FIFO from a `PurchaseReceipt` ledger priced at the receipt date; the rate used is the most recent receipt on or before the costing date. |
| 3 | How is **net yield** derived from gross quantity? | `NetYield = GrossQty × (1 − TrimLoss%) × (1 − Shrinkage%)`. Trimming and shrinkage are per-ingredient and versioned with the recipe. |
| 4 | How does a **batch** amortise into a per-pax cost? | `BatchCost ÷ BatchPax`, where `BatchPax` is the batch scale basis, and the buffer is added to the pax count (so the buffer's cost is absorbed, not sold). |
| 5 | Which cost basis does the **BR-07 benchmark** measure — raw FIFO, net plated, or plated-with-overhead? | Net plated cost per pax. This is the number a chef recognises, and it must be the one the 35 % band is defined against. |
| 6 | Are **portion sizes** and **buffers** per recipe, per course, or per package tier? | Per recipe, overridable per package tier. |
| 7 | Are **allergens** a tag list, a per-ingredient derivation, or both? | Per-ingredient allergen tags, rolled up to the recipe, with manual overrides. |
| 8 | Is a **dish tier** a concept at all (the reference's "VIP Tier" chip)? | No, unless the owner wants it — there is no tier in the approved model. |
| 9 | Does **price simulation** belong in the product, and is it a scenario or a real re-cost? | A **scenario** that never persists; a re-cost is a separate approval. |
| 10 | Is a **recipe version** required, and does a published recipe freeze its cost? | Yes, and yes — a published recipe's cost is frozen like a confirmed booking's price (BR-04). |

Until these are ruled, the ingredient breakdown, yield panel, portion figures,
allergen tags, plating card, batch economics and price simulation are **not
implemented and not faked**.

---

## 9. Acceptance criteria

| ID | Criterion |
| :--- | :--- |
| AC-M1 | The master screen renders the four KPI cards, the course tabs, the filter bar, the dish card grid, the recipe inspector and the pagination footer. |
| AC-M2 | Every figure comes from the payload. A repo scan of the feature folder finds **no** peso literal, percentage literal or count literal. |
| AC-M3 | The browser computes **no** margin, percentage or cost. The margin preview comes from `POST /api/menus/costing/preview`. |
| AC-M4 | `summary.averageFoodCostPercentage` equals `Σ FoodCost ÷ Σ Price × 100` over the margin population, and `100 − it` equals the dashboard's catalogue margin (KPI-15). |
| AC-M5 | `summary.totalDishes` + `summary.uncostedDishes` equal the catalogue count, and `costingCoveragePercentage` equals KPI-14. |
| AC-M6 | `summary.marginAlertCount` equals the number of items whose food cost % exceeds the target. |
| AC-M7 | The target percentage rendered anywhere equals the server's `targetFoodCostPercentage`; the client never hardcodes 32 % or 35 %. |
| AC-M8 | A null `averageFoodCostPercentage` or `bestMarginCourse` renders as an em dash, never `0` or `0%`. |
| AC-M9 | Search matches the dish code, name and note, case-insensitively. |
| AC-M10 | The status filter narrows to costed / uncosted / margin-alert dishes, and the sort orders by the chosen key. |
| AC-M11 | Pagination is server-side; the table shows at most `pageSize` cards and the footer reports the true `totalCount`. |
| AC-M12 | Selecting a card loads that dish into the inspector, with its cost track, variance and course. |
| AC-M13 | The inspector shows an explicit capability-gap notice where the ingredient breakdown would be — never a fake table. |
| AC-M14 | `+ New Recipe` opens the form; saving creates the dish and returns to the master with the list refreshed. |
| AC-M15 | The form's margin preview is server-computed and updates as the price or cost changes. |
| AC-M16 | A rejected save shows the server's `errors[]` verbatim and preserves the typed input; a duplicate id shows the `409` message. |
| AC-M17 | The export downloads an XLSX containing the current filtered result set. |
| AC-M18 | Every status chip carries a text label; no status is conveyed by colour alone. |
| AC-M19 | The screens are usable at 375 / 768 / 1024 / 1440 px with no horizontal overflow of the page. |
| AC-M20 | The server rejects a blank dish name, a negative price, an over-length name and an unknown course id with the documented error contract. |

---

## 10. Test requirements

### 10.1 Backend

| # | Test |
| :--- | :--- |
| M1 | The master's `averageFoodCostPercentage` equals `Σ FoodCost ÷ Σ Price × 100` over the margin population, and its `100 −` equals the dashboard's catalogue margin. |
| M2 | `costingCoveragePercentage` equals KPI-14 and `null`/zero semantics match the catalogue. |
| M3 | `marginAlertCount` equals the number of dishes above the target. |
| M4 | `bestMarginCourse` is the course with the highest margin, and `null` when the margin population is empty. |
| M5 | A dish with no price or no food cost is excluded from the margin population and is `Uncosted`. |
| M6 | A soft-deleted dish is excluded from the master query, the summary and the course counts. |
| M7 | The search matches the dish code, the name and the note. |
| M8 | Each sort key orders as specified, with a stable tiebreak on `menuId`. |
| M9 | The preview returns the documented figures and clamps nothing incorrectly for a zero price. |
| M10 | `POST` with a blank name / negative price / over-length name returns `400 { errors }` and persists nothing. |
| M11 | `PUT` with an invalid body returns `400 { errors }` and changes nothing. |
| M12 | The export returns a non-empty XLSX for a filtered query. |
| M13 | The master's figures reconcile with `GET /api/dashboard`'s catalogue margin and costing coverage. |

### 10.2 Frontend

| # | Test |
| :--- | :--- |
| F1 | The master screen renders the KPI cards, tabs, grid and pagination from a fixture payload. |
| F2 | A `noHardcodedFigures` scan of the feature folder finds no peso/percentage/count literal. |
| F3 | The client computes no margin — asserting the rendered percentage is the server's exact string. |
| F4 | A null summary figure renders an em dash, not `0`. |
| F5 | The preview is fetched from the server and rendered, never computed locally. |
| F6 | A rejected save renders the server's `errors[]` and preserves the typed input. |
| F7 | An empty catalogue and an empty filtered result render distinct empty states, with no fabricated card. |
| F8 | A failed load renders the error state with a retry. |
| F9 | A successful save invalidates the master query and navigates back to `/menus`. |
| F10 | The paging controls call the API with the new `page` / `pageSize`. |
| F11 | The export request carries the active filters. |
| F12 | Status chips render their text label (not colour alone). |
| F13 | The capability-gap notice is rendered where the ingredient breakdown would be, and no fake ingredient rows exist. |

---

## 11. Documentation updates required

| Artefact | Change |
| :--- | :--- |
| `architecture/approved-api-contracts.md` | Add §11 "Menu & Food Costing Endpoints". |
| `business/kpi-catalog.md` | Add KPI-22, KPI-23, KPI-24. |
| `business/business-glossary.md` | Add "Dish Costing", "Margin Population", "Margin Alert", "Recipe" (as a menu record). |
| `governance/decision-log.md` | Add DEC-059…DEC-062. |
| `governance/risk-register.md` | Add RSK-025 (the food cost is a manually entered decimal) and RSK-026 (the recipe engine gap). |
| `governance/requirements-traceability.md` | Add REQ-MNU-03; update REQ-MNU-01/02. |
| `business/requirements-catalog.md` | Update REQ-MNU-01/02; add REQ-MNU-03 as blocked. |
| `implementation/feature-status.md` | Mark the menu feature delivered. |
| `implementation/acceptance-test-scenarios.md` | Add the §10 scenarios. |
