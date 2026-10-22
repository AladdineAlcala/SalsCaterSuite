# Sal's CaterSuite — Business Intelligence & KPI Catalog

Every metric in Sal's CaterSuite must have an authoritative mathematical formula, an authoritative source database entity, explicit filters, and a clearly defined date basis. No dashboard totals may be displayed without matching these specifications.

---

### KPI-01: Monthly Contracted Catering Revenue
- **Business Meaning:** Total contracted net revenue from events scheduled within a target month.
- **Authoritative Formula:**
  $$\text{Monthly Revenue}(M, Y) = \sum_{b \in B_{(M,Y)}} b.\text{NetAmountDue}$$
- **Where:** $B_{(M,Y)} = \{ b \in \text{Booking} \mid b.\text{Status} \neq \text{Cancelled} \land \text{YEAR}(b.\text{EventDate}) = Y \land \text{MONTH}(b.\text{EventDate}) = M \}$
- **Authoritative Source:** `Booking` joined with line items/discounts.
- **Date Basis:** `EventDate` (Event Date, accrual revenue view).
- **Exclusions:** Cancelled bookings (`Status = Cancelled`), soft-deleted records.

---

### KPI-02: Total Collections (Cash Inflow)
- **Business Meaning:** Actual money collected across all payment instruments within a date window.
- **Authoritative Formula:**
  $$\text{Collections}(t_1, t_2) = \sum_{p \in P_{(t_1, t_2)}} p.\text{AmountPaid}$$
- **Where:** $P_{(t_1, t_2)} = \{ p \in \text{Payment} \mid p.\text{DateOfPayment} \ge t_1 \land p.\text{DateOfPayment} \le t_2 \}$
- **Authoritative Source:** `Payment` table (`amt_Payment`).
- **Date Basis:** `DateOfPayment` (Cash basis).
- **Note:** Independent of event dates; includes prepayments for future events and settlements for past events.

---

### KPI-03: Total Outstanding Accounts Receivable (AR)
- **Business Meaning:** Total uncollected balance owed by customers across all active (unserved + served) non-cancelled bookings.
- **Authoritative Formula:**
  $$\text{Total AR} = \sum_{b \in \text{ActiveBookings}} \max(0.00, b.\text{NetAmountDue} - \sum p.\text{AmountPaid})$$
- **Where:** $\text{ActiveBookings} = \{ b \in \text{Booking} \mid b.\text{Status} \neq \text{Cancelled} \}$
- **Authoritative Source:** Calculated dynamically via `BookingAmountDueCalculator` on `Booking` + `Payment`.
- **CRITICAL AUDIT NOTE:** Must never sum `Payment.amt_Payment` (which was the bug in `CustomersController`).

---

### KPI-04: Average Booking Value (ABV)
- **Business Meaning:** The mean contracted value per event.
- **Authoritative Formula:**
  $$\text{ABV}(M, Y) = \frac{\text{Monthly Revenue}(M, Y)}{|B_{(M,Y)}|}$$
- **Where:** $|B_{(M,Y)}|$ is the count of non-cancelled bookings in the month.

---

### KPI-05: Dish Food Cost Margin & Percentage
- **Business Meaning:** Profitability indicators per menu item.
- **Authoritative Formulas:**
  $$\text{Unit Margin} = \text{Menu.Price} - \text{Menu.FoodCost}$$
  $$\text{Food Cost \%} = \begin{cases} \frac{\text{Menu.FoodCost}}{\text{Menu.Price}} \times 100 & \text{if } \text{Menu.Price} > 0 \\ 0.00 & \text{otherwise} \end{cases}$$
- **Authoritative Source:** `Menu` table (`price`, `food_cost`).

---

### KPI-06: Net Collections (After Refunds)
- **Business Meaning:** Total cash retained after deducting customer refunds from cancellations.
- **Authoritative Formula:**
  $$\text{Net Collections}(t_1, t_2) = \text{Collections}(t_1, t_2) - \sum_{r \in R_{(t_1, t_2)}} r.\text{NetAmount}$$
- **Where:** $R_{(t_1, t_2)} = \{ r \in \text{Refund} \mid r.\text{RefundDate} \in [t_1, t_2] \land r.\text{Status} = 1 \}$

---

# KPI Catalog — Operations Dashboard Extension (REQ-DSH-01)

> Added by the dashboard feature specification v1.0 (2026-10-19).
> Full definitions, bounds and null semantics: `business/dashboard-feature-spec.md` §6.
> Wire contract: `architecture/approved-api-contracts.md` §9.
> All dashboard KPIs are computed **server-side**; the client displays only (DEC-002).

---

