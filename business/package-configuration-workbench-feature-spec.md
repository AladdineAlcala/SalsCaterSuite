# Sal's CaterSuite — Package Configuration Workbench — Feature Specification

| Field | Value |
| :--- | :--- |
| **Requirement** | REQ-PKG-02 (Package Configuration Workbench) |
| **Specification version** | v1.1 |
| **Issued** | 2026-10-22; amended 2026-10-27 by AGENT 01 (DEC-172) |
| **Status** | Approved for delivery |
| **Design references** | `d:\Downloads\SalsCaterSuite_Designs\Package_Editor and Course Category Rules\code.html` + `screen.png` (Screen A) · `d:\Downloads\SalsCaterSuite_Designs\Package_Location_AddOn\code.html` + `screen.png` (Screen B) — **references only**, not a specification of data |
| **API contract** | `architecture/approved-api-contracts.md` section 13 (issued with this specification) |
| **Decisions** | DEC-077 .. DEC-085, DEC-172 (`governance/decision-log.md`) |
| **KPI additions** | KPI-29 .. KPI-32 (`business/kpi-catalog.md`) |
| **Tasks** | TASK-PKG-02-BE (backend), TASK-PKG-02-FE (frontend) |

---

## 1. Objective and Scope

### 1.1 Objective

Deliver the two design screens as **real, data-backed configuration workbenches** for a buffet tier:

| Screen | Route | Reference |
| :--- | :--- | :--- |
| **A — Course Coverage Rules Workbench** | `/packages/:packageId/rules` | Package_Editor and Course Category Rules |
| **B — Location & Out-of-Town Add-On Matrix** | `/packages/:packageId/locations` | Package_Location_AddOn |

Screen A lets a coordinator attach an enforced **course-category rule** to each course a tier must satisfy (how many dishes, the standard portion, and the rule the client is held to), and then **simulate a client's dish selection** against those rules to see the food cost, the effective food-cost percentage against the configured benchmark, the cost-ceiling position and the rule-compliance verdict — all computed on the server.

**v1.1 amendment (2026-10-27, DEC-172):** `PackageCourseRule` has no inclusion tag or upgrade add-on association. The course-rule editor, card, API request/response and derived booking course projections omit those fields. Applying the new migration drops the old nullable columns and their add-on FK/index; existing values are not preserved. The migration is generated and script-reviewed only; it is not applied to the development database or any existing/persistent or production database. All other REQ-PKG-02 behavior is unchanged.

Screen B lets a coordinator attach the **municipalities** a tier can be served in, with each municipality's per-pax out-of-town surcharge, its distance, its dispatch note and its logistics cost, and then **price a booking** for a chosen municipality and headcount: the base contract, the extended total, the effective per-pax rate, the contract value and the gross-profit split.

### 1.2 The owner rulings this specification implements

The owner answered four questions on 2026-10-22 (DEC-077, DEC-079, DEC-080, DEC-081):

1. **The domain extension is approved** — two join entities plus three optional `Area` columns, one reviewed migration, nothing seeded.
2. **The minimum contract value is published** — `AmountPax × MinPax`, server-computed, labelled as a derived floor.
3. **A cost-ceiling breach warns; it never reprices and never blocks** — no automatic premium is invented.
4. **`Area.LogisticsCost` is added** — an optional per-event logistics cost, so the gross-profit split is real; a null cost omits the band rather than showing zero.

### 1.3 What the references show and what the approved model can carry

| Reference block | Approved model (before this cycle) | Decision |
| :--- | :--- | :--- |
| Package identity, rate, floor, type | `Package` carries all four | **Implemented** |
| Six course-category rules with quota, portion and rule text | No package→course relation | **Implemented** — new `PackageCourseRule` (DEC-077; v1.1 fields per DEC-172) |
| "8 Qualified Dishes" per course | `Menu.CourseId` → `CourseCategory`; the non-deleted dishes of a course | **Implemented** — the pool is derived |
| Per-dish food cost in the simulator | `Menu.FoodCost` (an entered decimal, DEC-062) | **Implemented** |
| Cost ceiling / effective food cost | `Dashboard:Benchmarks:TargetFoodCostPercentage` (DEC-040) | **Implemented** — the configured benchmark, never a hardcoded band |
| Out-of-town surcharge matrix, rate/pax | `Area.AreaDetails` + `Area.AdditionalRate` | **Implemented** |
| Distance (~42 km), dispatch note, logistics burn | No `Area` columns | **Implemented** — three new optional `Area` columns (DEC-077, DEC-081) |
| Package→municipality coverage | No relation | **Implemented** — new `PackageAreaCoverage` (DEC-077) |
| "Duplicaste Package" | no clone endpoint | **Not implemented** (DEC-084) |
| "Publish to Client Engine" | no publish concept | **Not implemented** (DEC-084) |
| Banquet amenities (seating, linens, hardware, waitstaff ratio) | no entity | **Capability gap** (DEC-084) |
| Complimentary staples (rice / iced tea) | no entity | **Capability gap** (DEC-084) |
| Mobile logistics checklist (Cambro units, burners, water) | no entity | **Capability gap** (DEC-084) |
| "+15% Out-of-Town Surcharge" (flat percentage) | a per-pax rate, not a percentage | **Not implemented** — the model carries a per-pax rate; a percentage surcharge would be a new pricing rule |
| "Automatic +₱48.45 premium adjustment" on a ceiling breach | no such rule | **Not implemented** — warn only (DEC-080) |
| "Print Spec Sheet (QuestPDF)" | no package document | **Implemented** — a new QuestPDF document |
| "Simulate Inflation Stress (+10%)" | — | **Implemented** — a server-side preview over the selected dishes' food cost |
| "Bulk Edit" on the matrix | — | **Not implemented** — the matrix is edited one municipality at a time; bulk editing a money column is a separate cycle |

### 1.4 Exclusions (explicit)

- No per-tier stored food cost and no stored margin (the food cost is **always** derived from a dish selection or from the standard-run estimate).
- No persisted client selection: the selection is a **preview**, never a stored commercial term.
- No automatic premium, no ceiling block, no price change on a breach.
- No duplicate/clone action, no "publish to client engine" action, no SignalR price panel.
- No banquet-amenity, staples or mobile-logistics data.
- No package XLSX export, no batch quotation PDF.
- **No seeded business data** — no package rules, no coverage rows, no municipalities (BR-13). The two new tables and the three new columns start empty, and the screens show an honest empty state.

---

## 2. Actors and Permissions

| Actor | Capability |
| :--- | :--- |
| Any authenticated user (BR-12) | Read both workbenches, run the previews, download the spec sheet |
| Any authenticated user | Edit the course ruleset, edit the coverage set, create and update municipalities |

