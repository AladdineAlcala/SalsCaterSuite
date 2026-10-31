| **Allergen chips** on a dish | The model carries **no** allergen field; allergen tagging is part of the blocked recipe engine | DEC-150, REQ-MNU-03 |
| **The quality stamp**, the **chef sign-off** and the **tasting sheet** | No compliance record, no approval state and no tasting-sheet document exist | DEC-150 |
| **The course groupings** ("Proteins", "Starters & Carbs", "Dessert & Drinks") | No category-group taxonomy exists; the tabs are derived from the tier's own rules | DEC-147 |
| The **ingredient-level** batch yield and its unit counts ("4 Full Pans") | Only the **batch mass** is derivable from the rule's portion and the headcount; the recipe is not modelled | DEC-145, REQ-MNU-03 |

# Sal's CaterSuite — Bookings & Events Operations: Dish Selection, Add-Ons & Automatic Discounts — Feature Specification

| Field | Value |
| :--- | :--- |
| **Specification** | `business/bookings-events-operations-feature-spec.md` v1.3 (DEC-170 UI amendment; AC-BKG-89..98 / DEC-171 workbench enhancement; DEC-176 applicable-pax display, 2026-10-31) |
| **Requirements delivered** | **REQ-BKG-06** (the per-booking dish selection and its change window) · **REQ-BKG-07** (the add-on master and the booking add-ons) · **REQ-BKG-08** (the headcount-band automatic discounts). Together they close the last open halves of **REQ-BKG-01** ("Addons/Discounts unlinked") and **REQ-BKG-02** ("dish selections"). |
| **Cycle** | 2026-10-22 — Bookings & Events Operations; 2026-10-24 — Booking Menu Add-on Picker UI |
| **Owner** | AGENT 01 — Business Intelligence Architect |
| **Primary sources** | The owner's answers of 2026-10-22, `Bookings and Events Operation.txt`, and the owner's UI requests in `docs/userupdate.txt` (2026-10-24) and `docs/userchangerequest.txt` (2026-10-31) |
| **Contract section** | `architecture/approved-api-contracts.md` §14 (register and quote, already delivered) · **§15** (the dish selection) · **§16** (the add-ons) |
| **Status** | Existing booking operations, DEC-171 workbench enhancement, and DEC-176 applicable-pax display are delivered. No general booking edit screen is in scope; remaining unresolved owner decisions are explicitly listed in §9 |

---

## 1. Objective and Scope

### 1.1 Objective

Complete the **Bookings & Events operation** so that the two delivered screens behave the way the business actually works:

1. **The dish selection** — a booking carries the dishes chosen for **that event**, drawn from the tier's approved pools, changeable until a configured number of days before the event.
2. **The add-ons** — beyond the tier, a booking may carry add-ons drawn from an **add-on master** with categories, amounts and units of measure, with the **description and the amount frozen** on the booking.
3. **The automatic headcount-band discounts** — a booking of 80–89 heads earns ₱10.00 per head and a booking of 100–999 heads earns ₱20.00 per head.
4. **The extended-location rule** — the out-of-town amount applies only to the `Regular` and `Premier` tiers, and the area filter appears only for those tiers.
5. **The post-create redirect** — once the booking is created, the operator is taken straight to the menu-selection screen.

### 1.2 The owner's answers (2026-10-22)

These are the governing commercial inputs. They are not re-litigated.

| # | Question | The owner's answer | Consequence |
| :--- | :--- | :--- | :--- |
| **1** | Can a confirmed booking's menu change after the deposit is taken? | **Yes** — the selection is a per-booking override chosen from the tier's pools, changeable until **N days before the event**, where **N comes from `appsettings`**. | The selection is editable **after** confirmation and **after** the deposit. A **menu-change window** exists and its length is **configuration**. |
| **2** | Does changing the dishes move the booking's money? | **No.** | The tier's per-pax rate is frozen (BR-04) and **the selection never touches a money column**. The **margin** moves with the selection while the **price** does not — the business's risk to carry (RSK-062). |
| **3** | Is the selection recorded per booking, or does the BEO just carry it? | **Yes** — per booking. | A **new junction entity** records the selection on the booking. |
| **4** | *From the operational note* — the **headcount-band discounts**: "Booked number of heads >=80 and <90: discount amount 10.00 … number of heads = 89 calculation is 89 x 10 = 890.00 discount"; "Booked number of heads >=100 and <1000: discount amount 20.00 … number of heads = 100 calculation is 100 x 20 = 2000.00 discount". | **Given.** | An **automatic** discount of a **fixed amount per head**, by headcount band. §3.4. |
| **5** | *From the operational note* — the **add-on master**: "Addon Masterlist … Table AddonCategory(CatId, AddOn Category) … Table Addons Details(Id, AddonCategoryId, Addon Description, Amount, Unit of measure(UOM))"; "BookingAddOns Table(AddonId, Description, Amount, BookingId)". | **Given.** | A new **category** entity, a **category** on the add-on, and a booking junction that **freezes the description and the amount**. §3.5. |
| **6** | *From the operational note* — "Package Extended Location Amount — Only applicable for PackageType Regular and Premier", and the area filter appears only for those types. | **Given.** | The out-of-town amount is **available only** for `Regular` and `Premier`; the area filter is **offered** only for those. §3.6. |
| **7** | *From the operational note* — "After the booking has been created it will redirect into another screen for menus selection". | **Given.** | The create flow ends on the **menu-selection screen**. §3.7. |

### 1.3 The rulings AGENT 01 derived from the operational note

Each is recorded as a decision; each names the ambiguity it resolves.

| # | The operational note says | Ruling | Decision |
| :--- | :--- | :--- | :--- |
| 1 | ">=80 and <90" and ">=100 and <1000" | The bands are read **literally and half-open**: `[80, 90)` earns **₱10.00 per head**; `[100, 1000)` earns **₱20.00 per head**; **every other headcount earns nothing** — including **90–99** and **1000 and above**. The 90s are a genuine **gap**, not an omission to be silently filled; a booking in it is a **negotiated** discount, which is what an explicit discount code is for. | **DEC-136** |
| 2 | "89 x 10 = 890.00 discount" | The band discount is a **fixed amount** (heads × the band's per-head rate), applied against the **gross** and **capped at the gross** (BR-02's ceiling). It is computed by the server at creation and **frozen**. | **DEC-137** |
| 3 | The note states the discount bands but not their interaction with a discount code | **One discount applies.** The **automatic band discount** is the booking's discount when the band qualifies and **no** explicit code was given. An **explicit discount code replaces it** — the operator's negotiated code is the more specific instruction. Both are BR-02 fixed amounts and both are capped at the gross. | **DEC-138** |
| 4 | "BookingAddOns Table(AddonId, Description, Amount, BookingId)" | The junction **freezes the description and the amount** (BR-04). The catalogue's current price never moves a stored line. The add-on's **unit of measure** is frozen with it. | **DEC-139** |
| 5 | The note gives the add-on master but not its quantity behaviour | The add-on line carries an **amount** the operator confirms, defaulted to the catalogue's unit amount, and the **quantity** is part of that amount. A quantity column is **not** stored separately, because the junction the owner specified has none — the amount on the line is the commercial truth. | **DEC-140** |
| 6 | "Only applicable for PackageType Regular and Premier" | The out-of-town amount is **available only** for the `Regular` and `Premier` tiers. For any other tier the area filter **does not appear** in the workbench, and a surcharge applied to another tier is a **`400`** — the rule is enforced **server-side**, not merely hidden. | **DEC-141** |
| 7 | "Premium" in one line, "Premier" in another | The type is **`Premier`** — the `PackageType` enum's member. "Premium" in the operational note is read as the same tier. | **DEC-142** |
| 7 | "Premium" in one line, "Premier" in another | The type is **`Premier`** - the `PackageType` enum's member. "Premium" in the operational note is read as the same tier. | **DEC-142** |

### 1.3a The rulings derived from the *menu-selection* design

The `Booking_Menu_Selection` reference (`code.html`, `screen.png`) is the design for the menu editor. Read against the model, it settles three things and adds four.