### KPI-07: Active Bookings, Committed vs Tentative & Contracted Pipeline Value
- **Business Meaning:** How many live catering contracts exist in the reporting period, how firm they are, and what peso value they represent.
- **Authoritative Formula:**
  $$\text{Count}(P) = |\{ b \in B_P \}|,\quad B_P = \{ b \mid b.\text{EventDate} \in P \land b.\text{Status} \notin \{\text{Cancelled}, \text{Expired}\} \}$$
  $$\text{ContractedValue}(P) = \sum_{b \in B_P} b.\text{NetAmountDue}$$
- **Status split (BR-10):** Committed = `{Confirmed, DepositPaid, FullyPaid}` · Tentative = `{Pending}` · Other = `{InProgress, Completed, NoShow, Rescheduled}`.
- **Date Basis:** `Booking.EventDate` (the event date), within the active reporting period.
- **Authoritative Source:** `Booking` + `Payment`; `NetAmountDue` via `BookingAmountDueCalculator` on the ledger's argument set.
- **Exclusions:** `Cancelled`, `Expired`, soft-deleted rows.

---

### KPI-08: Net Contracted Catering Revenue (Dashboard Scope)
- **Business Meaning:** The dashboard's presentation of KPI-01, scoped to the active reporting period.
- **Authoritative Formula:** identical to KPI-01 — $$\sum_{b \in B_P} b.\text{NetAmountDue}$$
- **Date Basis:** `Booking.EventDate`. **Exclusions:** `Cancelled`, `Expired`, soft-deleted.

---

### KPI-09: Average Booking Value (Dashboard Scope)
- **Business Meaning:** The dashboard's presentation of KPI-04.
- **Authoritative Formula:** $$\text{ABV}(P) = \frac{\text{Revenue}(P)}{|B_P|}$$
- **Null semantics:** `null` when $|B_P| = 0$ (the UI renders `—`, never `0`).

---

### KPI-10: Total Collections (Dashboard Scope)
- **Business Meaning:** The dashboard's presentation of KPI-02.
- **Authoritative Formula:** $$\text{Collections}(P) = \sum_{p \in P_P} p.\text{AmountPaid}$$
- **Date Basis:** `Payment.DateOfPayment` (cash basis).

---

### KPI-11: Collection Ratio
- **Business Meaning:** An indicative measure of how much of the period's billed revenue has been collected in cash.
- **Authoritative Formula:** $$\text{CollectionRatio}(P) = \frac{\text{Collections}(P)}{\text{Revenue}(P)} \times 100$$
- **Null semantics:** `null` when `Revenue(P) = 0`.
- **Interpretation caveat (must be disclosed in the UI):** the numerator is cash-basis, the denominator accrual-basis. The ratio is indicative, **not** a settlement rate, and can exceed 100% when prior-period receivables are collected in-period.
- **Source:** KPI-10 ÷ KPI-08.

---

### KPI-12: Total Outstanding Accounts Receivable (As-Of)
- **Business Meaning:** The dashboard's presentation of KPI-03, as a point-in-time snapshot (not period-scoped).
- **Authoritative Formula:** $$\text{TotalAR} = \sum_{b \in \text{ActiveBookings}} \max(0.00, b.\text{NetAmountDue} - \sum p.\text{AmountPaid})$$
- **Where:** $\text{ActiveBookings} = \{ b \mid b.\text{Status} \ne \text{Cancelled} \}$ (soft-deleted excluded).
- **Reconciliation:** must equal the sum of the per-booking ledger `balanceDue` for the same population, to the cent (DEC-015, DEC-025).
- **Note:** a `Completed` booking with an outstanding balance remains included (DEC-025).

---

### KPI-13: AR Aging & Overdue (As-Of)
- **Business Meaning:** How long the outstanding receivables have been overdue, and how much is more than 30 days late.
- **Due date:** `Booking.EventDate` — the balance is collectible once the event date has passed (DEC-041, owner-ruled).
- **Days overdue:** $\text{daysOverdue} = \text{today} - b.\text{EventDate}$ when $b.\text{EventDate} < \text{today}$; otherwise the booking is *not yet due*.
- **Authoritative Formula (buckets):**

  | Bucket key | Label | Condition |
  | :--- | :--- | :--- |
  | `NotYetDue` | Not yet due | `EventDate = today` |
  | `Days1To30` | 1–30 days | `1 = daysOverdue = 30` |
  | `Days31To60` | 31–60 days | `31 = daysOverdue = 60` |
  | `Days61To90` | 61–90 days | `61 = daysOverdue = 90` |
  | `Over90Days` | 90+ days | `daysOverdue > 90` |

- **Overdue (>30 days)** = the union of `Days31To60`, `Days61To90` and `Over90Days`; reports a count and an amount.
- **Invariant:** the five bucket amounts sum exactly to KPI-12's `TotalAR`, and every AR-contributing booking falls in exactly one bucket.
- **Closes:** the REQ-REC-02 date-basis gap for the dashboard.