Every endpoint in section 4 requires a bearer token and **no role policy**, exactly as `PackagesController` already declares. The server enforces the token requirement; the client hides nothing the server would allow.

---

## 3. Domain and Data

### 3.1 The new domain entities (DEC-077)

**`PackageCourseRule`** — the enforced rule a tier holds for one course category.

| Field | Type | Notes |
| :--- | :--- | :--- |
| `PackageCourseRuleId` | `int` | Identity key. |
| `PackageId` | `int` | FK → `Package`. Required. Cascade delete. |
| `CourseId` | `int` | FK → `CourseCategory`. Required. Restrict delete. |
| `RequiredQuota` | `int` | ≥ 1. The number of dishes the client must take from this course. |
| `PortionGrams` | `int?` | ≥ 0. The standard portion per guest, in grams. `null` = not specified. |
| `RuleDescription` | `string` | Required, ≤ 500. The rule the client is held to, in plain words. |
| audit columns | | `BaseEntity` (DEC-038). |

**`PackageAreaCoverage`** — the link between a tier and a municipality.

| Field | Type | Notes |
| :--- | :--- | :--- |
| `PackageAreaCoverageId` | `int` | Identity key. |
| `PackageId` | `int` | FK → `Package`. Required. Cascade delete. |
| `AreaId` | `int` | FK → `Area`. Required. Cascade delete. |
| `IsActive` | `bool` | Whether the coverage is currently offered. Default `true`. |
| audit columns | | `BaseEntity` (DEC-038). |

**The extended `Package` entity (M12).** `Package` gains two navigation collections, consistent with the way it already exposes `ICollection<Booking> Bookings`:

```csharp
public ICollection<PackageCourseRule> CourseRules { get; set; } = new List<PackageCourseRule>();
public ICollection<PackageAreaCoverage> AreaCoverages { get; set; } = new List<PackageAreaCoverage>();
```

The relationships are configured FK-first; the collections exist so the Domain entity stays consistent with the rest of the model and with `architecture/domain-model.md` §2.4. No `Package` column changes — `PackageType` stays free text (DEC-070) and no cost column is added.

### 3.2 The extended `Area` (the municipality catalogue)

| Field | Type | Notes |
| :--- | :--- | :--- |
| `AreaId` | `int` | Identity key (existing). |
| `AreaDetails` | `string` | The municipality / zone name, ≤ 150 (existing). |
| `AdditionalRate` | `decimal?` | The **per-pax** out-of-town surcharge (existing, BR-01). |
| `DistanceKm` | `int?` | **New.** ≥ 0. The distance from the commissary. `null` = not stated. |
| `LogisticsNote` | `string?` | **New.** ≤ 150. The dispatch mode, e.g. "Reefer Truck & Mobile Gen". `null` = not stated. |
| `LogisticsCost` | `decimal?` | **New.** (18,2). The **per-event** logistics cost for this municipality. `null` = not stated. |

**The rate is one rate per municipality, not one rate per package (DEC-078).** `Area.AdditionalRate` is the single per-municipality rate the workbench quotes as an out-of-town surcharge, so the coverage row carries **no** money column. The consequence is stated in the UI: editing a municipality's rate affects **every** tier that covers it, and the coverage row cannot override it. A per-package rate override is an open question (section 9).

> **Correction (F14).** An earlier draft of this specification said the rate is "the single per-municipality rate that **BR-01 charges on a booking with an extended area**". That is **not** what the as-built code does. `BookingAmountDueCalculator.Calculate(Booking, decimal)` passes `extendedAreaRate: 0m` and **every** consumer uses that overload, so the extended-area fee is **not charged on any booking today** - BR-01 states the rule as intended, and the implementation does not carry it. DEC-078's *decision* is unaffected (one rate column, not two), but its *consequence* is: editing a municipality's rate today changes **no booking** - only the workbench's own quotes. The municipality editor's warning text therefore says exactly that, and wiring BR-01's extended-area rate through the booking path is raised as a separate, owner-approved task rather than assumed here.

### 3.3 The derived figures

**The qualified pool** of a course rule is every **non-soft-deleted** `Menu` whose `CourseId` equals the rule's course. A dish is **costed** when its food cost is **greater than zero** — the same "margin population" idea `MenuCostingCalculator` uses (DEC-087). A dish that is not costed is still in the pool (it is a real dish) but it is excluded from the pool's average and from the standard run, and the UI says so.

> **Why "greater than zero" and not "not null".** `Menu.FoodCost` is a nullable column, but the column carries a database default of `0.00` (`MenuConfiguration.cs`) and the entity's own default is `0.00m`. A dish created without a food cost therefore stores **zero**, not null, and a row inserted outside EF (including by the future legacy import, DEC-032) lands at `0.00` as well. Treating zero as "not costed" is the only reading that keeps the pool average meaningful in every case (DEC-087).

**The standard run** is the planning estimate for a tier whose client has not yet chosen a dish in each course:

$$\text{StandardRunFoodCostPerPax} = \sum_{r \in R} \overline{\text{FoodCost}}_r \quad\text{where}\quad \overline{\text{FoodCost}}_r = \frac{\sum_{d \in \text{Pool}(r),\, d.\text{FoodCost} > 0} d.\text{FoodCost}}{\,|\{d \in \text{Pool}(r) : d.\text{FoodCost} > 0\}|\,}$$

- `null` when the tier has no course rule, or when no rule's pool contains a costed dish.
- The mean is over the **costed** dishes of the pool only.
- **Rounding order (DEC-003 / RSK-029).** The pool means are computed at full precision and the **sum** is rounded **once**, to two decimals away from the midpoint. The published `poolAverageFoodCost` is separately rounded to two decimals, but the standard run does **not** sum the rounded means — summing rounded means and rounding again would round twice and could differ from the single-rounding rule by a cent.
- It is a **planning estimate, not a commitment**, and the UI labels it that way (DEC-083, **owner-ratified 2026-10-22**).

### 3.4 The authoritative formulas

All of them are computed **on the server** (DEC-002). The browser computes no monetary figure.

**The null-guard rule (DEC-086).** A published figure is `null` when **any** of its operands is unknown, and a percentage is `null` when its denominator is unknown **or is zero**. `null` means *unknown*, never zero (contract §9.2 rule 6). A `0` is published only where zero is a real measurement (an empty coverage set, a zero surcharge, a zero ceiling overage).

**An unstated out-of-town rate is no surcharge (DEC-086).** `Area.AdditionalRate` is published as `null` when the municipality has stated no rate — that is an unknown *measurement*. The **money chain** nevertheless treats an absent surcharge as **zero**, because a surcharge is an *addition* and an absent addition is zero:

$$\text{EffectiveAdditionalRate} = \text{AdditionalRate} \;\text{if present, else}\; 0$$

so a municipality with no stated rate still yields a complete, non-null contract value, and the rate itself is still shown as "not stated". This is a **defined default**, not a null-as-zero: the surcharge's *amount* is known to be nothing; the rate is simply not on record.

| Figure | Formula | Null when |
| :--- | :--- | :--- |
| `CostCeilingPerPax` | `AmountPax × TargetFoodCostPercentage ÷ 100` | `AmountPax` is null |
| `MinimumContractValue` | `AmountPax × MinPax` | either is null (owner-ruled, DEC-079) |
| `SelectedFoodCostPerPax` | `Σ FoodCost` over the selected dishes | no selection is supplied, **or** any selected dish is not costed (DEC-087) |
| `EffectiveFoodCostPercentage` | `SelectedFoodCostPerPax ÷ AmountPax × 100` | `AmountPax` is null or ≤ 0, or the selected cost is null |
| `IsCeilingBreached` | `SelectedFoodCostPerPax > CostCeilingPerPax` | never (it is `false` when the selected cost is null) |
| `CeilingShortfallPerPax` | `max(0, SelectedFoodCostPerPax − CostCeilingPerPax)` | the selected cost is null, or the ceiling is null |
| `CeilingOveragePercentage` | `max(0, SelectedFoodCostPerPax − CostCeilingPerPax) ÷ CostCeilingPerPax × 100` | the selected cost is null, or the ceiling is null or ≤ 0 |
| `StressedFoodCostPerPax` | `SelectedFoodCostPerPax × (1 + StressPercentage ÷ 100)` | the selected cost is null |
| `GrossRevenue` | `AmountPax × Pax` | `AmountPax` is null, or no pax is resolved |
| `FoodCostPerPax` | the selection's `Σ FoodCost`, else the standard run | neither is available |
| `FoodCostTotal` | `FoodCostPerPax × Pax` | `FoodCostPerPax` is null, or no pax is resolved |
| `BaseTotal` | `AmountPax × Pax` | `AmountPax` is null, or no pax is resolved |
| `ExtendedTotal` | `EffectiveAdditionalRate × Pax` | no area is selected, or no pax is resolved |
| `EffectiveRatePerPax` | `AmountPax + EffectiveAdditionalRate` | `AmountPax` is null |
| `TotalContractValue` | `BaseTotal + ExtendedTotal` | `AmountPax` is null, or no pax is resolved |
| `LogisticsCost` | `Area.LogisticsCost` | the municipality has no logistics cost, or no area is selected |
| `GrossProfit` | `TotalContractValue − FoodCostTotal − LogisticsCost` | any operand is null |
| `GrossProfitPercentage` | `GrossProfit ÷ TotalContractValue × 100` | `TotalContractValue` is null or ≤ 0 |
| `BaseGrossProfitPercentage` | `(AmountPax − StandardRunFoodCostPerPax) ÷ AmountPax × 100` | the standard run is null, or `AmountPax` is null or ≤ 0 |

**An uncosted selected dish (DEC-087).** When any selected dish is not costed, the selected food cost is **null** — *unknown*, never zero. The quota verdict still **counts** the dish (it was selected; it satisfies or breaks a quota), `isCostKnown` is `false`, and `isCeilingBreached` is `false` because there is no known cost to compare against the ceiling. The screen says "the food cost of this selection is unknown" rather than showing a misleading zero.

**Rounding (DEC-003, RSK-029).** Every money figure is rounded to two decimals with `MidpointRounding.AwayFromZero`; every percentage likewise to two decimals, away from zero. The rounding happens **once**, in the Application layer, and the value published is the rounded one — the browser never re-rounds.

### 3.5 Rule compliance

For each course rule, the compliance verdict compares the number of selected dishes in that course with `RequiredQuota`:

- `SelectedCount == RequiredQuota` → **compliant**.
- otherwise → **not compliant**, and the response states both counts.

"Exactly" is the reference's own wording ("Enforce exactly 1 Beef selection") and the design's own verdict text ("satisfied (1/1)"). A rule with `RequiredQuota = 1` and two selected dishes is therefore **not** compliant.

**Two verdicts are published, and they are not the same thing (DEC-080).**

| Field | Meaning |
| :--- | :--- |
| `isQuotaCompliant` | Every rule is satisfied **and** no selection names a course the tier holds no rule for. |
| `isCompliant` | The **overall** verdict: `isQuotaCompliant` **and** the selection does not breach the cost ceiling. |

The owner ruled (DEC-080) that a cost-ceiling breach **marks the selection non-compliant**. The quota verdict and the overall verdict are therefore published separately, so the screen can say *which* condition failed. A selection naming a course with **no** rule is reported as an **unruled** selection and makes `isQuotaCompliant` false.

### 3.6 Validation (BR-11)

The validators are named explicitly here, because the set-level rules and the per-entry rules live in different classes. The limits are the **column** limits. The existence rules treat a **soft-deleted** course or area as "does not exist", so the lookups filter `IsDeleted` (DEC-087). Course rules no longer reference the add-on master (DEC-172).

| Validator | Scope | Rules and messages |
| :--- | :--- | :--- |
| `PackageCourseRulesetRequestValidator` | the **request** (a set of rules) | `Rules` must be present and not null → "At least one rule must be supplied." is **not** the message; a null member is "Rules must be supplied." · no repeated `CourseId` → "Course category {id} is configured more than once." |
| `PackageCourseRuleRequestValidator` | **one** rule (composed by the set validator) | `CourseId` must exist → "Course category {id} does not exist." · `RequiredQuota` ≥ 1 → "Required quota must be at least 1." · `PortionGrams` ≥ 0 when supplied → "Portion weight must be zero or greater." · `RuleDescription` required, ≤ 500 → "Rule description is required." / "Rule description must not exceed 500 characters." |
| `PackageAreaCoverageRequestValidator` | the **request** (a set of coverages) | `Coverages` must be present and not null → "Coverages must be supplied." · no repeated `AreaId` → "Area {id} is covered more than once." |
| `PackageAreaCoverageEntryValidator` | **one** coverage (composed by the set validator) | `AreaId` must exist → "Area {id} does not exist." |
| `AreaRequestValidator` | one municipality | `AreaDetails` required, ≤ 150 → "Area name is required." / "Area name must not exceed 150 characters." · `AdditionalRate` ≥ 0 when supplied → "Additional rate must be zero or greater." · `DistanceKm` ≥ 0 when supplied → "Distance must be zero or greater." · `LogisticsNote` ≤ 150 when supplied → "Logistics note must not exceed 150 characters." · `LogisticsCost` ≥ 0 when supplied → "Logistics cost must be zero or greater." |
| `PackageCourseRulePreviewValidator` | a preview request | `Selections` must be present and not null → "Selections must be supplied." · no repeated `menuId` → "Dish {menuId} is selected more than once." · every `menuId` must exist and not be soft-deleted → "Dish {menuId} does not exist." · every dish's own course must equal the named `CourseId` → "Dish {menuId} does not belong to course {courseId}." · `StressPercentage` ≥ 0 when supplied → "Stress percentage must be zero or greater." |
| `PackageLocationPreviewValidator` | a location preview | `Pax` ≥ 1 → "Pax must be at least 1." · the selection rules above, when `Selections` is supplied |