| # | The design shows | Ruling | Decision |
| :--- | :--- | :--- | :--- |
| 8 | A **Contract Ledger** whose lines are *Base Package* + *Selected Add-ons* + *Out-of-Town Fee*, totalling **PHP 120,300.00**, with the add-ons line at **+PHP 15,900.00** | **The add-ons join the contract value** - the design's own arithmetic proves it (98,400 + 15,900 + 6,000 = 120,300). This confirms the reading of BR-01's `AddonsTotal` term and DEC-135. | DEC-135 (confirmed) |
| 9 | A ledger line **"Standard 12% EVAT Included" PHP 12,889.29**, and a total of PHP 120,300.00 | **The tax is VAT-inclusive and it is a disclosure, not an addition.** 120,300 / 1.12 = 107,410.71, and 120,300 - 107,410.71 = **12,889.29** - the design's arithmetic proves it. The **rate** is a tax policy (DEC-008, UNRESOLVED) and is therefore **configuration** (`Bookings:Tax:VatPercentage`), **absent by default**; with the key absent the line is **absent**, never "0.00". | **DEC-143** |
| 10 | Add-ons priced **"+150/pax" -> "18,000.00"** for 120 guests, beside one priced **"1,500.00 - Flat rate"** | The add-on carries an **explicit pricing basis** (`Flat` or `PerGuest`) on the master, and the **unit of measure** stays a **descriptive** unit. The design proves the *distinction* - `+150/pax` against 120 guests gives `18,000.00`, beside a `1,500.00 - Flat rate` line - but a **string test on money** against a free-text unit column no data pins is not safe, so the basis is a column (DEC-158, superseding DEC-144's test). The booking's line **freezes the line total**. | **DEC-158** |
| 11 | A per-dish **"Chef Batching Prep Yield"** of **21.6 kg** for 120 guests at **180g / pax** | **The batch mass is derivable** from what the model already holds: `PortionGrams x NumberOfHeads / 1000` = 21.6 kg. It is a **derived planning figure**, published per rule, and `null` when the rule states no portion. The **ingredient-level** yield (and the design's unit counts such as "4 Full Pans") stays a **capability gap** - that is the recipe engine (REQ-MNU-03). | **DEC-145** |
| 12 | A free-text **"chef prep instruction"** per dish ("Walnuts on side for allergy table") | The instruction is **per booking and per dish**, so it is stored on the **selection row**, not on the catalogue. It is free text and it is **frozen** with the selection. | **DEC-146** |
| 13 | The quick tabs **"All 10 Courses / Proteins (Beef, Pork, Chicken, Seafood) / Starters & Carbs / Dessert & Drinks"** | The **groupings** are not modelled - `CourseCategory.CourseName` is free text and there is no category-group taxonomy - so they are **not rendered**. The tabs are **derived from the tier's own rules**: one tab per course rule, plus "All". The reference's groupings would need a new entity and an owner ruling, so they are a **capability gap**, not a hardcoded taxonomy. | **DEC-147** |
| 14 | A **"Save & Lock Selection"** button, beside a **"BEO Cutoff: Nov 21, 2026"** badge, for an event on **Nov 28, 2026** | The **cutoff badge is the menu-change window** - and the design's own dates say **7 days** (28 - 21 = 7), which confirms the shape of the owner's "N days". The button **saves**; the **lock** is automatic at the cutoff. Whether a **manual** lock also exists is an open question (section 9). | **DEC-149** |
| 15 | **Allergen chips** ("Contains Tree Nuts", "Dairy - Gluten-Free"), a **HACCP quality stamp**, a **chef sign-off** card and a **Tasting Sheet (PDF)** | All four are **capability gaps**: the model carries **no** allergen field (allergen tagging is part of the blocked recipe engine, REQ-MNU-03), no compliance or quality record, no approval state, and no tasting-sheet document. They render as explicit `CapabilityGapNotice`s. | **DEC-150** |
| 16 | A **"Generate Kitchen Production Order (PDF)"** and a **"View BEO"** action | The **BEO document already exists** (`KitchenBeoDocument`), and the **selection is what it prints** - the "menu items" REQ-REP-02 has always asked for. This cycle therefore closes REQ-REP-02's missing controller with `GET /api/bookings/{id}/beo`. The **tasting sheet** has no document and stays a gap. | **DEC-148** |

### 1.3b The corrections the plan review forced

The plan review of 2026-10-22 returned twelve blocking findings. **All twelve are applied**, and they are applied **in place** - the sections below already carry them. This section exists so a reader can see what changed and why, without reconstructing it from the decision log.

| # | What was wrong | What the specification now says |
| :--- | :--- | :--- |
| **F1** | The add-on total had no path into the authoritative calculator - the shared overload hardcoded `addonsAmount: 0m` and `Booking` stored no add-on total, so `Booking.AmountDue` and the calculator's `NetAmountDue` would have diverged by exactly the add-on total the moment an add-on existed. | `Booking.AddonsTotal` is a **frozen column** and the calculator **reads it** (DEC-151). The overload's argument set is unchanged - the parameter was always there; it simply had nothing to read. |
| **F2** | An add-on write re-prices a confirmed, deposited booking, and nothing said how the money reacted. | The whole reaction is specified in **§3.5** and **BR-43** (DEC-152): re-freeze, re-cap the discount, leave the deposit, refuse an overpay. |
| **F3** | The quote accepted neither the selection nor the add-ons, so the quote and the create would have diverged - a direct BR-31 breach. | The quote accepts both and prices them through the **same path** (DEC-153). |
| **F4** | KPI-42 was written over a `DiscountSource` that does not exist. | The source is **derived** by an exact rule and **published** (BR-44, DEC-154). |
| **F5** | The prep instruction had no field in the request, and a replace that omitted it would have wiped the chef's note. | The selection element carries `prepInstruction?`; an **omitted** member **clears** it, because the selection is a full replace (DEC-155). |
| **F6** | The surcharge tier test was undefined against a **free-text** type column. | The test is **pinned**: trimmed, case-insensitive **equality** with `Regular` and `Premier`, on the **type column only** (§3.6, DEC-156). |
| **F7** | The "filter by Package Area" had **no server support**. | `GET /api/packages?areaId=` returns the tiers covering that area (§3.6, DEC-157). |
| **F8** | The pricing basis was a **case-sensitive string test on money** against an unpinned free-text column. | The basis is an **explicit column**; the unit stays descriptive; a not-stated basis is `Flat` (§3.2, DEC-158). |
| **F9** | The request's `amount` semantics contradicted the multiplication, and BR-01's formula spoke of a quantity the plan had removed. | The request carries the **line total**; BR-01 §1.1 now says the same thing (§3.5, DEC-158). |
| **F10** | The BEO promised to print the tier's "standard run" as a dish list - but the standard run is a **cost figure**, not a dish set. | The claim is **dropped**. A selection-less BEO prints the **course rules** and says no dish selection is recorded (DEC-159). |
| **F11** | The client would have had to hardcode the band bounds and the VAT rate - and the feature's own `noHardcodedFigures` test would have failed on delivery. | The server publishes `discountBandLabel` and `vatPercentage` (DEC-160). |
| **F12** | The contract was never actually extended, and three figures sat at the wrong level. | Contract **§14.1-§14.4** carry the extensions; **§15.1** is re-shaped - `batchMassKg` on each course, `prepInstruction` on each selection, `includedVatAmount` at the top level. |

**The clarifications are applied too:** M1 (one shared BR-15 validator; §1.4's wording corrected) · M2 (exactly one selection state - `None` | `Selected` | `Locked`) · M3 (`batchMassKg` rounds to two decimals away from the midpoint, and a rule whose portion is **0 g** publishes `null`, never `0.00`) · M4 (`menuId` aligned to **50**; delete behaviours pinned - **Cascade** for `Booking`, **Restrict** for `Menu`, `CourseCategory` and `AddonDetail`) · M5 (the three new tables and the `AddonCategory` master are in `business/data-dictionary.md`) · M6 (`IBookingPolicyProvider` gains `MenuSelectionLockDaysBeforeEvent` and `VatPercentage`, both `null` by default) · M7 (the workbench gains a **package-type filter**; the area dropdown follows the **selected type**; and the existing **municipality** selector is hidden for a tier that cannot carry the out-of-town amount - otherwise the delivered workbench would break with a `400` on a Wedding tier) · M8 (the register population loads the two new navigations, and DEC-132's conditional estimate is computed **per booking**) · M9 (the replace path is **last write wins**; `Booking.RowVersion` guards the booking) · M10 (the frontend test plan names the right file) · M11 (KPI-40's margin basis is the booking's **frozen** `PricePerHead`) · M12 (the add-on master ships **empty** and the feature is **inert** until it is populated) · M13 (the governance record corrected).

**None of the findings called the commercial design into question.** Every one of them was a defect in **how the plan was written**, not in what it decided - and the two that would have cost real money (F1's missing path and F8's string test) are exactly what a live review exists to find.

### 1.4 What the model carries today, and what is genuinely new

| Piece | State | Verdict |
| :--- | :--- | :--- |
| The tier's **course-coverage ruleset** — one rule per course, with `RequiredQuota`, the portion and the rule text | **Exists** — `PackageCourseRule` (REQ-PKG-02, DEC-077; amended by DEC-172) | **Reused unchanged for selection/quota behavior.** It carries no inclusion tag or upgrade add-on. |
| The **qualified pool** of a course — the live, non-soft-deleted dishes of that course | **Exists** — derived on every read, never stored (BR-19, DEC-083) | **Reused unchanged.** |
| The **quota compliance** engine — "exactly `requiredQuota` dishes per course, and no dish from a course the tier has no rule for" | **Exists** — BR-15, implemented and tested in the packages workbench | **Reused unchanged.** |
| The selection's **food cost** and **gross profit** | **Exists** — the packages' selection preview (contract §13.3) | **Reused unchanged.** |
| The frontend **per-course dish picker** | **Exists** — `PackageSelectionSimulator` (REQ-PKG-02) | The booking editor follows the **same pattern** with its own component (its data source is a *saved* selection with a lock state, not a live preview). |
| The `AddonDetail` table — description, unit price, unit | **Exists** — but with **no category** | **Extended** with an `AddonCategoryId` (§3.5). |
| The **booking's own dish selection** | **Does not exist** — no `Booking`↔`Menu` link (RSK-049, DEC-105) | **NEW** — the junction entity (§3.3). |
| The **add-on category** master | **Does not exist** | **NEW** — one entity (§3.5). |
| The **booking's add-ons** | **Does not exist** — `AddonDetail` has no link to `Booking` (RSK-048) | **NEW** — the junction entity (§3.5). |
| The **headcount-band automatic discount** | **Does not exist** — the `Discount` catalogue exists but is empty (BR-13) | **NEW** — the rule (§3.4). |
| **The menu-change window** | **Does not exist** | **NEW** — the lock rule and its configuration (§3.8). |
| **A place to change an existing booking** | **Does not exist** — the booking **edit** screen was excluded from the previous cycle | **NEW** — the menu editor route (§5.3). |

### 1.5 Exclusions (explicit)

| Excluded | Why | Tracked as |
| :--- | :--- | :--- |
| **The general booking edit** — venue, occasion, dates, **pax**, **tier** | Changing the **pax** or the **tier** **reprices** the booking. The owner's "no" covers **dishes**; it cannot be stretched to quantities. | §9, question 1 |
| **A dish-level costing engine** (ingredients, yields, batch amortisation) | Blocked on ten owner decisions. | REQ-MNU-03 |
| **An authoritative per-event margin** | The dish food costs are **manually entered decimals** (DEC-062) and the recipe engine is unbuilt. A selection-based margin is a **planning estimate**. | RSK-025, RSK-034 |
| **The BEO PDF endpoint** | No BEO entity and no BEO endpoint (DEC-104). This cycle gives the selection somewhere to live; publishing it on a BEO is the next step. | REQ-REP-02 |
| **A dish or add-on approval workflow** | No approval state exists in the model. | — |
| **The banquet calendar, the receivables screen** | Separate requirements. | REQ-CAL-01, REQ-REC-01 |

---

## 2. Actors and Permissions

| Capability | Endpoint | Permitted roles | Basis |
| :--- | :--- | :--- | :--- |
| **Read** the register, the summary, the quote | `GET /api/bookings`, `/summary`, `POST /quote` | any authenticated user | BR-12 |
| **Read / replace** a booking's dish selection | `GET`/`PUT /api/bookings/{id}/menus` | any authenticated user | BR-12 |
| **Read / replace** a booking's add-ons | `GET`/`PUT /api/bookings/{id}/addons` | any authenticated user | BR-12 |
| **Read** the add-on master | `GET /api/addons` | any authenticated user | BR-12 |
| **Create** a booking (with its selection and its add-ons) | `POST /api/bookings` | any authenticated user | BR-12 |
| Cancel a booking / issue a refund | `POST /api/bookings/{id}/cancel` | Administrator, Manager | BR-12, `Bookings.Cancel` |

**The lock window and the tier rule, not a role, are the guards.**

---

## 3. Domain and Data

### 3.1 The money chain, restated for this cycle

Unchanged from the delivered cycle (BR-01..BR-04, BR-25, BR-37), with **one** addition and **one** restriction:

```
BaseAmount        = PricePerHead x NumberOfHeads
ExtendedAreaAmount= ApplyExtendedAmount ? Round2(Area.AdditionalRate x NumberOfHeads) : 0
AddonsTotal       = Booking.AddonsTotal - the frozen sum of the add-on line totals
GrossAmount       = Round2(BaseAmount + ExtendedAreaAmount + AddonsTotal)
DiscountAmount    = Round2( min( the applicable discount , GrossAmount) )
NetAmountDue      = Round2( max(0, GrossAmount - DiscountAmount) )
RemainingBalance  = Round2( max(0, NetAmountDue - Collected) )
IncludedVatAmount = VatPercentage is not null ? Round2( NetAmountDue - NetAmountDue / (1 + VatPercentage/100) ) : null
```

**An add-on is a charge; a dish is a substitution.** The owner's "no repricing" answer (ruling 2) was given about **changing the menu** - swapping one dish for another inside the tier the client already bought, where the price must not move. An **add-on** is not a substitution: the client buys it, it is money, and the owner's junction carries an `Amount` for exactly that reason.

**BR-01 already says so.** The gross formula has been `BasePackage + ExtendedAreaFee + AddonsTotal + OtherCharges` since the beginning, and `BookingAmountDueCalculator` has carried an `addonsTotal` parameter ever since - it has been `0m` only because **nothing could create one** (RSK-002). This cycle makes BR-01's third term reachable for the first time. **The add-on total therefore joins the gross** (DEC-135).

The **frozen** amount on the line is what is added, so a later catalogue price change never moves a stored booking (BR-04) - and the booking's total moves **only** when the operator adds or removes an add-on, exactly as the gross formula intends. The sum of those frozen line totals is **stored on the booking** as `Booking.AddonsTotal` (DEC-151), and the authoritative calculator **reads that column** rather than a zero placeholder - which is what keeps `Booking.AmountDue` equal to the calculator's `NetAmountDue` to the cent (DEC-099).

**The tax is a VAT-inclusive disclosure, and the rate is configuration** (DEC-143). The menu-selection design's ledger prints **"Standard 12% EVAT Included"** against a total of 120,300.00, and its arithmetic is conclusive: 120,300 / 1.12 = 107,410.71, and 120,300 - 107,410.71 = **12,889.29** - the figure the design prints. So the price the client pays is **VAT-inclusive**, and the VAT is a **memo line inside** the total, never an addition to it.

**The rate itself is still a tax policy (DEC-008, UNRESOLVED), so it is configuration** (`Bookings:Tax:VatPercentage`), **absent by default** - exactly like the deposit percentage (DEC-101) and the capacity (DEC-102). With the key absent, `IncludedVatAmount` is **`null`** and the ledger line is **absent**, never "0.00". With the key set to `12`, the line reads the configured rate - never a hardcoded 12.

The tax term does **not** change `NetAmountDue`: the amount due is the gross less the discount, and the VAT is disclosed within it. That keeps BR-25's single net-due path intact.

### 3.2 The add-on master (REQ-BKG-07)

Two entities, both small.

**`AddonCategory`** — `addonCategoryId` (identity PK), `addonCategoryName` (nvarchar(150), required), the audit columns.

**`AddonDetail`** - **extended** with two columns: `addonCategoryId` (`int?`, FK -> `AddonCategory`, **restrict** delete, **nullable** so the existing catalogue rows and the legacy import are not invalidated) and `pricingBasis` (`int?`, `0` = `Flat`, `1` = `PerGuest`, **`null` = not stated**). The existing `Description`, `UnitPrice` and `Unit` are untouched; `Unit` **is** the unit of measure the note names.

- The category is **nullable** because an add-on may pre-date the category master. `null` is **unknown**, never a category named "General".
- The master was read-only in the original cycle: the owner specified the **master's shape**, not an administration screen. **AMENDED 2026-10-24:** the owner requested `AddonDetail` CRUD at Settings → Addons, and the administration surface is now delivered (DEC-168, contract §16.2a). The master still ships **empty** (BR-13), so the picker is empty until the owner populates it from Settings → Addons or the legacy import (RSK-071; RSK-066's administration blocker was closed by DEC-168). Categories and departments remain read-only lookups (DEC-169).

- **The pricing basis is an EXPLICIT column, and the unit of measure is only descriptive** (DEC-158, superseding DEC-144's test). The design proves the *distinction* - `+150/pax` against 120 guests gives `18,000.00`, beside a `1,500.00 - Flat rate` line - but it does **not** license a **string test on money**: the unit column is free text that no data pins (the master ships empty and the legacy import has not started), and `pax`, `Pax`, `per pax` and `null` are all plausible. A silent mismatch would charge `150` where `18,000` was meant. The basis is therefore a column the catalogue states, and the unit stays a **descriptive** unit of measure.
- A **not-stated** basis is treated as **`Flat`** - the safe direction, because it never multiplies a price that was not meant to be multiplied.

### 3.3 The booking's dish selection (REQ-BKG-06, DEC-127)

One new table. No existing table's columns change.

**`BookingMenu`** — the dishes chosen for one booking, one row per chosen dish.

| Column | Type | Null? | Meaning |
| :--- | :--- | :--- | :--- |
| `bookingMenuId` | `int` identity | no | PK |
| `trn_Id` | `int` | no | FK → `Booking`. **Cascade** delete — the selection dies with its booking. |
| `menuId` | `nvarchar(100)` | no | FK → `Menu` (the catalogue's key is a **string**). |
| `courseId` | `int` | no | FK → `CourseCategory`, **frozen** at selection time, so the selection stays self-describing if the catalogue later re-assigns a dish (RSK-063). |
| `prepInstruction` | `nvarchar(500)` | yes | The chef's **per-event** prep instruction for this dish ("Walnuts on side for allergy table"). It is **per booking**, so it lives on the selection row and not on the catalogue (DEC-146). `null` = not stated. |
| audit columns | | | DEC-038. |

**Indexes.** A **filtered unique index** on (`trn_Id`, `menuId`) with the predicate `is_deleted = 0` (the DEC-088 precedent), and an index on (`trn_Id`).

**Deliberately not stored:** a quantity (the quota is per course, so the count of rows in that course *is* the quantity), a frozen dish name/price/food cost (the dish is a **catalogue reference**; the catalogue owns those), and any amount (the selection never carries money).

**The selection is validated by the tier's own rules (DEC-130).** BR-15, unchanged: for **every** course rule of the tier, the number of chosen dishes in that course is **exactly** `RequiredQuota`; **no** chosen dish belongs to a course the tier has **no rule** for; every chosen dish is a **live** member of that course's qualified pool. The ruleset is read **live** at the moment of the write. A breach is a **`400`** naming the course and the quota.

**The tier's ruleset is the ceiling.** The menu is personalised; the **standard** is not.

### 3.4 The headcount-band automatic discounts (REQ-BKG-08, DEC-136, DEC-137, DEC-138)

| Band | Heads | The discount |
| :--- | :--- | :--- |
| — | `< 80` | none |
| — | `80 … 89` | **₱10.00 per head** — a fixed discount of `NumberOfHeads x 10` |
| — | `90 … 99` | **none** (the negotiated band — use a discount code) |
| — | `100 … 999` | **₱20.00 per head** — a fixed discount of `NumberOfHeads x 20` |
| — | `>= 1000` | **none** |

- The bands are **half-open** and are evaluated on the **booked** headcount (`Booking.NumberOfHeads`), never on a forecast.
- The discount is a **fixed amount** against the **gross** and is **capped at the gross** (BR-02). The owner's example — `89 x 10 = 890.00` — is the arithmetic.
- It is computed by the **server** at creation, **frozen** on the booking, and published on the quote.
- **One discount applies.** The automatic band discount is the booking's discount when the band qualifies and **no** explicit code was given; an **explicit discount code replaces it** (DEC-138).
- The **catalogue** `Discount` rows are unaffected — the band discount is **not** a catalogue row, it is a rule.

### 3.5 The booking's add-ons (REQ-BKG-07, DEC-139, DEC-140, DEC-158)

One new table. **One existing table gains a column** - `Booking.AddonsTotal` (DEC-151).

**`BookingAddOn`** - the add-ons carried by one booking, one row per add-on line.

| Column | Type | Null? | Meaning |
| :--- | :--- | :--- | :--- |
| `bookingAddOnId` | `int` identity | no | PK |
| `trn_Id` | `int` | no | FK -> `Booking`. **Cascade** delete. |
| `addonId` | `int` | no | FK -> `AddonDetail`. **Restrict** delete (M4). |
| `description` | `nvarchar(150)` | no | The **frozen** description (BR-04). |
| `amount` | `decimal(18,2)` | no | The **frozen line total** - the line's commercial truth and the amount that enters the gross (DEC-158). |
| `unit` | `nvarchar(50)` | yes | The **frozen** unit of measure - **descriptive only**. `null` = not stated. |
| `pricingBasis` | `int` | no | The **frozen** pricing basis: `0` = `Flat`, `1` = `PerGuest` (DEC-158). |
| audit columns | | | DEC-038. |

**`Booking.AddonsTotal`** - `decimal(18,2) NOT NULL DEFAULT 0` - the **frozen** sum of the line totals (DEC-151). It is the value the authoritative calculator reads, so `Booking.AmountDue` and the calculator's `NetAmountDue` stay equal to the cent (DEC-099). It replaces the `0m` placeholder that has stood in the calculator since the beginning.

**Indexes.** A **filtered unique index** on (`trn_Id`, `addonId`) with the predicate `is_deleted = 0`, and an index on (`trn_Id`).

**The line total is frozen on the line.** Editing the catalogue's `UnitPrice` afterwards never moves a stored line (BR-04). The **quantity** is not a separate column - the line total is the commercial truth, and for a `PerGuest` add-on the headcount **is** the quantity and it is expressed **within** the line (DEC-158).

**The request carries the line total, not a quantity and not a basis.** `{ addonId, amount? }`. When `amount` is **absent**, the server derives the default from the catalogue's amount and the basis - `PerGuest` gives `catalogueAmount x NumberOfHeads`, `Flat` gives `catalogueAmount`. When `amount` is **present**, it **is** the line total and the server does not multiply it again. The operator states the commercial truth of the line; the catalogue owns the basis.

**The replacement semantics (DEC-134).** A `PUT` **replaces** the whole set: the lines no longer carried are **soft-deleted**, never destroyed, and re-adding an add-on **resurrects** its row rather than inserting a second one (the DEC-085 pattern).

**The money reaction (DEC-152, BR-43).** An add-on write **re-prices** the booking, and the whole reaction is specified:

1. the add-on total is recomputed and the snapshot is **re-frozen** - `Subtotal`, `DiscountAmount` and `AmountDue` are written again (BR-04 already anticipates "line-item addition");
2. the **frozen** discount amount is **re-capped** at the new gross but **not** recomputed - the band depends on the headcount, which the write did not change;
3. the **deposit is not** touched - it is advisory (BR-30) and the operator may restate it separately;
4. a write that would leave the **collected amount above the new net amount due** is **refused** with a **`400`** and nothing is written, because BR-05 forbids a payment above the net due, and the operator must settle the credit first.
**A dish selection still never moves the money** (BR-37, DEC-129). The two rules are deliberately different, and the difference is the whole point of BR-41.

### 3.6 The extended-location restriction (DEC-141, DEC-156, DEC-157)

- The out-of-town amount applies **only** to the `Regular` and `Premier` tiers.
- **The test is pinned** (DEC-156): the tier's `PackageType` is **trimmed** and compared **case-insensitively** for **equality** with `Regular` or `Premier` **verbatim**. Every other stored value - including a type such as "Premier Buffet" - is **not** a surcharge tier. The comparison is on the **type column only**, never on the description: the development catalogue's own tier is typed **`Regular`** while its *description* reads "2026 Premier Package B", so a description match would give the opposite answer. `Package.PackageType` is free text and DEC-070 deliberately refuses to restrict it, so the test must be stated rather than assumed.
- The catalogue's type values should be normalised to the `PackageType` enum's vocabulary (DEC-070). Until they are, the behaviour is deterministic and the remedy is data hygiene.
- In the workbench, the **"filter by Package Area"** control appears **only** when the selected type is `Regular` or `Premier`; for any other type the packages are filtered by the **type** alone, exactly as the operational note describes.
- **Server-side**, a surcharge applied to a tier of any other type is a **`400`** that **names the two eligible types** - the rule is enforced, not merely hidden (BR-29's "one blocking check" precedent).
- **The area filter has server support** (DEC-157): `GET /api/packages?areaId=` returns the active tiers whose coverage includes that area. The browser never fans out one request per tier.

### 3.7 The default menu is the standard run (DEC-131)

A booking with **no** selection is served the tier's **standard run** (BR-19). The selection is an **override**, not a requirement: a booking may be confirmed, deposited and servable before any dish is chosen, and a selection is **not** required to confirm a booking.

Before the lock, an **explicitly empty** selection **clears** the selection and the event reverts to the standard run; an **absent or `null`** member is a **`400`** (the DEC-085 pattern).

### 3.8 The menu-change window (BR-36, DEC-128)

```
LockDate = EventDate - LockDays
```

`LockDays` is **configuration**: `Bookings:MenuSelection:LockDaysBeforeEvent`.

| Situation | The lock date |
| :--- | :--- |
| Configured (`n` days) | `EventDate - n days` |
| **Absent** | `EventDate` — the selection is editable until the event begins |

**The lock date is always published** and is **never `null`**: the operator always sees the deadline. The **days** figure is `null` when the key is absent — the "not stated" state belongs to the *number of days*, never to the *date*.

A write **after** the lock is refused with a **`400`** that names the lock date, and **nothing** is written. A **cancelled** booking's selection and add-ons are frozen outright.

### 3.9 Validation summary (BR-11)

| Field / condition | Rule |
| :--- | :--- |
| The booking | must exist and must not be soft-deleted |
| The booking's status | a **cancelled** booking's selection and add-ons are frozen |
| The tier | a booking with no tier has no pools and therefore no selection |
| `selections` | **absent or `null`** → `400`; an **explicitly empty array** clears the selection (subject to the lock) |
| Each chosen dish | must be a **live** member of the chosen course's qualified pool |
| Each chosen course | must be a course the tier **has a rule** for |
| The quota | every rule's course must have **exactly** `requiredQuota` chosen dishes |
| Duplicates | the same dish twice for one booking → `400` (the database index is the backstop) |
| `addons` | **absent or `null`** → `400`; an **explicitly empty array** clears the add-ons |
| Each add-on | must be an **active** catalogue row |
| The headcount and the band discount | the band is evaluated on `NumberOfHeads`; the discount never exceeds the gross |
| The add-on's line amount | `>= 0`. It is the **line total**. When absent, the server derives it from the catalogue's amount and the basis - `PerGuest` gives `amount x heads`, `Flat` gives `amount` (DEC-158). The **line total** is what is frozen and what joins the gross. |
| The add-on's pricing basis | comes from the **catalogue**, never from the request. A basis that is **not stated** is treated as `Flat` (DEC-158). |
| The add-on write and the money | the write **re-freezes** the snapshot and **re-caps** the frozen discount; a write that would leave the **collected amount above the new net amount due** is a `400` (DEC-152, BR-43). |
| The tier and the surcharge | a surcharge on a tier whose type is **not** `Regular` or `Premier` - compared **trimmed**, **case-insensitively**, for **equality**, on the **type column only** - is a `400` that names the two eligible types (DEC-156). |
| `prepInstruction` | optional free text, `<= 500` characters; stored on the **selection row** and frozen with it (DEC-146). It is never taken from the catalogue, and an **omitted** member **clears** it (DEC-155). |
| The VAT percentage | **configuration, never a request field** (DEC-143). Absent means no VAT disclosure. The tax never changes `NetAmountDue`. |
| The lock | a write after `menuSelectionLockDate` → `400`, naming the date |

---

## 4. API Surface

### 4.1 Contract §14 - the register, the summary and the quote

**Extended by this cycle:**

- `POST /api/bookings/quote` **accepts** `selections?` and `addons?`, in the **same shapes** the create accepts, and prices them through the **same code path** - so the quote and the create cannot diverge (BR-31, DEC-153).
- `POST /api/bookings/quote` publishes the **automatic band discount**: `discountSource` (`None` | `HeadcountBand` | `Code`), `discountBandLabel` (`string?`), `discountBandRatePerHead` (`decimal?`) and the discount **amount**. The source tells the operator **which** rule produced the number - the band, an explicit code, or nothing - and the **label** and the **bounds** are the **server's**, so the browser never hardcodes a bound (DEC-160).
- `POST /api/bookings/quote` also publishes `addonsTotal`, `includedVatAmount` (the **VAT-inclusive** disclosure, `null` unless `Bookings:Tax:VatPercentage` is configured - DEC-143), `vatPercentage` (the **configured** rate, `null` when absent), and the tier's `courses[]` with each rule's `batchMassKg` (DEC-145).
- `POST /api/bookings` accepts `selections?` and `addons?` and stores them **in the same commit** as the booking; the frozen snapshot gains `AddonsTotal` (DEC-151).
- `GET /api/bookings` publishes the booking's **add-on count** (`addonCount`), its **frozen add-on total** (`addonsTotal`), its **derived discount source** (`discountSource`) and its **selection state** (`selectionState` - exactly one of `None` | `Selected` | `Locked`), so the card can show at a glance whether the event's menu is settled.
- `GET /api/bookings/summary` publishes `menuWindows` (the open and the locked counts), `bandDiscount` (the band-priced count and the net amount due of those bookings) and `vatPercentage`.

### 4.2 Contract §15 - the dish selection

| # | Method & path | Purpose |
| :--- | :--- | :--- |
| 15.1 | `GET /api/bookings/{id}/menus` | The selection, the tier's pools, the quota verdict and the lock state. |
| 15.2 | `PUT /api/bookings/{id}/menus` | **Replace** the selection. Validates BR-15 and the lock. |

**Response `BookingMenuSelectionResponse`**: `transactionId`, `bookingNumber`, `eventDate`, `packageId`, `packageName`, `menuSelectionLockDate` (**never null**), `menuSelectionLockDays` (`int?`), `isLocked`, `selectionState`, `selections[]`, `courses[]` (the tier's rules with their pools), `isCompliant`, `complianceMessage`, `selectedFoodCostPerPax`, `standardRunFoodCostPerPax`, `selectedGrossProfitPercentage`, `standardRunGrossProfitPercentage`, `includedVatAmount`, `vatPercentage`, `addonCount`, `addonsTotal`, `discountSource`, `discountBandLabel`, `discountBandRatePerHead`.

**The payload also carries, per course rule:**

- `batchMassKg` (`decimal?`) - the rule's `PortionGrams x NumberOfHeads / 1000`, the batch mass the kitchen produces for this event (DEC-145). `null` when the rule states no portion **or a portion of zero** (M3). A **planning figure**, derived from the rule the tier already holds.

**The payload also carries, per chosen dish:**

- `prepInstruction` (`string?`) - the chef's per-event instruction for that dish (DEC-146). `null` = not stated.

**And at the top level:**

- `includedVatAmount` (`decimal?`) - the **VAT-inclusive disclosure** for the booking (DEC-143). `null` when no VAT percentage is configured or when the net amount due is zero. It is a **memo** figure: it is disclosed **within** the net amount due and never added to it.
- `vatPercentage` (`decimal?`) - the **configured** rate, `null` when absent.
- `discountSource`, `discountBandLabel`, `discountBandRatePerHead` - the **derived** source of the frozen discount and the band's own description and rate (DEC-154, DEC-160). Published here so the menu ledger names the rule that produced the number and **never infers it or hardcodes a bound**.

**Request** - the **same shape** as the packages' selection simulator (contract §13.3) with one addition: `{ "selections": [ { "courseId": 0, "menuId": "DS-101", "prepInstruction": "Walnuts on the side for the allergy table" } ] }`. An **omitted** `prepInstruction` **clears** it, because the write is a full replace (DEC-155). The response is the **saved** state, not the request.

### 4.2a `GET /api/bookings/{id}/beo` - the Kitchen Banquet Event Order

- **Response `200 OK`:** `application/pdf` with a `Content-Disposition` filename.

- The document is the QuestPDF `KitchenBeoDocument`, rendered from the booking's **selection** - the "menu items" REQ-REP-02 has always asked for and could never have (DEC-148, DEC-159). It is **extended** to carry the **per-event prep instruction** and the booking's **add-on lines**, which are the kitchen's work.

- A booking with **no** selection prints the tier's **course rules** and states plainly that the event runs the tier's standard run and that **no dish selection has been recorded**. It prints **no dish list**: the standard run is a **cost figure**, not a dish set, and inventing one is not an option (DEC-159).

- **Responses:** `200`, `404` (the booking does not exist), `401`, `403`, `500`.

### 4.3 Contract §16 - the add-ons

| # | Method & path | Purpose |
| :--- | :--- | :--- |
| 16.1 | `GET /api/addons` | The add-on master - every active add-on with its category, its amount, its unit and its **pricing basis**, plus the line total it would carry on a booking of the **required** `headcount`. Optional `categoryId` filter. Absent or `< 1` headcount is a `400`: the endpoint prices a line for a booking and has no safe default (BR-20). |
| 16.2 | `GET /api/addons/categories` | The category master. |
| 16.3 | `GET /api/bookings/{id}/addons` | The booking's add-on lines, the frozen total and the lock state. |
| 16.4 | `PUT /api/bookings/{id}/addons` | **Replace** the booking's add-ons. Validates the lock, the catalogue and the money reaction. |

**Request** - `{ "addons": [ { "addonId": 3, "amount": 18000.00 } ] }`. The `amount` is the **line total** the operator confirms; when it is **absent** the server derives it from the catalogue's amount and the basis (DEC-158). The **description**, the **unit** and the **pricing basis** are frozen from the catalogue.

**Response** - the **saved** lines, each with its frozen description, line total, unit and basis, plus the catalogue's current amount alongside it, so the operator can see when a catalogue price has moved since the line was frozen, and the lines' **frozen total**.

**The pricing basis is the catalogue's explicit column** (DEC-158) - `Flat` or `PerGuest`, with a not-stated basis treated as `Flat`. It is **never** inferred from the unit of measure, and the unit stays a **descriptive** unit.

**The write re-freezes the money** (DEC-152): the add-on total is recomputed, `Subtotal`, `DiscountAmount` and `AmountDue` are re-frozen, the frozen discount is **re-capped** at the new gross but not recomputed, the **deposit is not** changed, and a write that would leave the collected amount above the new net amount due is **refused**.

## 5. UI Specification

### 5.1 Routes

| Route | Element | State |
| :--- | :--- | :--- |
| `/bookings` | `BookingsEventsPage` | **Delivered** — gains the **"Menu selection"** action and the add-on count |
| `/bookings/new` | `BookingWorkbenchPage` | **Delivered** — gains the dish picker, the add-on picker and the tier-type → area-filter behaviour |
| `/bookings/:transactionId/menu` | `BookingMenuPage` | **NEW — the menu editor** |

### 5.2 The workbench (create) — `POST /api/bookings`

- The tier controls read top-to-bottom in **Column 2**: **Package type → Area filter → Buffet tier**. Package type is required. Its empty placeholder is **"Select a package type"**; while no type is selected the tier picker is disabled with placeholder **"Select a package type first"** and hint **"Choose the package type to load its tiers"**. For trimmed, case-insensitive exact `Regular` or `Premier` (DEC-156), the **required Area filter** is shown directly above the tier picker; the tier picker stays disabled until an area is selected and its hint names the area as the next required step. The client requests `GET /api/packages?activeOnly=true&areaId={areaId}` and filters the returned active tiers to the selected type by exact, case-sensitive comparison after trimming both strings (DEC-157). For every other stored type, the Area filter is hidden and cleared, and the tier list is `GET /api/packages?activeOnly=true` narrowed to that type. Each buffet-tier option retains its current description/type and appends the exact applicable-pax label resolved by matching `numPaxId` against the existing `GET /api/packages/pax-applicables` lookup (DEC-173, DEC-176). No label is hardcoded or interpreted. While the lookup is pending or unavailable, state that fact without disabling tier selection. A type or area change clears the selected tier immediately; switching between the two eligible types preserves the selected area. Setting the Area filter to a non-empty value copies that area into event `extendedAreaId` in the same draft update; Municipality remains editable, and this prefill never turns on `applyExtendedAmount` (DEC-171). Clearing the Area filter does not reverse-sync the event Municipality. The Municipality selector remains in Column 1 and continues to obey AC-BKG-72.
- While the catalogue for a newly selected area is fetching or displaying placeholder data, the tier picker is disabled and reads **"Loading the tiers for {area}…"**; results from the prior area are never selectable. When the selected type and area have no matching tiers, the picker remains enabled and an explanation names both, with **"Clear the area"** / **"Clear the type"** actions.
- The selected-tier panel shows the exact API lookup label as **Applicable pax classification** for the selected package, along with **Rate per head** as a tier attribute even before the booking is complete enough to request a quote. The classification is resolved from `numPaxId`; pending, failed, or unmatched lookup data is explicitly shown as unavailable/loading and never blocks the selection, quote, or save. Clearing or changing the tier immediately removes the prior tier's classification. The label remains descriptive only: it is never parsed into bounds and has no effect on headcount, eligibility or price (DEC-173, DEC-176). Rate resolution order: a current quote for the selected tier wins; otherwise show the selected tier's catalogue `amountPax`; if neither is stated, render **"not stated"**. Because the quote DTO has no `packageId`, the client correlates a quote with the package ID in the request/query key that produced it; no DTO/API field is added. Since the quote query has no placeholder data, retain the last resolved rate with its request's package ID while recalculating; use it only while that package remains selected. A package change immediately shows the new tier's catalogue rate and never carries the prior tier's figure. A catalogue `null` is never rendered as zero even if the server quote has collapsed it to zero; an actual zero remains `₱0.00`. The rate remains visible during recalculation with a polite, subtle cue and never becomes an empty number or em dash merely because recalculation is in progress. The quoted rate wins on catalogue divergence; show the quiet note **"The quoted rate differs from the catalogue rate — the catalogue has been refreshed."** and invalidate the tier catalogue once. **Base value and the ledger remain in the strict no-stale-figure state** until the current quote is available. The browser never multiplies the rate by headcount. Clearing the package continues to hide the selected-tier panel, and currency readout goes through `formatCurrency` only; the "Rate per head" label carries the unit.
- Client gate validation is an affordance only and is not sent to the API: empty type → **"Select a package type before choosing a buffet tier."**; eligible type with empty area → **"Select the event area before choosing a buffet tier."**; a selected tier while its gate is unsatisfied → **"The selected buffet tier does not match the package type and area."** Save and confirm are blocked until the gate is satisfied. The existing server remains authoritative and continues to accept a live `packageId` without a new request field.
- **Column 2** gains the **dish picker** above the course specification, and the **add-on picker**.
- **Column 3** gains the **band discount** line — which shows the **source** (*"Automatic discount — 100 to 999 heads, ₱20.00 per head"*) so the operator can see the rule that produced the number.
- On success, the workbench **redirects to `/bookings/{id}/menu`** (the owner's ruling 7).

### 5.3 The menu editor (`/bookings/:transactionId/menu`)

The screen follows the `Booking_Menu_Selection` reference: a **two-column workbench** - the course selection list on the left (two thirds) and the **add-ons and the contract ledger** on the right (one third).

**The header bar.** A **back** link to the register, the eyebrow **"Live Menu Workbench"**, and three actions: **Reset defaults** (clears the selection, subject to the lock), **Tasting sheet (PDF)** - an **inactive** button with a capability notice (no tasting-sheet document exists, DEC-150) - and **Save selection** (the design's *"Save & Lock Selection"*; the save is the save, and the **lock** is automatic at the cutoff, DEC-149).

**The event identity banner.** The occasion, the booking number, the **"N-Course Banquet Quota Active"** line (the tier's rule count), the client's name, the event date, the headcount, the venue **with the stored surcharge**, and the tier's name. Three **metric badges**:

- **Course quota** - the selection's compliance count ("10 / 10 - Complete").
- **Batch engine** - the reference's *"Batch Engine: Synced / 120-Pax Yield"*. The **batch mass** is real (DEC-145) and is published per rule; the **ingredient-level** yield is a **capability gap** (REQ-MNU-03), so the badge reads the batch mass and names the gap.
- **Target margin** - the selection's gross profit percentage and its variance to the configured benchmark (both already published by the packages workbench).

**The filter panel.** A **search** over the dish pool (the reference's "180+ commissary recipes" is the pool's size), the **cutoff badge** - *"BEO cutoff: {date}"*, the menu-change window's date, always published - and the **covers** (the headcount).

**The course tabs.** One tab per course rule plus **All** - **derived from the tier's own rules** (DEC-147). The reference's hardcoded groupings ("Proteins", "Starters & Carbs", "Dessert & Drinks") are **not** rendered: they are not modelled.

**The course list.** One card per course rule, in the tier's order, each carrying:

- the **index** and the **course category**;
- the rule's **description**;
- the **chosen dish** - its name, the **`Menu.Note`** (its ingredients), and the rule's **portion** ("125g / pax");
- a **"Change dish"** action that opens the pool for that course;
- the dish's **food cost** per pax (`Menu.FoodCost`, the manually entered cost - a planning figure);
- the **chef prep instruction** input, stored on the selection row (DEC-146);
- and an **allergen chip** area - a **capability gap** (DEC-150): the model carries no allergen field, so the area says so rather than inventing tags.

**The right column.**

- **Add-ons & upgrades** - the master, grouped by **category**, each with its description, its **pricing basis** (per guest or flat), its rate and its line total, each with a checkbox. The **subtotal** is the server's.
- **The contract ledger** - the dark panel: **Base package**, **Selected add-ons**, **Out-of-town fee**, the **discount** (with its **source**), the **VAT disclosure** when configured, the **total contract value**, the **required deposit** (the configured percentage or the stated amount) and the **commissary cost headroom** with its margin track. Every line is the server's.
- **The commissary batch yield** - the per-rule **batch mass** (real, DEC-145) and a **capability gap** for the ingredient-level yield (REQ-MNU-03), the **chef sign-off** (no approval state, DEC-150) and the **quality stamp** (no compliance record, DEC-150).
- **Generate kitchen production order (PDF)** - the **BEO endpoint** (contract §15.5), which is real.

**The states.** A locked booking renders the whole screen read-only and says when it locks. A booking with **no** selection opens with the pools shown and a line saying *"This event is currently served the tier's standard run. Choose the dishes to override it."* A tier with **no** ruleset says so - the standard run is empty too. A non-compliant selection names the course and the quota and the save is disabled. A quotation error shows an alert with a retry.

### 5.4 The add-on editor

The add-ons are edited **in the right column** of the same screen, not on a separate route. The page shows the booking's selected add-on lines and an explicit **Choose add-ons** action; it does not load or render the full master on initial page load. The action opens a modal with a table of active add-ons and a category filter. The category and active master reads are lazy-loaded while the modal is open; category changes use the existing `GET /api/addons?headcount={bookingPax}&categoryId={id}` filter. The server's `lineAmountForBooking` remains the default line total. Only chosen lines appear in the page summary, where the operator may review/remove them and edit a negotiated line total.

Choosing or removing a row changes a **draft only**. The existing **Save selection** action submits the full replacement through `PUT /api/bookings/{id}/addons`; the client does not add the figures into an authoritative contract total. The add-on write applies DEC-152 on the server. On success, the client refreshes `GET /api/bookings/{id}` and the contract ledger and save confirmation show the newly frozen `amountDue` returned by the server. The user is told that totals recalculate on save. This preserves the existing money semantics and does not introduce a quote or a new endpoint.

The dialog is keyboard accessible: focus is trapped and restored, Escape and the close action dismiss it, and the category filter, table and selection controls have accessible names. Locked bookings remain read-only. A master with no active rows shows the explicit empty state - *"No add-ons are defined yet"* - and never a fabricated list.

### 5.5 The dossier card

Gains: the **"Menu selection"** action, the **add-on count**, and the **selection state** chip (*"Menu: not chosen"* / *"Menu: chosen"* / *"Menu: locked"*).

### 5.6 The states, accessibility and presentation

- Every panel states its own emptiness; every gap is a `CapabilityGapNotice`.
- A status, a payment state and a selection state are carried by a **text label plus an icon**, never by colour alone.
- The quota count is always **text** (*"2 of 2 selected"*).
- The lock state is a **status chip with a label**, and the editor is `aria-disabled` when locked.
- Currency is `₱` with two decimals; a `null` figure is an em dash.
- No horizontal overflow at 375 / 768 / 1024 / 1440 px.

---

## 6. KPI Impact

| KPI | Name | Formula | Source |
| :--- | :--- | :--- | :--- |
| **KPI-40** | The selection's food cost and gross profit | `selectedFoodCostPerPax` and its gross profit % for a booking **with** a selection; `null` when nothing is selected or a chosen dish is not costed (DEC-087). | `GET /api/bookings/{id}/menus` |
| **KPI-41** | The open and the locked menu selections | The count of bookings whose lock date is ahead of today (**open**) and behind it (**locked**). | `GET /api/bookings/summary` |
| **KPI-42** | The automatic band discount's reach | The count and the **net amount due** of the bookings whose discount came from the **headcount band** (the discount source is published). | `GET /api/bookings/summary` |

**KPI-42 is the measure of the new automatic rule.** It answers "how much business does the headcount ladder carry, and what did it give away?" — the number a pricing review asks for first. It is a **count** and a **net amount due**, both server-computed.

**The existing register's published estimate becomes conditional** (DEC-132): the **selection's** when the booking has one, the **tier's standard run** when it does not, and the card's label says which.

---

## 7. Acceptance Criteria

- **AC-BKG-51** - A booking's dish selection is **recorded on the booking**, survives a reload, and is visible from `GET /api/bookings/{id}/menus`.
- **AC-BKG-52** - The selection is chosen **only from the tier's qualified pools**, and a dish outside the pool is refused with `400`.
- **AC-BKG-53** - The selection satisfies **BR-15** exactly: `requiredQuota` dishes for every rule's course, and none from a course the tier has no rule for. A breach names the course and the quota. The rule is enforced by **one shared validator**, used by both the booking editor and the packages simulator.
- **AC-BKG-54** - The selection can be changed **after** the booking is confirmed and after the deposit is taken, and the booking's money **does not move**. A test asserts that every money column is byte-identical before and after.
- **AC-BKG-55** - The menu-change window is **configuration**: `Bookings:MenuSelection:LockDaysBeforeEvent`. Absent - the lock date is the event date.
- **AC-BKG-56** - The **lock date is always published** and is never `null`; the days figure is `null` when the key is absent.
- **AC-BKG-57** - A write **after** the lock is refused with `400`, names the lock date, and writes nothing.
- **AC-BKG-58** - An explicitly **empty** selection clears the selection (reverting the event to the tier's standard run) before the lock; a **null** or absent `selections` is a `400`.
- **AC-BKG-59** - A booking with **no** selection is served the tier's **standard run**; a selection is **not** required to confirm a booking.
- **AC-BKG-60** - The register's per-booking planning estimate is the **selection's** when there is one and the **tier's standard run** otherwise, and the card's label says which. The margin is computed against the booking's **frozen** `PricePerHead`, never the tier's current rate.
- **AC-BKG-61** - The create path stores the selection and the add-ons **in the same commit** as the booking; a booking, its dishes and its add-ons are never half-written.
- **AC-BKG-62** - A `PUT` **replaces** the whole selection and the whole add-on set: the removed rows are soft-deleted, and re-choosing one resurrects its row rather than inserting a second one.
- **AC-BKG-63** - The dossier card carries a **"Menu selection"** action that opens the editor for that booking; the reference's "Manage Event" is **not** rendered, because the general edit is not delivered.
- **AC-BKG-64** - The workbench's picker and the editor use **one** pattern and **one** request shape - the packages simulator's `selections` shape.
- **AC-BKG-65** - The editor is read-only and says why when the booking is locked.
- **AC-BKG-66** - The **DTO projection guard** still passes with the new read models: every member of every new payload is assigned by its projection.
- **AC-BKG-67** - The migration is **purely additive**: three new tables, two new columns on existing tables (`Booking.AddonsTotal`, `AddonDetail.AddonCategoryId`) and one more (`AddonDetail.PricingBasis`); no existing column changes and no existing column is dropped.
- **AC-BKG-68** - No business data is seeded (BR-13), and the workplan is realigned in the same cycle.
- **AC-BKG-69** - **The headcount bands are exactly as the owner stated**: `[80, 90)` earns 10.00 per head, `[100, 1000)` earns 20.00 per head, and **90-99 and 1000-and-above earn nothing**. The discount is `heads x rate`, capped at the gross, computed by the server, and frozen on the booking.
- **AC-BKG-70** - The quote and the booking publish the discount's **source** - `HeadcountBand`, `Code` or `None` - **derived** by the exact rule, so the operator can see which rule produced the number.
- **AC-BKG-71** - An explicit discount **code replaces** the automatic band discount; the two never stack.
- **AC-BKG-72** - The **out-of-town amount is refused** for a tier whose type is not `Regular` or `Premier` - compared **trimmed**, **case-insensitively**, for **equality**, on the **type column only** - and the refusal names the two eligible types. The area filter appears in the workbench **only** for those two types, and the **municipality** selector is hidden for a tier that cannot carry the amount, so the delivered workbench never breaks with a `400`.
- **AC-BKG-73** - The add-on master's **categories** are readable and filterable, and an add-on with **no** category reads as *not stated*, never as a named category. The master's **pricing basis** is read from its own column, never inferred from the unit.
- **AC-BKG-74** - A booking's add-on lines **freeze** the description, the **line total**, the unit and the pricing basis; a later catalogue price change never moves a stored line.
- **AC-BKG-75** - The create flow **redirects to the menu-selection screen** once the booking is created.
- **AC-BKG-76** - **The tax is a VAT-inclusive disclosure and the rate is configuration.** With `Bookings:Tax:VatPercentage` set to `12`, the ledger publishes `120300.00 - 120300.00 / 1.12 = 12889.29` **within** the net amount due; with the key **absent** the figure is `null` and the line is **absent** - never "0.00" and never a hardcoded `12`. The tax never changes `NetAmountDue`.
- **AC-BKG-77** - **The batch mass is derived, not invented.** The selection publishes `PortionGrams x NumberOfHeads / 1000` per rule - `180 x 120 / 1000 = 21.6` kg - and `null` when the rule states no portion **or a portion of zero**. It is labelled a **planning figure**.
- **AC-BKG-78** - The **chef prep instruction** is stored **per booking and per dish** on the selection row, travels with it, and never touches the catalogue. It travels in the request; an **omitted** member clears it, because the write is a full replace.
- **AC-BKG-79** - **The add-on's pricing basis is an explicit catalogue column.** A `PerGuest` add-on priced `150.00` on a 120-guest booking carries a line total of `18,000.00`; a `Flat` one carries `1,500.00`. A basis that is **not stated** is treated as `Flat`. The **line total** is frozen and is what joins the gross.
- **AC-BKG-80** - The **course tabs are derived** from the tier's own rules - one per course, plus "All" - and the reference's hardcoded groupings are not rendered.
- **AC-BKG-81** - The **allergen chips, the quality stamp, the chef sign-off and the tasting sheet** render as explicit capability-gap notices; none is fabricated.
- **AC-BKG-82** - The **BEO PDF** is generated from the booking's selection (`GET /api/bookings/{id}/beo`), carrying the per-event prep instruction and the add-on lines; a booking with **no** selection prints the tier's **course rules** and says that no dish selection is recorded - never an invented dish list.
- **AC-BKG-83** - **The add-on total reaches the authoritative calculator.** `Booking.AddonsTotal` holds the frozen sum of the line totals, the calculator reads it, and `Booking.AmountDue` equals the calculator's `NetAmountDue` **to the cent** - with an add-on on the booking, not only without one.
- **AC-BKG-84** - **An add-on write re-freezes the money**: the snapshot is written again, the frozen discount is **re-capped** at the new gross and **not** recomputed, the **deposit is unchanged**, and a write that would leave the collected amount above the new net amount due is **refused** with a `400` and writes nothing.
- **AC-BKG-85** - **The quote and the create cannot diverge.** The quote accepts `selections?` and `addons?` and prices them through the same path, so a quoted total and the created booking's net amount due agree.
- **AC-BKG-86** - The **area filter is answered by the server**: `GET /api/packages?areaId=` returns the active tiers covering that area, and the browser makes no per-tier fan-out.
- **AC-BKG-87** - **The client hardcodes no bound and no rate.** The band label, the band rate, the VAT rate, the batch masses and the line totals are all **published by the server**; the feature's own `noHardcodedFigures` test passes.
- **AC-BKG-88** - The menu page does not fetch the active add-on master until the operator opens **Choose add-ons**. The modal presents a category-filterable table; selecting rows changes the draft, and only selected rows are shown on the page. A save uses the existing full-replace endpoint, and the ledger refreshes to the server's recomputed `amountDue`; no contract total is calculated authoritatively in the browser. The modal supports keyboard dismissal/focus restoration and respects the booking lock.
- **AC-BKG-89** - A tier selected before the booking is complete shows its catalogue rate immediately; the rate cell is never an em dash solely because no quote exists.
- **AC-BKG-90** - Typing a headcount never blanks the rate cell; during quote recalculation retain the last resolved rate only while its request's package ID still matches the selected tier, and show an `aria-live="polite"` cue. Base value and ledger retain the strict no-stale-figure recalculation state. A package change immediately uses the new tier's catalogue rate and never shows the previous tier's rate.
- **AC-BKG-91** - `amountPax: null` renders **"not stated"**, never `₱0.00`, including when a quote reports zero; `amountPax: 0` renders `₱0.00`.
- **AC-BKG-92** - Buffet tier starts disabled and empty until a package type is selected; the package type select is required and its placeholder is **"Select a package type"**.
- **AC-BKG-93** - For `Regular`/`Premier` (trimmed, case-insensitive equality), Area filter is required and the tier picker is disabled until an area is chosen. The list comes from one server-filtered `GET /api/packages?activeOnly=true&areaId=` request and contains only that type's returned tiers; no per-tier fan-out occurs. Choosing a non-empty area prefills event `extendedAreaId` in the same draft update, leaves Municipality editable, and does not enable the surcharge switch (DEC-171).
- **AC-BKG-94** - Type and area changes clear the selected tier in the same state update; a non-eligible type also clears the area. Switching `Regular` ↔ `Premier` retains an already selected area.
- **AC-BKG-95** - During an area change, previous-area results are disabled and cannot be selected; the picker names the new area's loading state even when React Query is using placeholder data.
- **AC-BKG-96** - A stored type with surrounding whitespace filters correctly by trimming both sides; the filter remains exact and case-sensitive after trimming.
- **AC-BKG-97** - Save and confirm are blocked while the type/area gate is unsatisfied and show the specified client validation messages. `packageType` and `areaFilterId` remain UI-only; the create payload shape is unchanged and carries `packageId` only.
- **AC-BKG-98** - When a current quote's rate differs from the selected tier's catalogue rate, the quote wins, a quiet divergence note appears, and the tier catalogue is invalidated once.
- **AC-BKG-99 (DEC-176)** - Every buffet-tier option in the existing booking-create workbench includes the exact applicable-pax label from `GET /api/packages/pax-applicables`; the selected-tier panel shows the same label. Lookup loading/error/unmatched states are explicit and non-blocking; changing or clearing the selected tier never leaves a stale label. The label is not parsed or used by any booking rule or financial calculation.
## 8. Required Tests

### 8.1 Backend - unit

| Suite | Asserts |
| :--- | :--- |
| `BookingMenuSelectionValidatorTests` | BR-15 exactly: the quota per course, no dish from an unruled course, an out-of-pool dish, a duplicate, an empty array's meaning, a `null` member, a cancelled booking. **One shared validator**, asserted to give the packages simulator and the booking editor the **same** verdict. |
| `BookingMenuLockTests` | The lock date from the configuration; absent - the event date; the days figure `null` when absent; a write at and after the lock is refused. |
| `BookingMenuCommandTests` | The replacement semantics (soft-delete, resurrect); the money columns are **untouched** (a byte-for-byte comparison before and after); the same commit as the create; the **prep instruction** is stored, travels, and an **omitted** member **clears** it. |
| `HeadcountBandDiscountTests` | Every band, both edges of each band, the 90-99 gap, 1000 and above, the per-head arithmetic, the cap at the gross, and the code replacing the band. |
| `BookingAddonTests` | The frozen description/**line total**/unit/**pricing basis**; the replacement semantics; an inactive catalogue row refused; a negative line total refused; a `PerGuest` add-on on 120 guests yields `18,000.00` and a `Flat` one `1,500.00`; a **not-stated** basis is `Flat`. |
| `BookingAddonMoneyReactionTests` | An add-on write **re-freezes** the snapshot; the frozen discount is **re-capped** and **not** recomputed; the **deposit is unchanged**; a write that would overpay is **refused**. |
| `ExtendedAreaTierRestrictionTests` | `Regular` and `Premier` allowed - matched **trimmed** and **case-insensitively**; every other type refused **including** "Premier Buffet"; the type column only, never the description; the band calculation unchanged. |
| `BookingMenuSelectionResponseTests` | The null semantics of `selectedFoodCostPerPax` (nothing selected; an uncosted dish); `batchMassKg` rounds to two decimals away from the midpoint and is `null` for a zero or absent portion. |
| `IncludedVatTests` | `Net - Net / (1 + p/100)`; `null` when the rate is absent or the net is zero; the tax never changes `NetAmountDue`. |
| `BookingAmountDueCalculatorAddOnTests` | The calculator **reads `Booking.AddonsTotal`** and the add-on total joins the gross - the regression that the `0m` placeholder made impossible to express. |

### 8.2 Backend - integration

| Scenario | Asserts |
| :--- | :--- |
| The selection round-trips | Create with a selection - the booking and its rows are committed together - `GET` returns it - `PUT` changes it - `GET` returns the change. |
| The money does not move | The booking's money columns before and after a selection change are identical. |
| The lock is enforced | With the window configured, a `PUT` inside the window succeeds and one outside it is a `400` that names the date and writes nothing. |
| The quota is enforced | A short selection is a `400` that names the course; an out-of-pool dish is a `400`. |
| The band discount | A 120-head booking carries a 2,400.00 discount with the source `HeadcountBand`; a 95-head booking carries none; an explicit code replaces it. |
| The tier restriction | A surcharge on a `Wedding` tier is a `400` that names the two eligible types; the area filter returns only the tiers covering the area. |
| The add-ons round-trip | The master's categories and add-ons are readable; the lines are stored frozen; the catalogue price can change afterwards without moving the line. |
| The add-on write moves the money | Adding an add-on raises `Subtotal` and `AmountDue`, **keeps** the frozen discount capped, **keeps** the deposit, and **refuses** a write that would overpay. |
| **The calculator and the stored snapshot agree** | With an add-on on the booking, `Booking.AmountDue` equals the calculator's `NetAmountDue` **to the cent** - on the create path, on the add-on write path, and on the read path. |
| **The quote and the create agree** | A quote that carries the same selection and add-ons returns the same gross, discount and net amount due the create stores. |
| **The register's estimate** | A booking with a selection publishes the selection's estimate; one without publishes the tier's standard run. |
| **The BEO prints the selection** | `GET /api/bookings/{id}/beo` returns a PDF listing the chosen dishes, the prep instruction and the add-on lines; a booking with no selection lists the course rules and says no dish selection is recorded. |

### 8.3 Frontend

| Suite | Asserts |
| :--- | :--- |
| `BookingMenuPage.test.tsx` | The picker renders the pools; a dish is added and removed; the quota count is text; the prep instruction is typed and sent; the save is disabled when non-compliant or locked; the locked state is announced; add-on reads are lazy, the category filter is applied, and the saved total contract value is refreshed from the server (AC-BKG-88). |
| `BookingAddonsPanel.test.tsx` | The add-on table opens on demand, filters by category, changes the selected draft, displays only selected lines on the page, preserves edits across close/reopen, and renders the **empty master** state without fabricated rows. |
| `BookingWorkbenchPage.test.tsx` | The create request carries the selection and the add-ons; the band discount shows its source; type/area cascade, immediate resets, filtered requests, stale-list gating, empty result actions, save/confirm gating and unchanged payload; the event municipality still obeys AC-BKG-72; a successful create redirects to the menu editor. |
| `WorkbenchTierEngine.test.tsx` (new under the existing `features/bookings/__tests__/` directory) | Catalogue rate before quote; zero/null distinction; quote precedence; no blanking during recalculation; one-time divergence invalidation; required type/area/tier states and loading/empty messages. |
| `bookingVocabulary.test.ts` (new under the existing `features/bookings/__tests__/` directory) | Exact client validation messages for missing type, missing eligible area and a tier with an unsatisfied gate. |
| `BookingsEventsPage.test.tsx` | The dossier card carries the **"Menu selection"** action and opens the editor; the card shows the selection state and the add-on count. |
| `noHardcodedFigures.test.ts` | No peso sign, thousands separator, decimal literal, four-digit literal or percentage literal in the feature folder - the band bounds and the VAT rate are **published by the server**. |
| `BookingWorkbenchPage.test.tsx` / `WorkbenchTierEngine.test.tsx` (DEC-176) | The tier option and selected-tier panel show API-fed classification labels; accessible names include the label; loading/error/unmatched states stay explicit and do not block selection or save; switching tiers clears stale labels. The booking mock handles `/packages/pax-applicables` before its general `/packages` matcher. |
## 9. Open Questions for the Owner

| # | Question | What ships today |
| :--- | :--- | :--- |
| 1 | Should the **general booking edit** — venue, occasion, dates, **pax**, **tier** — be delivered, and what does it do to the price? | **Not delivered.** Changing the pax or the tier **reprices** the booking. A separate ruling is needed. |
| 2 | **Do the add-on amounts enter the booking's contract value?** The note gives the junction an `Amount` but does not say so in words. | **Yes** - BR-01's gross formula has always read `BasePackage + ExtendedAreaFee + AddonsTotal + OtherCharges`, and the calculator has carried an `addonsTotal` parameter since the beginning. It has been `0m` only because nothing could create one (RSK-002). The owner's "no repricing" answer covers **dish substitutions**, not new charges (DEC-135). **Please confirm.** |
| 3 | Is the **90–99 head** gap deliberate — a negotiation band — or an omission? | **Read literally as a gap** (DEC-136): 90–99 earns no automatic discount and is handled by a negotiated discount code. |
| 4 | Should a **cost-raising** dish or add-on swap carry a supplementary charge, or does the business absorb it? | **The business absorbs it.** The price never moves (ruling 2). Recorded as **RSK-062**. |
| 5 | Must the client choose their dishes **before** the booking is confirmed? | **No.** The selection is an override; the standard run is the default (DEC-133). |
| 6 | What is the **menu-change window** in days? | **Configuration** — `Bookings:MenuSelection:LockDaysBeforeEvent`, absent by default. |
| 7 | Should the **kitchen** be able to change the selection and the add-ons, or only the event-sales office? | **Any authenticated user**, within the lock window. No kitchen role exists in the model. |
| 8 | Do the add-ons carry a **quantity**, or is the amount the whole line? | The **amount** is the line's commercial truth; there is no quantity column, matching the junction the note specifies (DEC-140). |
| 9 | **Is VAT 12%, and is it included in the price?** The menu-selection design prints a **"Standard 12% EVAT Included"** line whose arithmetic proves the price is **VAT-inclusive** (120,300 / 1.12 leaves exactly the 12,889.29 the design prints). The **rate** is still a tax policy (DEC-008, UNRESOLVED). | **The rate is configuration** (`Bookings:Tax:VatPercentage`), **absent by default** (DEC-143). The **inclusive** treatment is implemented because the design's arithmetic proves it. **Please confirm the rate** - 12% is never hardcoded. |
| 10 | Should the operator be able to **lock** a selection manually, or is the cutoff the only lock? The design's button reads **"Save & Lock Selection"** beside a separate **"BEO cutoff"** badge. | **The cutoff is the only lock** (DEC-149). The button **saves**; the lock is automatic at the cutoff. If a manual lock is wanted it needs a stored state and is a further question. |
| 11 | Should the **add-on master** be administered (categories and add-ons created and edited)? | **Resolved 2026-10-24 (DEC-168):** the **add-on** master is administrable from Settings → Addons. The **category** and **department** masters remain read-only (DEC-169). |
| 12 | Should the **course groupings** ("Proteins", "Starters & Carbs", "Dessert & Drinks") exist as a taxonomy? | **No.** The tabs are **derived from the tier's rules** (DEC-147). A real grouping taxonomy would need a new entity and an owner ruling. |
| 13 | Should a tier whose `PackageType` is spelled outside the vocabulary (for example "Premier Buffet") carry the out-of-town amount? | **No** - the test is **equality** with `Regular` and `Premier` (DEC-156). The catalogue's type values should be normalised to the enum's vocabulary (DEC-070); until they are, a tier spelled outside the two names will not carry the amount. **Please confirm.** |
| 14 | When an add-on is added to a booking that has already been paid, and the new total falls **below** the amount collected, what should happen? | The write is **refused** with a `400` (DEC-152) - BR-05 forbids a payment above the net due, so the operator must settle the credit first. **Please confirm.** |
| 15 | Will the **add-on master** be populated, and by whom? It ships **empty**, so the picker is empty and the feature is inert until the catalogue exists (M12, RSK-066). | **Resolved 2026-10-24 (DEC-168):** the owner or an administrator can populate it from Settings → Addons; the legacy import may also populate it. The **department** lookup remains empty until a department exists (no department CRUD, DEC-169, RSK-071). |
| 16 | Should the workbench's **Area filter** prefill the event **Municipality** for `Regular`/`Premier`? | **Resolved 2026-10-24 (DEC-171):** choosing a non-empty Area filter copies it to `extendedAreaId`; Municipality remains editable and the prefill does not enable `applyExtendedAmount`. This can change the quote's coverage verdict but does not change money while the switch is off. Clearing the Area filter does not reverse-sync the Municipality. |

---

## 10. Task Split

| Task | Agent | Scope |
| :--- | :--- | :--- |
| **TASK-BKG-06-BE** | AGENT 02 | The three new entities and the migration; the `AddonDetail` category; the selection validator; the lock rule and its configuration; the band-discount rule; the tier restriction; the read/replace commands; the extended create; the register's conditional estimate; contract §15 and §16. |
| **TASK-BKG-06-FE** | AGENT 03 | The menu editor; the add-on editor; the dossier card's actions; the workbench's pickers, the area filter and the band-discount line; the post-create redirect; the types; the endpoint group; the routes; the tests. |
| **TASK-BKG-06-GV** | AGENT 01 | The governance record, contracts §15 and §16, the KPI and BR additions, the decisions and risks, the acceptance scenarios, and the workplan realignment. |