---

### KPI-14: Dish Costing Coverage (As-Of)
- **Business Meaning:** What proportion of the menu catalogue actually carries a food cost. A **data-completeness** measure, not a profitability measure.
- **Authoritative Formula:**
  $$\text{Coverage} = \frac{|\{ m \mid m.\text{FoodCost} > 0 \}|}{|\{ m \mid m \text{ not soft-deleted} \}|} \times 100$$
- **Costed** = `FoodCost.HasValue && FoodCost > 0`. **Needs costing** = `FoodCost` is null or `= 0`.
- **Zero-denominator semantics:** `0` when the catalogue is empty.

---

### KPI-15: Catalogue Gross Margin (As-Of)
- **Business Meaning:** The gross margin the menu catalogue yields today. **This is a catalogue measure, not the profitability of booked events.**
- **Population:** non-deleted menus with `Price > 0` **and** `FoodCost > 0`.
- **Authoritative Formula:**
  $$\text{CatalogueMargin\%} = \frac{\sum (m.\text{Price} - m.\text{FoodCost})}{\sum m.\text{Price}} \times 100$$
  $$\text{VarianceToBenchmark} = \text{CatalogueMargin\%} - \text{TargetMargin\%}$$
- **Benchmark:** `Dashboard:Benchmarks:TargetFoodCostPercentage` (configuration, default `35.0` per BR-07); `TargetMargin% = 100 - TargetFoodCostPercentage` (DEC-040).
- **Null semantics:** `null` when the catalogue revenue is `0`.
- **Scope limitation:** true banquet margin requires the `BookingMenu` junction and recipe/ingredient costing, which do not exist. The UI must label this "Catalogue Margin".

---

### KPI-16: Food Cost Share by Course Category (As-Of)
- **Business Meaning:** How the catalogue's ingredient cost distributes across courses.
- **Authoritative Formula:**
  $$\text{Share}(c) = \frac{\sum_{m \in c} m.\text{FoodCost}}{\sum_{m \in \text{all courses}} m.\text{FoodCost}} \times 100$$