A **null collection member** is a rejected payload, not an empty set: an absent or explicitly `null` `rules` / `coverages` / `selections` member is a `400` with the message above. It is **never** treated as an empty set, because an empty set has a destructive meaning (§3.7). The request records therefore declare the collection members **nullable** and the validator rejects the null explicitly.

A missing tier is a `404`; a missing course, area or dish is a `400`; a rejected payload is a `400`. All of them carry `{ "errors": [ ... ] }`.

### 3.7 The write semantics — a ruleset is replaced, not patched

`PUT /api/packages/{id}/course-rules` and `PUT /api/packages/{id}/areas` **replace the whole set** the tier holds, because a ruleset and a coverage list are read as one document. The server:

1. **resolves the tier through the catalogue population** (`!IsDeleted`, DEC-074) and answers `404` for a soft-deleted tier — the writes refuse to touch a deleted tier, even though the shared entity fetch `IPackageRepository.GetByIdAsync` is deliberately unfiltered;
2. upserts the submitted entries (matched on the natural key — `CourseId` for a rule, `AreaId` for a coverage);
3. **soft-deletes** the entries that were not submitted (`IsDeleted = true`, the DEC-038 / DEC-063 convention);
4. commits in one `SaveChangesAsync` (the shared request-scoped `DbContext`, DEC-039).

**An empty set clears the tier (DEC-085).** `{"rules": []}` removes **every** live rule of the tier, and `{"coverages": []}` removes every live coverage row, by soft-deleting them. This is the documented meaning of an empty set and it is stated here so it is never inferred.

**A re-added natural key resurrects its row (DEC-088).** The upsert matches the natural key over **all** rows, including the soft-deleted ones, and **resurrects** a soft-deleted match (`IsDeleted = false`) rather than inserting a second row. Without this, removing a rule and later re-adding the same course would leave two rows with the same natural key and make the next write ambiguous.

**The database enforces it (DEC-088).** The migration creates a **filtered** unique index on `(`p_id`, `courseId`)` and on `(`p_id`, `areaId`)` with the predicate `is_deleted = 0`. A plain unique index would break the re-add path. The unit-test in-memory provider does not enforce filtered indexes, so the database-level guarantee is asserted by an **integration** test.

The read projections filter `IsDeleted == false` (DEC-074 pattern), so a removed rule disappears from the workbench but its row survives for audit.

---

## 4. API Surface

Base path `/api`. Every response is JSON unless stated.

| Method | Route | Purpose | Result |
| :--- | :--- | :--- | :--- |
| `GET` | `/packages/{id}/course-rules` | The tier's course-coverage ruleset, its qualified pools and the package-level planning figures | `200` `PackageCourseRulesetDto` |
| `PUT` | `/packages/{id}/course-rules` | Replace the tier's ruleset | `200` `PackageCourseRulesetDto` (the new state) |
| `POST` | `/packages/{id}/course-rules/preview` | The selection simulator **and** the headcount forecast | `200` `PackageCourseRulePreviewDto` |
| `GET` | `/packages/{id}/areas` | The tier's coverage matrix | `200` `PackageAreaCoverageMatrixDto` |
| `PUT` | `/packages/{id}/areas` | Replace the tier's coverage set | `200` `PackageAreaCoverageMatrixDto` (the new state) |
| `POST` | `/packages/{id}/areas` | Attach a municipality to the tier | `201` `{ "packageAreaCoverageId": n }` |
| `POST` | `/packages/{id}/areas/preview` | The out-of-town pricing simulator | `200` `PackageLocationPreviewDto` |
| `GET` | `/packages/{id}/spec-sheet` | The tier's printable spec sheet | `200` `application/pdf` |
| `GET` | `/areas` | The municipality catalogue | `200` `AreaDto[]` |
| `POST` | `/areas` | Add a municipality | `201` `{ "areaId": n }` |
| `PUT` | `/areas/{id}` | Update a municipality | `204` |

**Route safety.** `/packages/summary` remains a literal segment; `/packages/{id:int}` is int-constrained, so `/packages/{id}/course-rules` can never be captured by it. `/packages/{id}/areas/preview` and `/packages/{id}/areas` differ in segment count and cannot collide.

**Route scope.** No route is added, removed or renamed by this amendment. The existing package catalogue routes (`GET /api/packages`, `GET /api/packages/summary`, `GET /api/packages/{id}`, `POST /api/packages`, `PUT /api/packages/{id}` and `PATCH /api/packages/{id}/toggle-active`) remain unchanged (DEC-084). The course-rule DTO shape is intentionally amended by DEC-172; see v1.1 above.

**The tier population (F6).** Every one of the eleven endpoints resolves the tier through the **catalogue population** (`IsDeleted == false`, DEC-074) and answers `404 { "errors": [ "Package not found." ] }` for a soft-deleted tier. The **write** use cases refuse to write onto a soft-deleted tier. This is deliberately stricter than `IPackageRepository.GetByIdAsync`, which stays unfiltered for the booking path (BR-04).

### 4.1 DTO shapes

```csharp
// GET/PUT /api/packages/{id}/course-rules
public class PackageCourseRulesetDto
{
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;   // Package.Description
    public string PackageType { get; set; } = string.Empty;    // Package.PackageType
    public decimal? AmountPax { get; set; }
    public int? MinPax { get; set; }
    public bool IsActive { get; set; }
    public decimal TargetFoodCostPercentage { get; set; }     // DEC-040 benchmark
    public decimal TargetMarginPercentage { get; set; }
    public bool IsBenchmarkConfigured { get; set; }
    public decimal? CostCeilingPerPax { get; set; }
    public decimal? MinimumContractValue { get; set; }
    public decimal? StandardRunFoodCostPerPax { get; set; }
    public decimal? StandardRunFoodCostPercentage { get; set; }
    public decimal? BaseGrossProfitPercentage { get; set; }
    public List<PackageCourseRuleDto> Rules { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class PackageCourseRuleDto
{
    public int PackageCourseRuleId { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int RequiredQuota { get; set; }
    public int? PortionGrams { get; set; }
    public string RuleDescription { get; set; } = string.Empty;
     public int PoolSize { get; set; }
    public int PoolCostedCount { get; set; }
    public decimal? PoolAverageFoodCost { get; set; }
    public List<PackageCoursePoolDishDto> Pool { get; set; } = new();
}

public class PackageCoursePoolDishDto
{
    public string MenuId { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public decimal? FoodCost { get; set; }
    public decimal? Price { get; set; }
}
```

