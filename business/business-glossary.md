# Sal's CaterSuite — Business Glossary & Domain Definitions

This glossary defines the authoritative business terminology for Sal's CaterSuite. All agents, developers, and stakeholders must strictly use these definitions.

---

### 1. Booking & Event Lifecycle Terms

- **Inquiry / Reservation (`Reservation`):** An initial customer expression of interest for a catering date, venue, and estimated pax count, prior to formal contract confirmation.
- **Booking (`Booking` / `Transaction`):** A confirmed catering event contract associated with a specific customer, event date/time, venue, guest count, and package. Identified by a unique `TransactionId` (`trn_Id`).
- **Pax (Headcount):** Number of guests/persons contracted for food service (`noOfPerson` / `noofPax`). **The printed catering contract labels this line "Guaranteed Pax"** — that is the document's own wording and it predates the Bookings & Events work (2026-10-22); the two are the same term, and a rule or a data field uses the business vocabulary while the signed document keeps its label.
- **Booking Lifecycle Status (`booking_status`):** The single authoritative **event lifecycle** state of a booking (BR-10, DEC-177). Its vocabulary is `Pending`, `Confirmed`, `InProgress`, `Completed`, `Cancelled`, `NoShow`, `Rescheduled`, `Expired`. Payment progress is reported separately and never overwrites this status. The legacy `serve_stat` (`ServeStatus`) / `is_cancelled` (`IsCancelled`) boolean pair is retired and must not be reintroduced.
- **Booking Payment Status (`paymentState`):** A separate server-derived view of payment progress (BR-28, DEC-177), classified from authoritative, non-soft-deleted payment entries and net amount due: `PendingPayment`, `DepositPaid`, or `FullyPaid`. The first recorded payment counts as a deposit; no deposit percentage is assumed. It is not changed by BG-04.
- **Event Executed / Served:** An event whose food service has been rendered. It is expressed by the lifecycle status `Completed` (and `InProgress` while the event is running) — **not** by a boolean column.
- **Cancellation:** The termination of a booking prior to execution, expressed by the lifecycle status `Cancelled`. It requires an explicit reason and a determination of refund or cancellation fee (BR-06).
- **Banquet Event Order (BEO):** The master operational preparation document used by kitchen, banquet servers, and logistics detailing schedule, menu, equipment, venue instructions, and guest count.

---

### 2. Menu, Costing & Package Terms

- **Course Category (`CourseCategory`):** The classification of dishes within a buffet service (e.g., Soup, Salad, Beef, Pork, Chicken, Seafood, Pasta, Rice, Dessert, Beverage).
- **Menu Item / Dish (`Menu`):** An individual culinary dish offered by Sal's Catering, identified by an alphanumeric `MenuId`. Includes item name, course category, selling price, and unit food cost.
- **Food Cost (`food_cost`):** The total direct ingredient cost required to prepare one serving/portion of a menu item.
- **Contribution Margin / Gross Margin:** The difference between dish selling price and food cost:  
  $$\text{Margin} = \text{Price} - \text{Food Cost}$$
- **Food Cost Percentage:**  
  $$\text{Food Cost \%} = \frac{\text{Food Cost}}{\text{Price}} \times 100$$
- **Catering Package (`Package`):** A pre-configured buffet bundle priced either per pax (`amount_pax`) or as a lump sum, specifying minimum pax requirements (`minPax`) and eligible course selections. The approved tier vocabulary is the `PackageType` enum: `Regular`, `Wedding`, `Vip`, `Premier`, `Pm` (afternoon tea / merienda) and `Sd` (special diet - halal, vegan, allergen). The `Package.PackageType` column is **free text** and is not typed to the enum, so any stored value is presented verbatim (DEC-070); the enum names are the values the application offers as suggestions. **Not carried by the model:** a tier's food cost, its course allocation, its event inclusions and its venue coverage (DEC-067).
- **Applicable Pax Classification (`PackagePaxApplicable`):** A required, fixed lookup value assigned to a package tier through `Package.numPaxId` (DEC-173). It is descriptive metadata only; its label is not parsed into numeric bounds and does not constrain package type, `MinPax`, booking headcount, eligibility, or pricing.
- **Add-on (`AddonDetail`):** Optional supplementary items or equipment rentals requested by the client (e.g., extra waiter, roasting pig station, chocolate fountain, mobile bar, tent, Tiffany chairs).
- **Extended Area (`Area`):** Catering delivery or execution outside standard catering territory incurring an additional logistical/transportation surcharge (`additionalRate`).