- **Population:** costed menus (KPI-14's costed set). Menus with a null `CourseId` are grouped under `Uncategorised`.
- **Ordering:** by `foodCostAmount` descending.

---

### KPI-17: Collections — Trailing 7 Days
- **Business Meaning:** Cash collected in the last seven calendar days, for the near-term cash position.
- **Authoritative Formula:** $$\text{Collections}_{7d} = \sum_{p \in P_{7d}} p.\text{AmountPaid},\quad P_{7d} = \{ p \mid p.\text{DateOfPayment} \in [\text{today} - 6, \text{today}] \}$$
- **Date Basis:** `Payment.DateOfPayment` (cash basis). Reports the amount and the receipt count.

---

### KPI-18: Bookings Awaiting Deposit
- **Business Meaning:** Live contracts that have not yet received any payment — the deposit exposure.
- **Authoritative Formula:**
  $$\{ b \mid b.\text{Status} \ne \text{Cancelled} \land \textstyle\sum_{p \in b} p.\text{AmountPaid} = 0 \}$$
  Reports the count and $$\sum b.\text{NetAmountDue}$$ over that population.
- **No deposit percentage is assumed.** DEC-009 (minimum deposit percentage) is **UNRESOLVED**; this KPI deliberately reports the full outstanding balance rather than an invented percentage (DEC-043).

---

### KPI-19: Official Receipts Issued (Period)
- **Business Meaning:** How many official receipts (BR-05 series `OR-YYYYMMDD-XXXX`) were issued in the period.
- **Authoritative Formula:** $$\left|\{ p \mid p.\text{DateOfPayment} \in P \}\right|$$
- **Date Basis:** `Payment.DateOfPayment`.


---

### KPI-20: Total Registered Clients (As-Of)
- **Business Meaning:** How many customer profiles the business has on record. The population size of the Customer Master directory.
- **Authoritative Formula:** $$\text{TotalClients} = \left|\{ c \in \text{Customer} \mid c.\text{IsDeleted} = \text{false} \}\right|$$
- **Where:** the `Customer` table.
- **Date Basis:** none — an **as-of snapshot** (glossary §4). Never scoped to a period.
- **Exclusions:** soft-deleted customers (DEC-050, scoped to the Customer Master's own queries).
- **Null semantics:** the count is `0` when there are no customers. This is a **population count**, not an undefined ratio, so `0` is the true value and not a null-as-zero violation (contract §9.2 rule 6 applies to *ratios*).
- **Status:** PROPOSED (DEC-050 dependency) — awaiting owner ratification.

---

### KPI-21: Total Contracted Value (As-Of)
- **Business Meaning:** The total net value the business has contracted with clients across all live bookings — the "lifetime billed" figure for the client base.
- **Authoritative Formula:** $$\text{TotalContractedValue} = \sum_{b.\text{Status} \ne \text{Cancelled}} b.\text{NetAmountDue}$$
- **Where:** `Booking`, with `NetAmountDue` computed through the single shared `BookingAmountDueCalculator.Calculate(booking, S Payment.AmountPaid)` overload (DEC-046), so the figure reconciles to the per-booking ledger to the cent (DEC-025).
- **Amount paid is** `S Payment.AmountPaid` (DEC-012) — never the denormalized `Booking.AmountPaid` cache.
- **Date Basis:** none — an **as-of snapshot**. "All-time" in the UI means "every non-cancelled booking ever recorded", not a period total.
- **Exclusions:** cancelled bookings. `Expired` bookings are **included**, consistent with DEC-025 / DEC-047 (the same population as AR).
- **Relationship to AR:** `TotalContractedValue` is what was billed; AR (KPI-12) is what remains unpaid. The difference is collections (KPI-02) plus overpayments. They are separate figures and must not be conflated (BR-08).
- **Per-client derivation:** the same sum restricted to one customer gives that customer's *Total Contracted Value*, *Total Paid* and *Outstanding Balance* on the Customer Master.
- **Status:** PROPOSED — awaiting owner ratification.

---

### KPI-22: Catalogue Average Food Cost Percentage (As-Of)
- **Business Meaning:** The weighted food cost the menu catalogue carries today, as a percentage of its selling price. The complement of KPI-15's catalogue margin, expressed the way the kitchen reads it.
- **Population:** the **margin population** — non-deleted menus with `Price > 0` **and** `FoodCost > 0` (the same population as KPI-15).
- **Authoritative Formula:**
  $$\text{AverageFoodCost\%} = \frac{\sum m.\text{FoodCost}}{\sum m.\text{Price}} \times 100$$
- **Headroom:** $$\text{Headroom} = \text{TargetFoodCost\%} - \text{AverageFoodCost\%}$$ — positive means the catalogue is inside the benchmark, negative means it is over.
- **Benchmark:** `Dashboard:Benchmarks:TargetFoodCostPercentage` (configuration, default `35.0` per BR-07) — the **same** source the dashboard and the menu-costing service read (DEC-040, AC-B8).
- **Date Basis:** none — an **as-of snapshot**.
- **Null semantics:** `null` when the margin population is empty. `headroom` is `null` in the same case. Never `0` and never `0%` (contract §9.2 rule 6).
- **Consistency:** `100 - AverageFoodCost%` equals KPI-15's catalogue margin over the same population, because KPI-15 is `S(Price - FoodCost) ÷ SPrice × 100`. The two are two views of one aggregate and must never disagree.
- **Scope limitation:** `FoodCost` is a **manually entered** decimal (DEC-062). This is the catalogue's entered cost as a share of its entered price, not a computed ingredient cost.
- **Status:** PROPOSED — awaiting owner ratification.

---

### KPI-23: Best-Margin Course (As-Of)
- **Business Meaning:** Which course in the catalogue currently yields the highest gross margin, and the typical price point it sells at.
- **Population:** the margin population, grouped by `CourseId`.
- **Authoritative Formula:** for each course `c`,
  $$\text{GrossMargin\%}(c) = \frac{\sum_{m \in c}(m.\text{Price} - m.\text{FoodCost})}{\sum_{m \in c} m.\text{Price}} \times 100$$
  The reported course is the one with the **highest** `GrossMargin%(c)`.
  $$\text{AverageSellingPrice}(c) = \frac{\sum_{m \in c} m.\text{Price}}{|\{ m \in c \}|}$$
- **Uncategorised:** menus with a null `CourseId` are grouped under `"Uncategorised"`, exactly as KPI-16 does, and may be the reported course.
- **Date Basis:** none — an as-of snapshot.
- **Null semantics:** `null` when the margin population is empty.
- **Ties:** where two courses share the highest margin, the one with the greater `S Price` is reported; if that also ties, the lower `CourseId` wins. The rule exists so the figure is deterministic.
- **Status:** PROPOSED — awaiting owner ratification.

---

### KPI-24: Margin Alert Count (As-Of)
- **Business Meaning:** How many dishes currently cost more to make than the configured food-cost benchmark allows. The actionable reading of "which dishes need re- costing".
- **Authoritative Formula:** $$\text{MarginAlertCount} = \left|\{ m \mid \tfrac{m.\text{FoodCost}}{m.\text{Price}} \times 100 > \text{TargetFoodCost\%} \}\right|$$
- **Population:** the **filtered** result set of the Menu & Food Costing Master — so the figure answers "of the dishes I am looking at, how many are over the benchmark?" — with a dish whose `Price` is `0` or null excluded (it has no meaningful percentage).
- **Benchmark:** `Dashboard:Benchmarks:TargetFoodCostPercentage` (DEC-040).
- **Date Basis:** none — an as-of snapshot.
- **Note:** the reference's "Cost Volatility Alert" (price-change percentages and audit counts) is **not** implementable — there is no price history (DEC-060). This KPI replaces it.
- **Status:** PROPOSED — awaiting owner ratification.

---

### KPI-25: Configured Package Tier Count (As-Of)
- **Business Meaning:** How many buffet tiers the catalogue holds, and how many of them are selectable right now.
- **Population:** every non-soft-deleted row in `Package`.
- **Authoritative Formula:**
  $$\text{TotalPackages} = \left|\{ p \in \text{Package} \mid p.\text{IsDeleted} = \text{false} \}\}$$
  $$\text{ActivePackages} = \left|\{ p \in \text{Package} \mid p.\text{IsDeleted} = \text{false} \land p.\text{IsActive} = \text{true} \}\}|$$
  $$\text{InactivePackages} = \text{TotalPackages} - \text{ActivePackages}$$