```csharp
// POST /api/packages/{id}/course-rules/preview
public record PackageCourseRulePreviewRequest(
    IReadOnlyList<PackageCourseSelectionDto>? Selections,
    decimal? StressPercentage = null,
    int? Pax = null);

public record PackageCourseSelectionDto(int CourseId, string MenuId);

public class PackageCourseRulePreviewDto
{
    public int PackageId { get; set; }
    public decimal? AmountPax { get; set; }
    public int? MinPax { get; set; }
    public int? Pax { get; set; }                              // the resolved headcount
    public decimal TargetFoodCostPercentage { get; set; }
    public decimal? CostCeilingPerPax { get; set; }
    public decimal? SelectedFoodCostPerPax { get; set; }
    public decimal? FoodCostPerPax { get; set; }               // the selection, else the standard run
    public decimal? EffectiveFoodCostPercentage { get; set; }
    public bool IsCostKnown { get; set; }                      // false when a selected dish is uncosted
    public bool IsCeilingBreached { get; set; }
    public decimal? CeilingShortfallPerPax { get; set; }
    public decimal? CeilingOveragePercentage { get; set; }      // DEC-080
    public decimal? StressPercentage { get; set; }
    public decimal? StressedFoodCostPerPax { get; set; }
    public decimal? StressedFoodCostPercentage { get; set; }
    public decimal? GrossRevenue { get; set; }                  // AmountPax * Pax
    public decimal? FoodCostTotal { get; set; }                 // FoodCostPerPax * Pax
    public decimal? GrossProfit { get; set; }                   // GrossRevenue - FoodCostTotal
    public decimal? GrossProfitPercentage { get; set; }
    public bool IsQuotaCompliant { get; set; }                 // the quota verdict alone
    public bool IsCompliant { get; set; }                      // the overall verdict (DEC-080)
    public List<PackageCourseComplianceDto> Compliance { get; set; } = new();
    public List<PackageCourseSelectionResultDto> Selections { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class PackageCourseComplianceDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int RequiredQuota { get; set; }
    public int SelectedCount { get; set; }
    public bool IsCompliant { get; set; }
    public bool HasRule { get; set; }        // false = the selection names a course the tier has no rule for
}

public class PackageCourseSelectionResultDto
{
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string MenuId { get; set; } = string.Empty;
    public string MenuName { get; set; } = string.Empty;
    public decimal? FoodCost { get; set; }   // null when the dish carries no food cost
}
```

```csharp
// GET/PUT /api/packages/{id}/areas
public class PackageAreaCoverageMatrixDto
{
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal? AmountPax { get; set; }
    public int? MinPax { get; set; }
    public decimal? MinimumContractValue { get; set; }
    public decimal? StandardRunFoodCostPerPax { get; set; }   // read from the ruleset (M2)
    public decimal? BaseGrossProfitPercentage { get; set; }   // read from the ruleset (M2)
    public int CoverageCount { get; set; }
    public int ActiveCoverageCount { get; set; }
    public decimal? LowestAdditionalRate { get; set; }
    public decimal? HighestAdditionalRate { get; set; }
    public decimal? LowestGrossProfitPercentage { get; set; }  // over the active coverage, at the floor
    public decimal? HighestGrossProfitPercentage { get; set; }
    public int GrossProfitCoverageCount { get; set; }          // the rows that yielded a figure
    public List<PackageAreaCoverageRowDto> Areas { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class PackageAreaCoverageRowDto
{
    public int PackageAreaCoverageId { get; set; }
    public int AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;   // Area.AreaDetails
    public decimal? AdditionalRate { get; set; }            // Area.AdditionalRate - the MUNICIPALITY's rate
    public int? DistanceKm { get; set; }
    public string? LogisticsNote { get; set; }
    public decimal? LogisticsCost { get; set; }
    public bool IsActive { get; set; }
}
```

```csharp
// POST /api/packages/{id}/areas/preview
public record PackageLocationPreviewRequest(
    int Pax,
    int? AreaId = null,
    IReadOnlyList<PackageCourseSelectionDto>? Selections = null);

public class PackageLocationPreviewDto
{
    public int PackageId { get; set; }
    public int Pax { get; set; }
    public decimal? AmountPax { get; set; }
    public int? AreaId { get; set; }
    public string? AreaName { get; set; }
    public decimal? AdditionalRate { get; set; }
    public decimal? BaseTotal { get; set; }
    public decimal? ExtendedTotal { get; set; }
    public decimal? EffectiveRatePerPax { get; set; }
    public decimal? TotalContractValue { get; set; }
    public decimal? FoodCostPerPax { get; set; }
    public decimal? FoodCostTotal { get; set; }
    public decimal? LogisticsCost { get; set; }
    public decimal? GrossProfit { get; set; }
    public decimal? GrossProfitPercentage { get; set; }
    public DateTime GeneratedAt { get; set; }
}
```

```csharp
// GET /api/areas  ·  POST /api/areas
public class AreaDto
{
    public int AreaId { get; set; }
    public string AreaDetails { get; set; } = string.Empty;
    public decimal? AdditionalRate { get; set; }
    public int? DistanceKm { get; set; }
    public string? LogisticsNote { get; set; }
    public decimal? LogisticsCost { get; set; }
}

public record AreaRequestDto(
    string AreaDetails,
    decimal? AdditionalRate,
    int? DistanceKm,
    string? LogisticsNote,
    decimal? LogisticsCost);
```

### 4.2 Request bodies for the writes

The collection members are **nullable** on purpose: an absent or explicitly `null` member is a **rejected payload** (`400`), never an empty set. An empty set has a destructive meaning (§3.7) and must be asked for explicitly.

```csharp
// PUT /api/packages/{id}/course-rules
public record PackageCourseRulesetRequest(
    IReadOnlyList<PackageCourseRuleRequestDto>? Rules);

public record PackageCourseRuleRequestDto(
    int CourseId,
    int RequiredQuota,
    int? PortionGrams,
    string RuleDescription);

// PUT /api/packages/{id}/areas
public record PackageAreaCoverageRequest(
    IReadOnlyList<PackageAreaCoverageEntryDto>? Coverages);

public record PackageAreaCoverageEntryDto(int AreaId, bool IsActive);

// POST /api/packages/{id}/areas
public record PackageAreaCoverageAttachDto(int AreaId, bool IsActive = true);
```

