# Sal's CaterSuite — Data Dictionary

Database: `SalsCaterSuiteDb` (Legacy baseline: `PegasusTacV2`)  
ORM: Entity Framework Core 10  

---

### Table: `Booking` (`trn_Id` PK)

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `trn_Id` | `TransactionId` | `INT IDENTITY(1,1)` | NO | Primary Key. Unique booking transaction identifier. |
| `bookingNumber` | `BookingNumber` | `NVARCHAR(30)` | NO | Human-readable booking number, format `BK-YYYYMMDD-NNNNN` (BR-09, DEC-016). Unique index. |
| `transdate` | `TransactionDate` | `DATETIME2` | YES | Date and time the booking record was created. |
| `booktype` | `BookType` | `INT` | YES | Booking classification, stored as the `BookingType` ordinal (`Inside` / `Outside`). |
| `typeofservice`| `TypeOfService` | `INT` | YES | Service style indicator, stored as the `ServiceType` ordinal (Catering, BuffetSetup, Plated, FoodPacks, DropOff, Pickup). |
| `c_Id` | `CustomerId` | `INT` | YES | Foreign Key referencing `Customer.c_Id`. |
| `p_id` | `PackageId` | `INT` | YES | Foreign Key referencing `Package.p_id`. |
| `numberOfHeads` | `NumberOfHeads` | `INT` | YES | Contracted guest count (pax). |
| `occasion` | `Occasion` | `NVARCHAR(150)` | YES | Event occasion (Wedding, Birthday, Corporate, Anniversary). |
| `venue` | `Venue` | `NVARCHAR(200)` | YES | Physical event location or hall name. |
| `reference` | `Reference` | `NVARCHAR(100)` | YES | External client reference or booking note. |
| `specialInstructions` | `SpecialInstructions` | `NVARCHAR(500)` | YES | Free-text kitchen/event instructions. |
| `extendedAreaId`| `ExtendedAreaId` | `INT` | YES | Foreign Key referencing `Area.areaId`. |
| `apply_extendedAmount` | `ApplyExtendedAmount` | `BIT` | YES | Boolean flag: whether area surcharge is added to contract. |
| `eventcolor` | `EventColor` | `NVARCHAR(50)` | YES | Hex color code for calendar visualization (default `#2563eb`). |
| `eventDate` | `EventDate` | `DATE` | YES | **The event date. It is the Accounts Receivable due date** (DEC-041, KPI-13) and the revenue/period date basis (KPI-07, KPI-08). |
| `eventEndDate` | `EventEndDate` | `DATE` | NO | **PLANNED — not yet implemented (DEC-178).** The event's end date, preserving overnight schedules. Existing unambiguous same-day rows backfill from `EventDate`; ambiguous legacy rows with `EventEndTime < EventStartTime` make the migration fail until owner mapping is supplied. |
| `startTime` | `EventStartTime` | `TIME` | YES | Event start time-of-day. |
| `endTime` | `EventEndTime` | `TIME` | YES | Event end time-of-day. |
| `p_name` | `PackageName` | `NVARCHAR(250)` | YES | Frozen display name of the package at booking time (BR-04). |
| `p_pricePerHead` | `PricePerHead` | `DECIMAL(18,2)` | YES | Frozen rate per person (BR-04). |
| `p_amount` | `PackageAmount` | `DECIMAL(18,2)` | YES | Frozen base package amount (`PricePerHead × NumberOfHeads`). |
| `subtotal` | `Subtotal` | `DECIMAL(18,2)` | YES | Gross before discount and tax. |
| `discountType` | `DiscountType` | `INT` | YES | `None` / `Percentage` / `FixedAmount`. |
| `discountPercentage` | `DiscountPercentage` | `DECIMAL(18,4)` | YES | Percentage discount. |
| `discountAmount` | `DiscountAmount` | `DECIMAL(18,2)` | YES | Absolute discount. |
| `taxAmount` | `TaxAmount` | `DECIMAL(18,2)` | YES | Tax component. The tax **policy** is UNRESOLVED (DEC-008). |
| `amountDue` | `AmountDue` | `DECIMAL(18,2)` | YES | Frozen net amount due. See RSK-009 / DEC-035: the **shared** `BookingAmountDueCalculator` path is authoritative for reporting. |
| `depositAmount` | `DepositAmount` | `DECIMAL(18,2)` | YES | Deposit recorded against the booking. No deposit **percentage** is assumed (DEC-009). |
| `amountPaid` | `AmountPaid` | `DECIMAL(18,2)` | YES | **Denormalized cache** of the payment sum — not authoritative (DEC-012). |
| `booking_status` | `Status` | `INT` | YES | **PLANNED — not yet implemented (DEC-177).** Lifecycle-only `BookingStatus` ordinal: `Pending`=0, `Confirmed`=1, `InProgress`=2, `Completed`=3, `Cancelled`=4, `NoShow`=5, `Rescheduled`=6, `Expired`=7. Migration maps legacy ordinals 2/3 (`DepositPaid`/`FullyPaid`) to new `Confirmed` and remaps the remaining values as specified in DEC-177. |
| `reminderSent` | `ReminderSent` | `BIT` | YES | Whether the event reminder has been sent. Default `0`. |
| `row_version` | `RowVersion` | `ROWVERSION` | YES | Concurrency token. |
| `b_createdbyUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Username of staff who encoded the booking. |
| `b_createdDate` | `CreatedDate` | `DATETIME2` | YES | System audit creation timestamp. |
| `b_updatedDate` | `UpdatedDate` | `DATETIME2` | YES | System audit last update timestamp. |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Soft delete flag. Stored only — no global query filter is applied (DEC-038). The dashboard and the Customer Master filter it inside their own queries (DEC-045, DEC-050). |

> **Retired columns.** The legacy `serve_stat` (`ServeStatus`) and `is_cancelled`
> (`IsCancelled`) booleans are **retired** (BR-10) and no longer exist in the model;
> `booking_status` is the single lifecycle field (contract §5). They are not part of
> the current schema and must not be reintroduced.

### Table: `BookingStatusHistory` — **PLANNED — not yet implemented (DEC-178)**

One durable row per lifecycle transition, plus a migration-baseline row preserving each existing pre-DEC-177 status for rollback review. The exact SQL column names/lengths and indexes are subject to AGENT 02's reviewed implementation proposal; required semantics are:

| Concept | Meaning |
| :--- | :--- |
| Booking FK | The `Booking.TransactionId` whose lifecycle changed. |
| Previous / new status names | The exact before/after lifecycle strings; the baseline record also preserves the exact legacy ordinal. |
| Effective / recorded timestamps | The scheduled instant the transition became due and the UTC instant the transition/history row committed. |
| Actor / source | `system` and BG-04 for automatic transitions; migration source for the baseline. |

The status update and its history row commit atomically. Payment status is not stored in this table; it remains a ledger-derived projection.

---

### Table: `Customer` (`c_Id` PK)

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `c_Id` | `CustomerId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `lastname` | `LastName` | `NVARCHAR(100)` | NO | Customer surname. |
| `firstname` | `FirstName` | `NVARCHAR(100)` | NO | Customer given name. |
| `middle` | `Middle` | `NVARCHAR(50)` | YES | Customer middle name/initial. |
| `address` | `Address` | `NVARCHAR(250)` | YES | Billing/residence address. |
| `contact1` | `Contact1` | `NVARCHAR(50)` | YES | Primary telephone / mobile number. |
| `contact2` | `Contact2` | `NVARCHAR(50)` | YES | Secondary contact number. |
| `company` | `Company` | `NVARCHAR(150)` | YES | Organization / corporate client name. |
| `datereg`| `DateRegistered`| `DATETIME2` | YES | Customer registration date. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. Stored only — no query filter is applied (DEC-038). The Customer Master filters it inside its own queries (DEC-050). |