- **Authoritative Source:** `Package`, published by `GET /api/packages/summary` (DEC-068).
- **Date Basis:** none - an **as-of snapshot**.
- **Null semantics:** the counts are never `null`; they are `0` when the catalogue is empty. A `0` here is a real measurement (an empty catalogue), not an unknown value.
- **Note:** a tier that is inactive remains in the catalogue and in the count. It is excluded from **selection** in a booking, never from the catalogue. This KPI counts catalogue rows; it is **not** a revenue figure and never enters a booking total.
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-26: Catalogue Average Per-Pax Rate (As-Of)
- **Business Meaning:** The mean price per guest across the priced tiers - the single number that describes where the catalogue sits commercially.
- **Population:** the **priced population** - tiers with a non-null `AmountPax`.
- **Authoritative Formula:**
  $$\text{AverageAmountPax} = \frac{\sum_{p \in P} p.\text{AmountPax}}{|P|}$$
  where $P = \{ p \in \text{Package} \mid p.\text{IsDeleted} = \text{false} \land p.\text{AmountPax} \neq \text{null} \}$.
- **Authoritative Source:** `Package.AmountPax`, published by `GET /api/packages/summary` (DEC-068).
- **Date Basis:** none - an as-of snapshot.
- **Null semantics:** `null` when the priced population is empty. The client renders an em dash, never `0` and never `0.00` (contract section 9.2 rule 6).
- **Note:** a tier with a null rate is **not priced** and is excluded from the average; it is counted separately as `pricedPackages`. The browser never recomputes this average (DEC-002).
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-27: Catalogue Per-Pax Rate Range (As-Of)
- **Business Meaning:** The lowest and the highest per-guest rate in the catalogue - the commercial spread the coordinator quotes from.
- **Population:** the same priced population as KPI-26.
- **Authoritative Formula:**
  $$\text{LowestAmountPax} = \min_{p \in P} p.\text{AmountPax}, \qquad \text{HighestAmountPax} = \max_{p \in P} p.\text{AmountPax}$$
- **Authoritative Source:** `Package.AmountPax`, published by `GET /api/packages/summary` (DEC-068).
- **Date Basis:** none - an as-of snapshot.
- **Null semantics:** both values are `null` when the priced population is empty.
- **Relationship to KPI-26:** the range brackets the average; the three figures describe one population and are published by one endpoint, so they can never describe different populations.
- **Note:** this is a **list price**, not a realised price. It excludes every discount, add-on and extended-area charge, none of which exist for a tier (DEC-067).
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-28: Catalogue Minimum-Headcount Floor Range (As-Of)
- **Business Meaning:** The smallest and the largest minimum headcount the catalogue demands - the operational constraint the kitchen and the venue plan against.
- **Population:** the **floor population** - tiers with a non-null `MinPax`.
- **Authoritative Formula:**
  $$\text{LowestMinPax} = \min_{p \in F} p.\text{MinPax}, \qquad \text{HighestMinPax} = \max_{p \in F} p.\text{MinPax}$$
  where $F = \{ p \in \text{Package} \mid p.\text{IsDeleted} = \text{false} \land p.\text{MinPax} \neq \text{null} \}$.
- **Authoritative Source:** `Package.MinPax`, published by `GET /api/packages/summary` (DEC-068).
- **Date Basis:** none - an as-of snapshot.
- **Null semantics:** both values are `null` when the floor population is empty.
- **Note:** the floor is a **catalogue attribute**. It is not a booking headcount, and it is not the same thing as the minimum contract value (`AmountPax x MinPax`), which is **not** published because it is a commercial figure that has not been ruled on (spec section 9 question 4).
- **Status:** PROPOSED - awaiting owner ratification.
---