---

### 3. Financial, Billing & Accounting Terms

- **Base Package Amount:** The core package cost, calculated as `PackageAmount` (if lump sum) or `PricePerPax * NoOfPerson`.
- **Gross Contract Amount:** The total contract value before any deductions:  
  $$\text{Gross} = \text{Base Package Amount} + \text{Extended Area Charge} + \text{Add-ons Total} + \text{Other Charges}$$
- **Discount:** A price concession granted to the client, either as a percentage of gross or as an authorized fixed amount.
- **Net Amount Due (Contract Receivable Value):** The final contracted amount payable by the client:  
  $$\text{Net Amount Due} = \text{Gross Contract Amount} - \text{Discount}$$
- **Payment / Collection (`Payment`):** Cash, check, digital transfer, or credit received from the client toward their booking contract. Generates an Official Receipt (`PaymentNo` / `OR`).
- **Total Paid:** The cumulative sum of all validated payments received for a booking:  
  $$\text{Total Paid} = \sum \text{AmountPaid}$$
- **Remaining Balance (Booking Receivable):**  
  $$\text{Remaining Balance} = \max(0, \text{Net Amount Due} - \text{Total Paid})$$
- **Accounts Receivable (AR):** The aggregate uncollected balance owed to Sal's CaterSuite across all active (non-cancelled) bookings:  
  $$\text{Total AR} = \sum_{\text{active bookings}} (\text{Net Amount Due} - \text{Total Paid})$$
  *(Note: Must never be confused with Total Collections).*
- **Refund (`Refund`):** Money returned to a client upon booking cancellation after applying any agreed deductions or cancellation penalties:  
  $$\text{Net Refund} = \text{Refund Amount} - \text{Deduction}$$
- **Overpayment:** An excess of payments over net amount due. Handled either by credit toward another event or refund.

---

## 4. Dashboard & Telemetry Terms (added by REQ-DSH-01, 2026-10-19)

These terms are approved with the Operations Dashboard feature specification
(`business/dashboard-feature-spec.md` §11). They describe how the system **classifies** existing
records for reporting; they introduce no new business behaviour.

- **Active Period:** The calendar month a dashboard's flow metrics are scoped to, expressed as
  `YYYY-MM` and resolved **by the server**, which returns the exact boundaries it used. Defaults to
  the current UTC calendar month.
- **Period-scoped metric:** A metric over a *flow* within the active period — bookings, contracted
  value, revenue, collections, receipts issued. Date basis: `Booking.EventDate` for bookings,
  `Payment.DateOfPayment` for collections.
- **As-of metric:** A metric over a *snapshot* of the book at request time — accounts receivable,
  AR aging, dish costing coverage, catalogue margin, food cost by category, bookings awaiting
  deposit. **Never scoped to a period**; a past-month value of a snapshot is meaningless.
- **Committed booking:** A booking in `Confirmed` lifecycle status — a contract that is in force.
- **Tentative booking:** A booking in `Pending` status — created but not yet confirmed. *Distinct
  from a `Reservation`, which is a pre-contract expression of interest and is not a booking.*
- **Other / executed booking:** A booking in `InProgress`, `Completed`, `NoShow` or `Rescheduled`
  status — an event inside the period that is executed or exceptional.
- **Contracted Pipeline Value:** `S NetAmountDue` over a defined booking population. A commitment
  indicator, **not** revenue (revenue uses the event-date basis of KPI-01) and **not** cash.
- **Not yet due:** An AR balance on a booking whose **event date** has not yet passed.
- **Overdue:** An AR balance on a booking whose **event date** passed more than 30 days ago. The
  event date is the due date (DEC-041). Refunds do not reduce the balance (DEC-013).
- **Collection Ratio:** `Collections(period) ÷ Net Contracted Revenue(period) × 100`. An **indicative**
  measure only — the numerator is cash-basis and the denominator accrual-basis, so it can exceed
  100% when prior-period receivables are collected in-period. It is **not** a settlement rate.
- **Catalogue Margin:** The gross margin across the costed **menu catalogue**
  (`S(Price - FoodCost) ÷ SPrice`). **Not** the margin of booked events — banquet-level margin is
  not computable because the `BookingMenu` junction and recipe costing do not exist.