> **Not columns on `Customer`.** The following are *not* stored attributes and have no
> column, property or migration: a customer code (`CUST-YYYY-NNNN`), a Tax
> Identification Number, a customer tier, a metro area, a customer email address, a
> point-of-contact name or role, credit/payment terms, a customer status, and a
> lifetime-value or repeat-rate figure. The Customer Master's **Customer
> Classification** is *derived* from `company` (DEC-051) and its **aging state** is
> derived from the customer's bookings (DEC-052); neither is persisted. A persisted
> classification or tier would require a new decision, a column and a migration.

---

### Table: `Payment` (`pay_no` PK)

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `pay_no` | `PaymentNo` | `NVARCHAR(50)` | NO | Primary Key. Official Receipt / Voucher number. |
| `trn_Id` | `TransactionId` | `INT` | YES | Foreign Key referencing `Booking.trn_Id`. |
| `date_Payment` | `DateOfPayment` | `DATETIME2` | YES | Payment date and time. |
| `particular` | `Particular` | `NVARCHAR(150)` | YES | Description (e.g., Downpayment, 2nd installment, Full). |
| `payType` | `PayType` | `NVARCHAR(50)` | YES | Classification of payment. |
| `amt_Payment` | `AmountPaid` | `DECIMAL(18,2)`| YES | Monetary payment amount. |
| `payment_means`| `PaymentMeans` | `NVARCHAR(50)` | YES | Payment method (`Cash`, `Check`, `GCash`, etc.). |
| `check_no` | `CheckNo` | `NVARCHAR(50)` | YES | Check number (for check payments). |
| `notes` | `Notes` | `NVARCHAR(250)` | YES | Additional cashier notes. |
| `p_createdbyUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username of the staff who encoded the payment (DEC-038). |
| `p_createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `p_updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. Stored only — no query filter is applied (DEC-038). |

---

### Table: `Menu` (`menuid` PK)

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `menuid` | `MenuId` | `NVARCHAR(50)` | NO | Primary Key — an **alphanumeric dish code** the user types (e.g. `DS-104`). It is **not** an identity column and it is **upper-cased by the create command**. |
| `courseId` | `CourseId` | `INT` | YES | Foreign Key referencing `CourseCategory.courseId`. `ON DELETE SET NULL` — a dish with no course is grouped under "Uncategorised" for reporting (KPI-16). |
| `menu_name` | `MenuName` | `NVARCHAR(150)` | NO | Name of the dish. |
| `deptId` | `DeptId` | `INT` | YES | The owning department. **Wired to `Department.deptId`** (FK, Restrict) on 2026-10-24; before that it was an orphaned legacy column. `NULL` = not stated. |
| `note` | `Note` | `NVARCHAR(500)` | YES | Free-text notes. The only free-text field available for recipe notes, allergens or dietary flags — there is **no** structured allergen data (DEC-059). |
| `image` | `Image` | `NVARCHAR(250)` | YES | Plating photo URL. The column exists but was unreachable until the Customer/Menu Master delivery made it writable (RSK-030). |
| `price` | `Price` | `DECIMAL(18,2)` | YES | Selling price per serving/order. |
| `food_cost` | `FoodCost` | `DECIMAL(18,2)` | YES | **Direct ingredient cost per serving — a MANUALLY ENTERED decimal.** No ingredient, yield, supplier or purchase-price model exists, so this figure is entered by the kitchen and is not computed (DEC-059, DEC-062, RSK-025). Database default `0.00`; a dish with `0` or `NULL` counts as **uncosted** (KPI-14). |
| `date_added` | `DateAdded` | `DATETIME2` | YES | Legacy business column — when the dish was added. Distinct from the `createdDate` audit stamp. |
| `date_update` | `DateUpdated` | `DATETIME2` | YES | Legacy business column — when the dish was last updated. Distinct from the `updatedDate` audit stamp. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). Distinct from the legacy `date_added` business column. |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). Distinct from the legacy `date_update` business column. |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. Stored only — no global query filter is applied (DEC-038). The dashboard (DEC-045) and the Menu & Food Costing Master (spec §7.2) filter it inside their own queries. |

> **No unique index on `menuid` beyond the primary key.** The primary key *is* the
> uniqueness guarantee, but the duplicate check in `CreateMenuCommand` compares the
> **raw** request id while the command persists the **upper-cased** id, so a
> case-differing duplicate bypasses the check and fails as an unhandled `500`
> (RSK-028, spec §7.4).

> **Not columns on `Menu`.** There is **no** ingredient list, quantity, unit of
> measure, supplier, purchase price, yield, trim, shrinkage, portion weight,
> plating card, allergen tag, batch, lot/FIFO reference, menu status, active flag
> or recipe version. "Recipe" in this release means the dish record itself
> (DEC-059).

---

### Table: `CourseCategory` (`courseId` PK)

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `courseId` | `CourseId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `course` | `CourseName` | `NVARCHAR(100)` | NO | The course a dish occupies in a banquet progression. **Six rows are seeded** (DEC-018): Appetizer, Soup, Salad, Main Course, Dessert, Beverage. There is **no CRUD endpoint** for course categories — the endpoint is read-only. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. Stored only (DEC-038). |