### KPI-29: Configured Course-Rule Coverage (As-Of)
- **Business Meaning:** How much of a banquet progression a tier actually constrains - the number of course categories for which the tier holds an enforced rule, against the number of course categories that exist.
- **Population:** the tier's **non-soft-deleted** `PackageCourseRule` rows, against the `CourseCategory` catalogue.
- **Authoritative Formula:**
  $$\text{ConfiguredRuleCount} = |\{ r \in \text{PackageCourseRule} \mid r.\text{PackageId} = p \land r.\text{IsDeleted} = \text{false} \}|$$
  $$\text{CourseCategoryCount} = |\{ c \in \text{CourseCategory} \mid c.\text{IsDeleted} = \text{false} \}|$$
- **Authoritative Source:** `GET /api/packages/{id}/course-rules` (`rules[]`), published alongside the `CourseCategory` catalogue (DEC-077).
- **Date Basis:** none - an as-of snapshot.
- **Null semantics:** the counts are never `null`; they are `0` when the tier holds no rule. A `0` here is a real measurement.
- **Note:** this is a **coverage** measure - how completely the tier is specified - not a quality or profitability measure. A tier with no rules is a valid state and is shown as one.
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-30: Qualified-Dish Pool Coverage (As-Of)
- **Business Meaning:** How many dishes a client can actually choose from in each course the tier constrains, and how many of them carry a food cost.
- **Population:** the non-soft-deleted `Menu` rows whose `CourseId` matches the rule's course.
- **Authoritative Formula:**
  $$\text{PoolSize}(r) = |\{ d \in \text{Menu} \mid d.\text{CourseId} = r.\text{CourseId} \land d.\text{IsDeleted} = \text{false} \}|$$
  $$\text{PoolCostedCount}(r) = |\{ d \in \text{Pool}(r) \mid d.\text{FoodCost} > 0 \}|$$
- **Authoritative Source:** `GET /api/packages/{id}/course-rules` (`rules[].poolSize`, `rules[].poolCostedCount`).
- **Date Basis:** none - an as-of snapshot.
- **Null semantics:** the counts are never `null`; they are `0` when the course has no dish.
- **Note:** a dish is **costed** when its food cost is **greater than zero** (DEC-087). `Menu.FoodCost` is nullable but the column carries a database default of `0.00` and the entity's own default is `0.00m`, so a dish created without a cost stores **zero**, not null - and a row inserted outside EF (including by the future legacy import, DEC-032) lands at `0.00` as well. Counting only the dishes above zero is therefore the only reading that keeps the pool average meaningful, and `poolCostedCount` is a **data-completeness** measure, not a costing measure. A course with no costed dish contributes nothing to the standard run (KPI-32).
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-31: Out-of-Town Coverage and Surcharge Range (As-Of)
- **Business Meaning:** The geographic reach of a tier - how many municipalities it can be served in - and the spread of the per-pax surcharge that reach costs.
- **Population:** the tier's **active** `PackageAreaCoverage` rows.
- **Authoritative Formula:**
  $$\text{ActiveCoverageCount} = |\{ c \in \text{PackageAreaCoverage} \mid c.\text{PackageId} = p \land c.\text{IsDeleted} = \text{false} \land c.\text{IsActive} = \text{true} \}|$$
  $$\text{LowestAdditionalRate} = \min_{c \in \text{Active}} c.\text{Area}.\text{AdditionalRate}, \qquad \text{HighestAdditionalRate} = \max_{c \in \text{Active}} c.\text{Area}.\text{AdditionalRate}$$
- **Authoritative Source:** `GET /api/packages/{id}/areas` (`activeCoverageCount`, `lowestAdditionalRate`, `highestAdditionalRate`) (DEC-077).
- **Date Basis:** none - an as-of snapshot.
- **Null semantics:** the rates are `null` when no active coverage carries a rate. The counts are `0` when the tier covers nothing.
- **Note:** the rate is **one rate per municipality**, not one per package (DEC-078), so this KPI describes the municipality tariff the tier inherits, not a negotiated tier-specific price. It is a **list price** and never enters a booking total.
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-32: Standard-Run Food Cost and Base Gross Profit (As-Of)
- **Business Meaning:** The planning margin of a tier before a client chooses a dish in each course - what the tier looks like on a standard run.
- **Population:** the tier's course rules and their qualified pools.
- **Authoritative Formula:**
  $$\text{StandardRunFoodCostPerPax} = \sum_{r \in R} \overline{\text{FoodCost}}_r \quad\text{where}\quad \overline{\text{FoodCost}}_r = \frac{\sum_{d \in \text{Pool}(r),\, d.\text{FoodCost} > 0} d.\text{FoodCost}}{\,|\{d \in \text{Pool}(r) : d.\text{FoodCost} > 0\}|\,}$$
  $$\text{StandardRunFoodCostPercentage} = \frac{\text{StandardRunFoodCostPerPax}}{\text{AmountPax}} \times 100, \qquad \text{BaseGrossProfitPercentage} = \frac{\text{AmountPax} - \text{StandardRunFoodCostPerPax}}{\text{AmountPax}} \times 100$$