### 4.3 The `pax` guard

`PackageLocationPreviewRequest.Pax` must be = 1. A `pax` of 0 or less is a `400`: "Pax must be at least 1.". A `pax` below the tier's `MinPax` is **allowed** and reported — the preview is a what-if, not a booking, and the screen shows the floor so the operator can see they are below it.

**No maximum is ruled (M10).** The reference's slider runs to 500 pax, but no maximum is approved, so none is enforced; `AmountPax × Pax` is arithmetically safe for any `int`.

**The resolved headcount.** The preview's `pax` is the requested headcount when one is supplied, otherwise the tier's `MinPax`, otherwise `null`. When it is `null`, every headcount-dependent figure (`grossRevenue`, `foodCostTotal`, `grossProfit`, `grossProfitPercentage`) is `null` — the preview still reports the per-pax figures.

**The stress factor.** `stressPercentage` may be `null`, which means **no stress** (a factor of 1). A stress percentage of `0` is the same. A negative stress percentage is a `400`. `stressedFoodCostPerPax` is `null` when the selected food cost is `null`.
## 5. UI Specification

### 5.1 Routes

| Route | Element | State |
| :--- | :--- | :--- |
| `/packages` | `PackageCataloguePage` | Delivered (REQ-PKG-01) — the inspector gains the two new workbench actions |
| `/packages/new` | `PackageTierFormPage` | Delivered (REQ-PKG-01) |
| `/packages/:packageId/edit` | `PackageTierFormPage` | Delivered (REQ-PKG-01) |
| `/packages/:packageId/rules` | `PackageCourseRulesPage` | **New** (Screen A) |
| `/packages/:packageId/locations` | `PackageLocationsPage` | **New** (Screen B) |

Both new routes sit under the authenticated `AppLayout` shell. The catalogue's tier inspector gains **"Course Rules"** and **"Location Coverage"** actions that navigate to them, and the tier form gains a link to each after a successful save.

### 5.2 Screen A — Course Coverage Rules Workbench (`/packages/:packageId/rules`)

A three-column workbench on the reference's anatomy, inside the application shell (the reference's own header and footer are replaced by the shell's top bar and the application's content area, as the delivered screens do).

**Column 1 — the spec rail and the planning forecast**

| Block | Content | Source |
| :--- | :--- | :--- |
| Package identity | The tier's name, type, numeric key, active state | `Package` |
| Net pax rate | `AmountPax` and the cost ceiling per pax | server |
| Minimum contract value | `AmountPax × MinPax` | server (DEC-079) |
| The tier's commercial terms | A read-only summary with an **"Edit tier terms"** action → `/packages/:packageId/edit` | `Package` (DEC-082) |
| Planning forecast | The benchmark, the standard-run food cost, the base gross profit, a **headcount input** and the contract at that headcount | server - the headcount figures come from `POST /api/packages/{id}/course-rules/preview` with that `pax`; the browser never multiplies (DEC-002) |
| Spatial coverage | The tier's covered municipalities as a checklist, with the count | server (the coverage matrix payload) |

The reference's spec rail is **read-only** here (DEC-082): the reference's own field carries a lock glyph, and the tier form remains the single editor of the commercial terms. Two editors for one field is a defect, not a feature.

**Column 2 — the course-category rules and quota matrix**

- A header with the rule count and an **"Add course rule"** action.
- One card per rule: the course name, the quota badge, the standard portion, the rule description, the **pool size** and the qualified dish names, and a menu (edit / remove).
- A **"Save & apply course rules"** action that `PUT`s the whole ruleset and refreshes the screen.
- Each card shows its own edit affordance; the screen holds the unsaved state and the save applies all of it at once, because a ruleset is one document (section 3.7).

**Column 3 — the selection simulator and the compliance verdict**

- A **course × dish picker**: for each course, the operator picks a dish from its qualified pool.
- The selected-dish cost list, the **calculated dish cost per pax**, the **effective food cost %**, the **cost ceiling position** (the ceiling, the shortfall and the breach flag) and the **stress preview** (a percentage applied to the selected dishes' food cost).
- The **rule compliance** list: one line per rule with the required quota, the selected count and the verdict.
- A **"Reset selection"** action.

**Explicitly absent**: duplicate, publish-to-client, and the amenity / staples panels (capability-gap notices where the reference shows them).

### 5.3 Screen B — Location & Out-of-Town Add-On Matrix (`/packages/:packageId/locations`)

- A **header** with the tier's identity, the coverage count and a "Specification sheet" action (the PDF).
- A **four-card strip**, all server figures:
  1. **Minimum** — the guest bracket (`MinPax` and "and above") and the minimum contract value.
  2. **Base tariff** — `AmountPax` per guest and the minimum contract value.
  3. **Out-of-town coverage** — the active municipality count and the surcharge rate range.
  4. **Gross profit safeguard** — the base gross profit % and the logistics-adjusted range across the active coverage.
- The **surcharge matrix** — one row per covered municipality: the name, the coverage state, the **rate / pax**, the **extended total at the current headcount**, the distance, the dispatch note and the actions (edit the municipality, activate/deactivate the coverage, remove).
- The **municipality catalogue panel** — the catalogue of municipalities, with an add/edit form, so a new municipality can be created and then attached.
- The **course allocation card** — the tier's ruleset as a compact card grid (course, quota, portion, the pool's average food cost) plus a free-inclusion capability-gap notice. No entity carries free-inclusion records; course rules do not link add-ons (DEC-172).
- The **simulator** — the municipality selector, the headcount control, the breakdown (base total, extended total, effective rate per pax, the contract value) and the **gross-profit telemetry** (gross profit, food cost, logistics burn, the percentage and the split bar).
- The **mobile logistics checklist** — a capability-gap notice (no entity).
- A **save** action for the coverage set and a **revert** action.

### 5.4 The empty, loading and error states

- A tier with **no course rules** shows an empty ruleset with the "Add course rule" action — never a fabricated rule.
- A tier with **no coverage** shows an empty matrix with the "Attach a municipality" action — never a fabricated municipality.
- A tier whose **pool is empty** (no dish in that course) says so on the card.
- A tier with **no rate** (`AmountPax` null) shows an em dash for every money figure and states that the tier is not priced; the simulators are disabled with that reason.
- Loading, error-with-retry and the server's verbatim `errors[]` follow the delivered screens' pattern exactly.

### 5.5 Accessibility and presentation rules