---

### Table: `Package` (`p_id` PK)

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `p_id` | `PackageId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `p_code` | `PackageCode` | `NVARCHAR(30)` | NO | **NEW (2026-10-24).** The human-readable business code, `PKG-{TYPE}-{NN}` (`PKG-REG-01`). Generated once at create and **frozen**; unique among the live tiers (`IX_Package_p_code`, filtered `is_deleted = 0`). |
| `p_type` | `PackageType` | `NVARCHAR(50)` | YES | Package category (`regular`, `wedding`, `vip`, etc.). |
| `p_descripton` | `Description` | `NVARCHAR(250)` | YES | Package title and inclusion summary. |
| `p_amountPax` | `AmountPax` | `DECIMAL(18,2)`| YES | Price per pax. |
| `minPax` | `MinPax` | `INT` | YES | Minimum guest count required for package. |
| `numPaxId` | `NumPaxId` | `INT` | NO | **NEW (2026-10-28, DEC-173).** Required FK to `PackagePaxApplicable.numPaxId`. Descriptive classification only; it does not define booking headcount limits. `ON DELETE NO ACTION` / EF `Restrict`. |
| `p_extendedLocationAmount` | `ExtendedLocationAmount` | `DECIMAL(18,2)` | YES | **NEW (2026-10-24).** The tier's own **per-guest** extended-location amount. Only `Regular`/`Premier` may carry one; `NULL` = not stated. It is added to the municipality's rate when a booking applies the extended amount. |
| `isActive` | `IsActive` | `BIT` | NO | Availability status flag. |
| `date_Created` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `date_Updated` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. Stored only — no query filter is applied (DEC-038). |

---

### Table: `PackagePaxApplicable` (`numPaxId` PK)

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `numPaxId` | `NumPaxId` | `INT` | NO | Fixed primary key, seeded with the five DEC-173 IDs; not generated by the database. |
| `packageApplicablePax` | `PackageApplicablePax` | `NVARCHAR(100)` | NO | Fixed display label. Stored and returned verbatim; no numeric interval is parsed. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038); the fixed lookup rows are stamped with the migration-authoring UTC timestamp. |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038); `NULL` for migration-seeded fixed reference rows (no fabricated creator identity). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038); `NULL` for never-updated lookup rows. |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`; seeded reference rows are live. |