- **Dish Costing Coverage:** The proportion of the menu catalogue that carries a food cost greater
  than zero. A **data-completeness** measure, not a profitability measure.
- **Bookings Awaiting Deposit:** Non-cancelled bookings with no payment recorded, reported with
  their full outstanding balance. Carries **no assumed deposit percentage** while DEC-009 is open.


## 5. Customer Master & Accounts Receivable Terms (added by REQ-CUS-01, 2026-10-19)

- **Customer Master:** The management screen and its API surface for the client directory — the registered customer population, each customer's profile, and each customer's position in the Accounts Receivable ledger.
- **Statement of Account (SOA):** The itemised statement of a single customer's contracted bookings, the payments received against them, and the balance outstanding. Rendered on screen and downloadable as a PDF (QuestPDF). REQ-REC-01.
- **Total Contracted Value:** The sum of the Net Amount Due across a set of non-cancelled bookings — what was billed, as opposed to what remains unpaid. KPI-21.
- **Outstanding Balance (customer):** The customer's Accounts Receivable position: $$\S_{b.\text{Status} \ne \text{Cancelled}} \max(0, b.\text{NetAmountDue} - \textstyle\sum p.\text{AmountPaid})$$ BR-08 / DEC-025 / KPI-12.
- **Customer Aging State:** A customer's Accounts Receivable position expressed on the **KPI-13 aging scale** — the worst (most overdue) aging bucket among the bookings contributing a positive balance to that customer, or `Clean` when the customer owes nothing. DEC-052. The state is computed and labelled by the server; the browser never derives a day count.
- **Not yet due / Overdue:** An Accounts Receivable balance on a booking whose **event date** has not yet passed / passed more than 30 days ago. The event date is the due date (DEC-041).
- **Customer Classification (derived):** The presentation of the existing `Customer.Company` attribute as `Corporate` (company name **non-blank**) or `Private` (blank). "Blank" is whitespace-aware, so a company of spaces is `Private`. It is **never persisted** and is not a stored attribute of the customer. DEC-051.

## 6. Menu, Costing & Recipe Terms (added by REQ-MNU-01/02, 2026-10-20)

- **Dish (Menu Item):** One sellable dish in the menu catalogue, identified by a short code (`menuId`, e.g. `DS-104`) and belonging to a **course** (Appetizer, Soup, Salad, Main Course, Dessert, Beverage). A dish carries a **selling price** and a **food cost**.
- **Course (Course Category):** The position a dish occupies in a banquet progression. A dish belongs to at most one course; a dish with no course is grouped under **"Uncategorised"** for reporting. **In practice a course is required** — `CreateMenuRequestDto.CourseId` is a non-nullable `int` and the validator rejects a missing (`0`) or unknown course id with `400` — so the "Uncategorised" group is reachable only from legacy rows, and every dish created through the API has a course.
- **Food Cost:** The cost of producing one portion of a dish, **entered by the kitchen** into the catalogue. It is a stored decimal, not a computed ingredient cost — no ingredient, yield or supplier model exists (DEC-062).
- **Margin Population:** The set of dishes that carry **both** a selling price greater than zero **and** a food cost greater than zero. Every costing percentage is computed over this population, so an unpriced or uncosted dish never distorts an average (KPI-15, KPI-22, KPI-23).
- **Costed / Needs Costing:** A dish is **Costed** when its food cost is known and greater than zero; it **needs costing** when the food cost is null or zero (KPI-14).
- **Food Cost Percentage:** $$\text{FoodCost\%} = \frac{\text{FoodCost}}{\text{SellingPrice}} \times 100$$ — the share of the selling price consumed by the food cost.
- **Gross Margin Percentage:** $$\text{GrossMargin\%} = \frac{\text{SellingPrice} - \text{FoodCost}}{\text{SellingPrice}} \times 100$$
- **Margin Alert:** A dish whose food cost percentage exceeds the configured food-cost benchmark. It is a **re-costing prompt**, not a penalty (KPI-24).
- **Food Cost Benchmark:** The deployment-configured ceiling for the catalogue's food cost percentage (`Dashboard:Benchmarks:TargetFoodCostPercentage`, default 35 % per BR-07). It is configuration, not a constant, and it is read from one place so the dashboard and the menu screen cannot disagree (DEC-040).
- **Recipe:** In this release a **recipe is the dish record itself** — its identity, course, price, food cost, note and image. A *recipe in the kitchen sense* — a bill of ingredients, yields, trimming and shrinkage losses, portion weights, allergens and plating instructions — **does not exist in the model** and is tracked as the blocked capability REQ-MNU-03 (DEC-059).