- **Authoritative Source:** `GET /api/packages/{id}/course-rules` (`standardRunFoodCostPerPax`, `standardRunFoodCostPercentage`, `baseGrossProfitPercentage`) (DEC-083).
- **Date Basis:** none - an as-of snapshot.
- **Null semantics:** all three are `null` when the tier holds no rule, when no rule's pool holds a costed dish, or when `AmountPax` is null.
- **Note:** this is a **planning estimate, not a commitment** (DEC-083). It is the mean of the pool, not the cost of a booked menu, and it never enters a booking total. The *actual* food cost of a proposal is the **selection preview** (`POST /api/packages/{id}/course-rules/preview`).
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-33: Register Pipeline Count And Status Split (Filtered Population)
- **Business Meaning:** How many bookings the catering office is carrying, and what state they are in.
- **Population:** the **filtered** register population - every non-soft-deleted booking whose status is not `Cancelled`, subject to the active filters (BR-27, BR-32).
- **Authoritative Formula:**
  $$\text{BookingCount} = |P|, \qquad \text{ConfirmedCount} = |\{ b \in P : b.\text{Status} \in \{\text{Confirmed}, \text{DepositPaid}, \text{FullyPaid}\}\} |$$
  $$\text{TentativeCount} = |\{ b \in P : b.\text{Status} = \text{Pending} \}|, \qquad \text{CompletedCount} = |\{ b \in P : b.\text{Status} = \text{Completed} \}|$$
  $$ \text{OtherCount} = \text{BookingCount} - \text{ConfirmedCount} - \text{TentativeCount} - \text{CompletedCount} $$
- **Authoritative Source:** `GET /api/bookings/summary` (`pipeline.*`).
- **Date Basis:** the filter's own range, when one is applied; otherwise the whole register.
- **Null semantics:** the counts are never `null`; they are `0` for an empty population.
- **Note:** the split mirrors KPI-07's committed/tentative vocabulary but is **not** period-scoped and **does** include `Expired` and `NoShow` in "other" - the register describes the live book of work, the dashboard describes a reporting period (RSK-017).
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-34: Leading Tier, Its Share And Its Period Change (Filtered Population)
- **Business Meaning:** Which buffet tier carries the business right now, how much of the book it is, and whether it is growing.
- **Population:** the **filtered** register population.
- **Authoritative Formula:**
  $$\text{LeadingTier} = \arg\max_{t \in T} \sum_{b \in P : b.\text{PackageType} = t} \text{NetAmountDue}(b)$$
  $$\text{SharePercentage} = \frac{\text{LeadingTierNetContractedValue}}{\text{NetContractedValue}(P)} \times 100$$
  $$\text{ChangePercentage} = \frac{\text{LeadingTierNetContractedValue}(P) - \text{LeadingTierNetContractedValue}(P_{\text{preceding}})}{\text{LeadingTierNetContractedValue}(P_{\text{preceding}})} \times 100$$
- **Authoritative Source:** `GET /api/bookings/summary` (`leadingTier.*`).
- **Date Basis:** the preceding period is the **immediately preceding equal-length window** of the active date filter; with no date filter it is the equal-length window ending the day before the population's earliest event date.
- **Null semantics:** `leadingTier` is `null` for an empty population; `sharePercentage` is `null` when the population's net contracted value is zero; `changePercentage` is `null` when the preceding period has no bookings in the leading tier.
- **Note:** this is the register's **mix** measure. It is not a revenue figure and it never enters a booking total. It is the register-scoped counterpart of the catalogue's tier count (KPI-25), which counts catalogue rows and carries no money at all.
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-35: Net Contracted Value, Collected And Outstanding (Filtered Population)
- **Business Meaning:** The value of the book in the register, and how much of it has been paid for.
- **Population:** the **filtered** register population.
- **Authoritative Formula:**
  $$\text{NetContractedValue} = \sum_{b \in P} \text{NetAmountDue}(b), \qquad \text{CollectedTotal} = \sum_{b \in P} \text{Collected}(b), \qquad \text{OutstandingTotal} = \sum_{b \in P} \text{Outstanding}(b)$$