| `numPaxId` | `packageApplicablePax` |
| :---: | :--- |
| 1 | `15 to 19 Pax` |
| 2 | `20 to 29 Pax` |
| 3 | `30 to 49 Pax` |
| 4 | `80 to 100 Above` |
| 5 | `Not Applicable to PackMeals` |

---

### Table: `Refund` (`Rf_id` PK)

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `Rf_id` | `RefundId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `rfDate` | `RefundDate` | `DATETIME2` | YES | Date refund was issued. |
| `trn_Id` | `TransactionId` | `INT` | YES | Foreign Key referencing `Booking.trn_Id`. |
| `rf_Reason` | `RefundReason` | `NVARCHAR(250)` | YES | Reason for cancellation/refund. |
| `rf_Amount` | `RefundAmount` | `DECIMAL(18,2)`| YES | Gross refundable payment amount. |
| `rfDeduction` | `Deduction` | `DECIMAL(18,2)`| YES | Cancellation fee or penalty deducted. |
| `rfNetAmount` | `NetAmount` | `DECIMAL(18,2)`| YES | Actual net payout to client (`rf_Amount - rfDeduction`). |
| `rf_Stat` | `Status` | `INT` | YES | Refund processing status flag. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. Stored only — no query filter is applied (DEC-038). |

---

## Audit columns on the remaining domain tables (DEC-038)

Every domain entity inherits `BaseEntity` and persists the same four audit columns.
The tables below are not yet documented column-by-column in this dictionary; only their
audit columns are listed here so the audit model is complete. `IsDeleted` is always
`BIT NOT NULL` and is stored only — no query filter is applied.

| Table | `CreatedDate` | `CreatedByUser` | `UpdatedDate` | `IsDeleted` | DB default on `IsDeleted` |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Reservation` | `createdDate` | `createdByUser` | `updatedDate` | `is_deleted` | `0` |
| `CourseCategory` | `createdDate` | `createdByUser` | `updatedDate` | `is_deleted` | `0` |
| `Area` | `createdDate` | `createdByUser` | `updatedDate` | `is_deleted` | `0` |
| `AddonDetail` | `createdDate` | `createdByUser` | `updatedDate` | `is_deleted` | `0` |
| `Discount` | `date_Created` | `createdByUser` | `date_Updated` | `is_deleted` | `0` |
| `Customer` | `createdDate` | `createdByUser` | `updatedDate` | `is_deleted` | `0` |
| `Menu` | `createdDate` | `createdByUser` | `updatedDate` | `is_deleted` | `0` |
| `Package` | `date_Created` | `createdByUser` | `date_Updated` | `is_deleted` | `0` |
| `Payment` | `p_createdDate` | `p_createdbyUser` | `p_updatedDate` | `is_deleted` | `0` |
| `Refund` | `createdDate` | `createdByUser` | `updatedDate` | `is_deleted` | `0` |
| `Booking` | `b_createdDate` | `b_createdbyUser` | `b_updatedDate` | `is_deleted` | none (pre-existing column) |