## 7. Package Configuration Workbench Terms (added by REQ-PKG-02, 2026-10-22; course-rule term amended by DEC-172 on 2026-10-27)

- **Course Rule (Package Course Rule):** The enforced rule a buffet tier holds for one course category - how many dishes the client must take from it (`requiredQuota`), the standard portion per guest in grams, and the rule in plain words. It carries no inclusion label or add-on association (DEC-172).
- **Required Quota:** The number of dishes a client must select from a course. A course with a quota of one is satisfied by **exactly one** dish. DEC-083's sibling rule; the compliance verdict compares the selected count with the quota **exactly**.
- **Qualified Pool:** The dishes a client may choose from for a course - every non-soft-deleted `Menu` whose `CourseId` matches the course the rule governs. A pool is **not** a commitment: it is the menu of everything the kitchen has prepared for that course.
- **Costed Dish:** A dish whose food cost is known - a food cost **greater than zero**. A dish with no food cost is in the pool but contributes to no average and to no cost figure. The test is "greater than zero", not "not null", because `Menu.FoodCost` stores **zero** rather than null when no cost was entered (DEC-087, DEC-062).
- **Standard Run:** The planning estimate of a tier's food cost **before** a client has chosen a dish in each course: the mean food cost of each course rule's qualified pool, summed. A **planning estimate, not a commitment**. DEC-083.
- **Cost Ceiling (per pax):** The highest food cost a tier can carry at its own per-pax rate: `AmountPax x TargetFoodCostPercentage / 100`, using the configured food-cost benchmark (DEC-040, BR-07). It is a **warning threshold**, not a block and not a price.
- **Ceiling Breach:** A selection whose food cost exceeds the tier's cost ceiling. The system **warns** and reports the shortfall; it never reprices the selection and never refuses it. DEC-080.
- **Stress Preview:** The effect of a hypothetical increase on the selected dishes' food cost - a percentage the operator enters, applied to the selection, reported as a stressed cost and a stressed percentage. It changes nothing that is stored.
- **Rule Compliance:** The verdict on a proposed selection, rule by rule: the selected count against the required quota, plus the package-level verdict. A selection naming a course the tier holds **no** rule for is **not** compliant.
- **Municipality (Area):** A named out-of-town destination the operation can serve - a town or zone with a per-pax out-of-town surcharge, an optional distance from the commissary, an optional dispatch note and an optional per-event logistics cost. The `Area` entity; DEC-077.
- **Out-of-Town Surcharge:** The **per-pax** amount added to a tier's base rate when the event is served in a covered municipality. It is **one rate per municipality**, not one per package. DEC-078.
- **Coverage (Package Area Coverage):** The link between a tier and a municipality, with an active flag saying whether the tier is currently offered there. A tier with no coverage can only be served in its home zone.
- **Logistics Cost:** The **per-event** cost of serving a municipality - fuel, thermal-retention equipment, generator rental and provincial labour. It is a flat cost per event, not per guest, and a municipality with none simply has no logistics band. DEC-081.
- **Effective Rate per Pax:** The tier's base per-pax rate plus the municipality's out-of-town surcharge - the rate actually quoted for that destination.
- **Total Contract Value:** The base contract plus the extended (surcharge) total for a given headcount. A **what-if** figure produced by the simulator; it is not a booking total and it is not a receivable.
- **Minimum Contract Value:** The smallest contract a tier can carry: `AmountPax x MinPax`. A **derived floor**, not a charge, and it never enters a booking total. DEC-079.
- **Gross Profit (tier, simulated):** The contract value less the food cost and the logistics cost for one simulated event. A planning figure, never an accounting entry.
- **Capability Gap Notice:** The visible, labelled statement that a panel the design reference shows has **no approved data source**, rendered in place of that panel. It names the missing concept and never substitutes a placeholder table, a fabricated figure or a disabled form. DEC-059, DEC-067, DEC-084.