- **Authoritative Source:** `GET /api/bookings/summary` (`contractValue.*`) and `GET /api/bookings` (`summary.*`).
- **Date Basis:** none - the population's own filter.
- **Null semantics:** the amounts are never `null`; they are `0` for an empty population.
- **Note:** the invariant **`NetContractedValue = CollectedTotal + OutstandingTotal`** holds by construction (BR-27). `NetAmountDue` comes from the **one** calculator (BR-25) and `Collected` from the authoritative payment sum (BR-10), so the register cannot disagree with the ledger or the dashboard.
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-36: Committed Headcount (Filtered Population)
- **Business Meaning:** How many guests the book in the register commits the kitchen to.
- **Population:** the **filtered** register population.
- **Authoritative Formula:**
  $$\text{TotalPax} = \sum_{b \in P} b.\text{NumberOfHeads}, \qquad \text{AveragePaxPerBooking} = \frac{\text{TotalPax}}{|P|}$$
- **Authoritative Source:** `GET /api/bookings/summary` (`headcount.*`).
- **Date Basis:** none - the population's own filter.
- **Null semantics:** `totalPax` is `0` for an empty population; `averagePaxPerBooking` is `null` for an empty population.
- **Note:** a headcount is a **commitment**, not a guarantee. It is the figure the kitchen plans against and it is the same `NumberOfHeads` the booking stores (BR-04).
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-37: Payment-State Distribution (Filtered Population)
- **Business Meaning:** How the book splits across the three payment states - how much is still awaiting its first payment, how much is partly paid, how much is settled.
- **Population:** the **filtered** register population.
- **Authoritative Formula:** the count and the outstanding amount of each of the three states defined by **BR-28** (`AwaitingDeposit`, `PartiallyPaid`, `Settled`).
- **Authoritative Source:** `GET /api/bookings/summary` (`paymentStates[]`).
- **Date Basis:** none - the population's own filter.
- **Null semantics:** the counts are never `null`; a state with no bookings is simply absent from the list, and its amount is `0`.
- **Note:** the three states are mutually exclusive and exhaustive (BR-28), so the three counts sum to the population's booking count. This is the register-scoped counterpart of the dashboard's KPI-18 "bookings awaiting deposit". **Overdue is not a payment state** - it is a separate dimension with its own aggregate (KPI-39), because a partly-paid booking is both `PartiallyPaid` and overdue.
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-38: Near-Term Commissary Load Against Capacity (As-Of)
- **Business Meaning:** How much production the commissary has committed in the immediate window, and how that sits against its capacity.
- **Population:** the bookings whose event date falls in the **next 14 days** from today, within the filtered population.
- **Authoritative Formula:**
  $$\text{CommittedPax} = \sum_{b \in P_{14}} b.\text{NumberOfHeads}, \qquad \text{UtilisationPercentage} = \frac{\text{CommittedPax}}{\text{CapacityPax}} \times 100$$
- **Authoritative Source:** `GET /api/bookings/summary` (`capacity.*`), with `CapacityPax` read from the `Bookings:Capacity:MaximumPax` configuration key (DEC-102).
- **Date Basis:** the **next 14 days** from today.
- **Null semantics:** `capacityPax` is `null` when no capacity is configured, and `utilisationPercentage` is then `null` - **unknown**, never zero and never 100%. `committedPax` is `0` when nothing is committed in the window.
- **Note:** the capacity is **configuration**, not a constant, and the reference mockup's `400` is never hardcoded (DEC-102). BR-16 is explicit that the model has **no** guest ceiling, so this KPI is the first ceiling in the system and it is a **configuration**, not a business rule.
- **Status:** PROPOSED - awaiting owner ratification.

---

### KPI-39: Register Receivables And Overdue (Filtered Population)
- **Business Meaning:** The register's own receivables position and how much of it is already late.
- **Population:** the **filtered** register population.
- **Authoritative Formula:**
  $$\text{OutstandingAmount} = \sum_{b \in P} \max(0, \text{NetAmountDue}(b) - \text{Collected}(b))$$
  $$\text{OverdueCount} = |\{ b \in P : b.\text{Outstanding} > 0 \land b.\text{EventDate} < \text{today} \}|, \qquad \text{OverdueAmount} = \sum_{\text{overdue}} b.\text{Outstanding}$$
- **Authoritative Source:** `GET /api/bookings/summary` (`receivables.*`).
- **Date Basis:** the due date is the **event date** (DEC-041).
- **Null semantics:** the amounts are never `null`; they are `0` when nothing is outstanding.
- **Note:** with **no filter applied**, this KPI is the dashboard's KPI-12 and KPI-13 over the same population and **reconciles to the cent** - the register and the dashboard describe one book of work (BR-27, DEC-111).
- **Status:** PROPOSED - awaiting owner ratification.