Column-naming rule: where a table already had an audit convention (`Booking` → `b_*`,
`Payment` → `p_*`, `Package`/`Discount` → `date_Created`/`date_Updated`) it is preserved;
all other tables use the documented generic pattern `createdDate` / `createdByUser` /
`updatedDate` / `is_deleted`. `is_deleted` is the single schema-wide soft-delete column name.

Note: `Booking.is_deleted` predates DEC-038 and was created without a database default
constraint. It is still `BIT NOT NULL`; the default was left unchanged rather than altering
an existing column. Every column added by DEC-038 carries `DEFAULT 0`.


---

### Table: `PackageCourseRule` (`pcr_id` PK) - NEW (DEC-077; fields amended by DEC-172)

The enforced rule a buffet tier holds for one course category. Created by the REQ-PKG-02 migration. **No row is seeded** (BR-13).

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `pcr_id` | `PackageCourseRuleId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `p_id` | `PackageId` | `INT` | NO | Foreign Key referencing `Package.p_id`. Cascade delete. |
| `courseId` | `CourseId` | `INT` | NO | Foreign Key referencing `CourseCategory.courseId`. The course the rule governs. |
| `requiredQuota` | `RequiredQuota` | `INT` | NO | The number of dishes the client must take from this course (>= 1). |
| `portionGrams` | `PortionGrams` | `INT` | YES | The standard portion per guest in grams. `NULL` = not specified. |
| `ruleDescription` | `RuleDescription` | `NVARCHAR(500)` | NO | The enforced rule in plain words, as the coordinator wrote it. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. A rule removed from a ruleset is **soft-deleted**, not destroyed (DEC-085). |

Uniqueness: one live rule per (`p_id`, `courseId`). The write use case rejects a duplicate inside one submitted set with `400` (DEC-085).

**Retired by DEC-172:** the nullable `inclusionTag` and `addonId` columns and the `addonId` FK/index are removed from the target schema by migration `20261026232102_RetireCourseRuleInclusionTagAndUpgradeAddon`. Applying it discards existing values; no backfill is approved. The migration was generated and script-reviewed but is not applied to the development database or any existing/persistent or production database (DEC-020); migration execution was validated only on disposable integration-test databases.

---

### Table: `PackageAreaCoverage` (`pac_id` PK) - NEW (DEC-077)

The link between a buffet tier and a municipality. Created by the REQ-PKG-02 migration. **No row is seeded** (BR-13). Carries **no money column** - the surcharge is the municipality's own rate (DEC-078).

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `pac_id` | `PackageAreaCoverageId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `p_id` | `PackageId` | `INT` | NO | Foreign Key referencing `Package.p_id`. Cascade delete. |
| `areaId` | `AreaId` | `INT` | NO | Foreign Key referencing `Area.areaId`. The covered municipality. Cascade delete. |
| `isActive` | `IsActive` | `BIT` | NO | Whether the tier is currently offered in this municipality. Default `1`. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. A coverage removed from the set is **soft-deleted** (DEC-085). |