- Currency is always `₱` with two decimals through `utils/currencyFormatter.ts`.
- Every status chip carries a text label beside its tone (WCAG 1.4.1) — the coverage state, the compliance verdict and the ceiling flag all carry a word.
- The compliance verdict is announced through an `aria-live="polite"` region so a re-run of the preview is announced.
- Every input has an explicit label, and every error message is linked with `aria-describedby`.
- The matrix table has a `<caption>` and `scope="col"` headers; the row actions carry an accessible name that includes the municipality.
- No horizontal overflow at 375 / 768 / 1024 / 1440 px.
- A feature-folder `noHardcodedFigures` scan fails the build if a peso, thousands separator, decimal literal, four-digit literal or percentage literal is hardcoded in the packages feature.

---

## 6. KPI Impact

| KPI | Definition | Status |
| :--- | :--- | :--- |
| KPI-29 | Configured course-rule coverage (rules configured / course categories) | PROPOSED |
| KPI-30 | Qualified-dish pool size (total, and costed) | PROPOSED |
| KPI-31 | Active area coverage count and the surcharge rate range | PROPOSED |
| KPI-32 | Standard-run food cost % of the per-pax rate, and the base gross profit % | PROPOSED |

Full definitions are appended to `business/kpi-catalog.md`. They are **planning** figures. None of them is revenue, none enters a booking total, and none of them is a commitment.

---

## 7. Acceptance Criteria

| ID | Criterion |
| :--- | :--- |
| AC-P2-01 | `/packages/:packageId/rules` renders the spec rail, the planning forecast, the rules matrix, the simulator and the compliance verdict from the API payloads. |
| AC-P2-02 | Every figure on both screens is a server figure. The browser computes no money and no percentage. A null figure renders an em dash, never `0`. |
| AC-P2-03 | The ruleset is read from `GET /api/packages/{id}/course-rules`; the qualified pool of each rule is the non-deleted dishes of that course, with the pool's size and its costed count. |
| AC-P2-04 | `PUT /api/packages/{id}/course-rules` replaces the ruleset; a removed rule disappears from the workbench and its row is soft-deleted, not destroyed. |
| AC-P2-05 | The preview reports the selected dishes' cost per pax, the effective food cost %, the cost ceiling, the breach flag, the shortfall in pesos and the **percentage over the ceiling**. A breach **warns**: it does not block and it does not change any price, but it **does** mark the overall verdict non-compliant (DEC-080). |
| AC-P2-06 | The compliance verdict compares the selected count with the required quota, per rule. The **quota verdict** (isQuotaCompliant) is the AND of the rules and is false when the selection names a course with no rule; the **overall verdict** (isCompliant) is the quota verdict AND the absence of a ceiling breach. The screen shows which of the two failed. |
| AC-P2-07 | The stress preview multiplies the selected dishes' food cost by the entered percentage and reports the stressed cost and percentage. A null or zero stress percentage means no stress. It changes nothing else. |
| AC-P2-08 | `/packages/:packageId/locations` renders the four-card strip, the surcharge matrix, the municipality catalogue and the simulator from the API payloads. |
| AC-P2-09 | The matrix shows each covered municipality's rate per pax, distance, dispatch note and coverage state. The rate is the municipality's, and the screen says so. |
| AC-P2-10 | The location preview reports the base total, the extended total, the effective rate per pax, the contract value, the food cost, the logistics cost, the gross profit and the gross-profit percentage, at the requested headcount. |
| AC-P2-11 | A municipality with no logistics cost omits the logistics band; it is never shown as zero. |
| AC-P2-12 | The minimum contract value is `AmountPax × MinPax` and is published as a derived floor. It is null when either figure is unset. |
| AC-P2-13 | A tier with no rules, no coverage, no rate or an empty pool shows an explicit empty state — never a fabricated rule, municipality, dish or price. |
| AC-P2-14 | A failed load renders the error banner with the server's message and a Retry action; the server's `errors[]` are rendered verbatim on a rejected write, and the operator's typed input survives the rejection. |
| AC-P2-15 | The packages feature folder contains no hardcoded peso, thousands separator, decimal, four-digit or percentage literal. |
| AC-P2-16 | The existing catalogue inspector and tier form are updated: the "course allocations" and "venue coverage" capability-gap notices are **replaced** by the real workbench links, and the "per-tier food cost" notice is narrowed to the figure that is still unavailable (a stored, per-tier food cost). |
| AC-P2-17 | The spec sheet downloads as a PDF and carries the tier's identity, its ruleset, its coverage and its planning figures. |
| AC-P2-18 | No migration is applied to an existing or production database. For DEC-172, generate and script-review the destructive migration but do not apply it to the development database; execute migrations only on disposable integration-test databases for validation. |
| AC-P2-19 | Every route renders with zero console errors, and there is no horizontal overflow at 375 / 768 / 1024 / 1440 px. |
| AC-P2-20 | The reference's duplicate, publish, bulk-edit, amenity, staples and mobile-logistics affordances are absent or are labelled capability-gap notices. |
| AC-P2-21 | Every new endpoint resolves the tier through the catalogue population: a **soft-deleted** tier answers 404 { "errors": [ "Package not found." ] } on every one of the eleven endpoints, and no write touches it. |
| AC-P2-22 | A 
ull or absent ules / coverages / selections member is a 400 with the stated message. It is **never** treated as an empty set. |
| AC-P2-23 | An **empty** set is honoured: {"rules": []} removes every live rule of the tier and {"coverages": []} removes every live coverage row, by soft-deleting them. |
| AC-P2-24 | A rule removed and later re-added **resurrects** its row: the natural key matches over all rows, so no second row is created, and the database's filtered unique index refuses a second live row for the same natural key. |
| AC-P2-25 | A selection that names a dish which does not exist, is soft-deleted, belongs to a **different** course, or repeats a menuId is a 400 with the stated message. |
| AC-P2-26 | A selected dish that is not costed makes the selected food cost **null** - unknown, never zero. The quota verdict still counts the dish, isCostKnown is false and isCeilingBreached is false. |
| AC-P2-27 | A municipality with **no stated rate** still yields a complete contract value: the rate is published as 
ull ("not stated") and the money chain treats the absent surcharge as zero (DEC-086). |
| AC-P2-28 | The preview's headcount figures (gross revenue, food cost total, gross profit and its percentage) are the server's. The browser never multiplies a rate by a headcount. |
| AC-P2-29 | The coverage matrix's gross-profit range states its inputs: the food cost is the **standard run**, the headcount is the tier's **floor**, and the range is null when MinPax is null, when there is no active coverage, or when no active coverage yields a computable figure. grossProfitCoverageCount says how many rows contributed. |
| AC-P2-30 | The dead PackageCoverageDto is **deleted**; the real PackageAreaCoverageRowDto replaces it (RSK-032). |
| AC-P2-31 | Course-rule persistence and API contain only course, quota, portion and description rule data (plus existing identity/audit and derived pool data). The Packages & Buffets add/edit form and rule cards do not expose an inclusion tag or upgrade add-on. Booking quote/menu course projections and the spec sheet do not publish/render those retired fields. The migration drops the columns, FK and index; `Down` restores schema only, not values. Unrelated booking/add-on-master `addonId` behavior is unchanged. |