Uniqueness: one live coverage per (`p_id`, `areaId`).

---

### Table: `Area` (`areaId` PK) - EXTENDED (DEC-077)

The municipality catalogue. Three optional columns were added by the REQ-PKG-02 migration; every other column is unchanged.

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `areaId` | `AreaId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `areaDetails` | `AreaDetails` | `NVARCHAR(150)` | NO | The municipality or zone name. |
| `additionalRate` | `AdditionalRate` | `DECIMAL(18,2)` | YES | The **per-pax** out-of-town surcharge. Also the BR-01 extended-area rate charged on a booking. One rate per municipality, not one per package (DEC-078). |
| `distanceKm` | `DistanceKm` | `INT` | YES | **NEW.** The distance from the commissary, in kilometres. `NULL` = not stated. |
| `logisticsNote` | `LogisticsNote` | `NVARCHAR(150)` | YES | **NEW.** The dispatch mode (e.g. "Reefer Truck & Mobile Gen"). `NULL` = not stated. |
| `logisticsCost` | `LogisticsCost` | `DECIMAL(18,2)` | YES | **NEW.** The **per-event** logistics cost for this municipality (fuel, thermal retention, generator, provincial labour). `NULL` = not stated, and the gross-profit split then omits the band rather than showing zero (DEC-081). |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. |
---

## The Bookings & Events Operation Tables (REQ-BKG-06 / 07 / 08, added 2026-10-22)

### Table: `AddonCategory` (`addonCategoryId` PK) - NEW (DEC-142)

The add-on master's **category** level. The owner's operational note names it `AddonCategory(CatId, category)`. **No row is seeded** (BR-13) - the master ships **empty**, so the add-on picker is empty and the feature is inert until the catalogue is populated (RSK-066, M12).

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `addonCategoryId` | `AddonCategoryId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `addonCategoryName` | `AddonCategoryName` | `NVARCHAR(150)` | NO | The category's name - the note's `category`. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. |

---

### Table: `Department` (`deptId` PK) - NEW (2026-10-24)

The kitchen / service department master. **No row is seeded** (BR-13). It is the parent of `AddonDetail.deptId` and of the legacy `Menu.deptId` column, which was orphaned before this table existed.

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `deptId` | `DeptId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `deptName` | `DeptName` | `NVARCHAR(150)` | NO | The department's display name. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. |

---

### Table: `AddonDetail` (`addonId` PK) - EXTENDED (DEC-142, DEC-158, 2026-10-24)

The add-on itself. The owner's note gives it as `Addons Details(Id, AddonCategoryId, Addon Description, Amount, Unit of measure(UOM))`. **Two** columns were added by this cycle's migration; every other column is unchanged.

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `addonId` | `AddonId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `addonCategoryId` | `AddonCategoryId` | `INT` | YES | **NEW.** Foreign Key referencing `AddonCategory.addonCategoryId`. **Restrict** delete. `NULL` = **not stated** - an add-on may pre-date the category master; it is never a category named "General". |
| `deptId` | `DeptId` | `INT` | YES | **NEW (2026-10-24).** Foreign Key referencing `Department.deptId`. **Restrict** delete. `NULL` = **not stated** - the legacy add-on rows pre-date the department master. |
| `pricingBasis` | `PricingBasis` | `INT` | YES | **NEW.** `0` = `Flat`, `1` = `PerGuest`. `NULL` = **not stated**, and the server treats it as `Flat` - the safe direction, because it never multiplies a price that was not meant to be multiplied (DEC-158). |
| `description` | `Description` | `NVARCHAR(150)` | NO | The catalogue description - the note's `Addon Description`. |
| `unitPrice` | `UnitPrice` | `DECIMAL(18,2)` | NO | The **unit** amount - per guest when the basis is `PerGuest`, the whole line when it is `Flat`. |
| `unit` | `Unit` | `NVARCHAR(50)` | YES | The unit of measure - the note's `UOM`. **Descriptive only**: it never decides the pricing basis (DEC-158). `NULL` = not stated. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. |