---

## 8. Required Tests

**Backend (TASK-PKG-02-BE)**

- `PackageCourseRuleValidatorTests` — every rejection message; a valid payload passes; a duplicate course is refused.
- `PackageAreaCoverageValidatorTests` — every rejection message; a duplicate area is refused.
- `AreaRequestValidatorTests` — every rejection message; a valid payload passes.
- `PackageCostingCalculatorTests` — the ceiling, the effective percentage, the breach and the shortfall, the standard run over a known pool, the minimum contract value, the location breakdown and the gross-profit split, **including the null cases and the away-from-zero rounding**.
- `PackageCourseRulesetQueryTests` — the ruleset, the pool, the pool's costed count and the standard run over a seeded population.
- `PackageCourseRulePreviewTests` — the compliance verdicts (satisfied, short, over, unruled course), the ceiling breach and the stress preview.
- `PackageAreaCoverageQueryTests` — the matrix, the coverage counts and the rate range.
- `PackageLocationPreviewTests` — the breakdown at a known headcount, the logistics band omitted when the cost is null, and the gross-profit percentage.
- `PackageCourseRuleCommandTests` — a rejected payload persists nothing; a rule removed from the submitted set is soft-deleted, not destroyed; an unknown course is a `400`.
- `PackageAreaCoverageCommandTests` — the same for the coverage set; attaching an unknown area is a `400`.
- `AreaCommandTests` — a rejected payload persists nothing; an update to a missing area is a `404`.
- `PackageRepositoryTests` — the new projections over the seeded population.

**DEC-172 amendment tests (TASK-PKG-03-BE / TASK-PKG-03-FE)**

- Assert the package course-rule request/response and booking course projections omit the retired inclusion/upgrade members; old optional request members, if sent, are ignored rather than made strict model-binding failures.
- Assert the migration-created `PackageCourseRule` schema has no `inclusionTag` or `addonId` column, FK or add-on index, and that `Down` restores the nullable schema without claiming to recover prior values. Run only against the disposable integration-test database; do not apply to the development, existing/persistent or production database (DEC-020).
- Assert the Packages & Buffets editor/card have no inclusion-tag or upgrade-add-on controls/content, the save payload contains only remaining rule fields, and the booking course board does not render an inclusion badge.
- Integration: `GET /api/packages/{id}/course-rules` returns `200` with the envelope; `PUT` with a duplicate course returns `400 { "errors": [...] }`; `GET /api/packages/{id}/areas` returns `200`; `POST /api/areas` returns `201 { "areaId" }`; `PUT /api/areas/{id}` returns `204`; the preview returns `200` with the expected figures; the spec sheet returns `200` with a PDF content type; the migration chain still applies to an empty database. **The database refuses a second live row for the same natural key** (the filtered unique index) and **a re-added key does not create a duplicate row**. Every endpoint answers `404` for a soft-deleted tier. The benchmark is the base configuration's `35.0` (the test host runs in the `Testing` environment), so the ceiling and breach assertions are deterministic.

**Frontend (TASK-PKG-02-FE)**

- `PackageCourseRulesPage.test.tsx` — the ruleset rendering, the pool, the empty state, the simulator, the compliance verdicts, the ceiling-breach warning, the save, the server's verbatim `errors[]` and the capability-gap notices.
- `PackageLocationsPage.test.tsx` — the four-card strip, the matrix, the catalogue panel, the simulator breakdown, the omitted logistics band, the empty states and the save.
- `PackageCataloguePage.test.tsx` (extended) — the two new workbench actions and the narrowed capability-gap notices.
- `PackageTierFormPage.test.tsx` (extended) — the links to the two workbenches.
- `noHardcodedFigures.test.ts` — the feature scan.

---

## 9. Open Questions for the Owner

These do **not** block the delivery; they are recorded for a later cycle.

1. **A per-package rate override.** The surcharge is one rate per municipality, so a tier that covers a municipality cannot negotiate its own rate there. A `PackageAreaCoverage.AdditionalRate` override column would fix it, at the cost of two money columns for one concept. Not implemented (DEC-078).
2. **A stored per-tier food cost.** The workbench derives the food cost from a selection or from the standard-run estimate. A tier could instead carry a *committed* food cost, which is what a contract would actually bind. Not implemented.
3. ~~**The standard run's authority.**~~ **Resolved by the owner on 2026-10-22: the mean-of-pool basis is approved (DEC-083, now CONFIRMED).** The standard run remains a *planning estimate* rather than a commitment; if a committed figure is ever wanted it must become an explicit per-tier value (question 2).
4. **The municipality's "extended area" meaning.** BR-01 states an extended-area fee on a booking, but **the as-built code does not charge it** (`BookingAmountDueCalculator` passes `extendedAreaRate: 0m` on every path), so the workbench's rate affects only the workbench's own quotes today. **Wiring BR-01's extended-area rate through the booking path is a separate, owner-approved task** and is not assumed here. Whether a municipality is ever an "extended area" of an in-town venue, or always an out-of-town destination, is likewise not stated anywhere.
5. **The tier's guest ceiling.** `Package` has a floor (`MinPax`) and no ceiling. The design's "80 to 100" bracket has no source, so only the floor is shown.
6. **The flat out-of-town percentage.** The reference's "+15% Total Contract" surcharge is not implemented; the model carries a per-pax rate. Whether a percentage surcharge is also a commercial rule is for the owner.

---

## 10. Task Split

| Task | Owner | Deliverables |
| :--- | :--- | :--- |
| TASK-PKG-02-BE | AGENT 02 (.NET Backend) | The two new domain entities, the `Area` extension, the migration, the EF configurations, the repository interfaces and implementations, the Application use cases, `PackageCostingCalculator`, the validators, the `AreasController`, the new `PackagesController` actions, the QuestPDF spec sheet, the unit tests and the integration tests. |
| TASK-PKG-02-BE | AGENT 02 | The Application layer must not reference EF Core (DEC-037); every repository interface lives in `Application\Common\Interfaces` and every implementation in `Infrastructure\Persistence\Repositories`. |

Both tasks cite `docs/salscatersuite_project_structure_workplan.md` sections 2, 3 and 4 as the placement constraint, and both are bound by its alignment gate.