---

### Table: `BookingMenu` (`bookingMenuId` PK) - NEW (DEC-127)

The dishes chosen for **one booking**, one row per chosen dish. **No row is seeded** (BR-13).

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `bookingMenuId` | `BookingMenuId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `trn_Id` | `TransactionId` | `INT` | NO | Foreign Key referencing `Booking.trn_Id`. **Cascade** delete - the selection dies with its booking. |
| `menuId` | `MenuId` | `NVARCHAR(50)` | NO | Foreign Key referencing `Menu.menuid` (the catalogue's key is a **string**). **Restrict** delete (M4). |
| `courseId` | `CourseId` | `INT` | NO | Foreign Key referencing `CourseCategory.courseId`. **Restrict** delete. **Frozen** at selection time, so the selection stays self-describing if the catalogue later re-assigns a dish (RSK-063). |
| `prepInstruction` | `PrepInstruction` | `NVARCHAR(500)` | YES | The chef's **per-event** prep instruction for this dish. It is **per booking**, so it lives on the selection row and not on the catalogue (DEC-146). `NULL` = not stated. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. A dish no longer chosen is **soft-deleted**, and re-choosing it **resurrects** its row rather than inserting a second one (DEC-085). |

Indexes: a **filtered unique index** on (`trn_Id`, `menuId`) with the predicate `is_deleted = 0` (the DEC-088 precedent), and an index on (`trn_Id`).

---

### Table: `BookingAddOn` (`bookingAddOnId` PK) - NEW (DEC-139)

The add-on lines carried by **one booking**, one row per line. **No row is seeded** (BR-13).

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `bookingAddOnId` | `BookingAddOnId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `trn_Id` | `TransactionId` | `INT` | NO | Foreign Key referencing `Booking.trn_Id`. **Cascade** delete. |
| `addonId` | `AddonId` | `INT` | NO | Foreign Key referencing `AddonDetail.addonId`. **Restrict** delete (M4). |
| `description` | `Description` | `NVARCHAR(150)` | NO | The **frozen** description (BR-04). |
| `amount` | `Amount` | `DECIMAL(18,2)` | NO | The **frozen line total** - the line's commercial truth and the amount that enters the gross (DEC-135, DEC-158). |
| `unit` | `Unit` | `NVARCHAR(50)` | YES | The **frozen** unit of measure - **descriptive only**. `NULL` = not stated. |
| `pricingBasis` | `PricingBasis` | `INT` | NO | The **frozen** pricing basis: `0` = `Flat`, `1` = `PerGuest` (DEC-158). |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. A line no longer carried is **soft-deleted** (DEC-085). |

Indexes: a **filtered unique index** on (`trn_Id`, `addonId`) with the predicate `is_deleted = 0`, and an index on (`trn_Id`).

---

### Table: `Booking` (`trn_Id` PK) - EXTENDED (DEC-151)

One column was added by this cycle's migration; every other column is unchanged.

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `addonsTotal` | `AddonsTotal` | `DECIMAL(18,2)` | NO | **NEW.** The **frozen** sum of the booking's add-on **line totals**. It is BR-01's third gross term, and the value the authoritative calculator **reads** - so `Booking.AmountDue` and the calculator's `NetAmountDue` stay equal to the cent (DEC-099, DEC-151). Default `0`. |
