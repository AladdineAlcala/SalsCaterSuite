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
| `booking_status` | `Status` | `INT` | YES | The BR-10 booking lifecycle, stored as the `BookingStatus` ordinal (`Pending`=0 … `Expired`=9). |
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
| `deptId` | `DeptId` | `INT` | YES | **Legacy and orphaned** — there is no `Department` entity, no foreign key constraint, and nothing in the codebase reads or writes it. Not exposed by any endpoint. |
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
| `p_type` | `PackageType` | `NVARCHAR(50)` | YES | Package category (`regular`, `wedding`, `vip`, etc.). |
| `p_descripton` | `Description` | `NVARCHAR(150)` | YES | Package title and inclusion summary. |
| `amount_pax` | `AmountPax` | `DECIMAL(18,2)`| YES | Price per pax. |
| `minPax` | `MinPax` | `INT` | YES | Minimum guest count required for package. |
| `isActive` | `IsActive` | `BIT` | NO | Availability status flag. |
| `date_Created` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `date_Updated` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. Stored only — no query filter is applied (DEC-038). |

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

### Table: `PackageCourseRule` (`pcr_id` PK) - NEW (DEC-077)

The enforced rule a buffet tier holds for one course category. Created by the REQ-PKG-02 migration. **No row is seeded** (BR-13).

| Column Name | Entity Property | Data Type | Nullable | Description / Business Meaning |
| :--- | :--- | :--- | :--- | :--- |
| `pcr_id` | `PackageCourseRuleId` | `INT IDENTITY(1,1)` | NO | Primary Key. |
| `p_id` | `PackageId` | `INT` | NO | Foreign Key referencing `Package.p_id`. Cascade delete. |
| `courseId` | `CourseId` | `INT` | NO | Foreign Key referencing `CourseCategory.courseId`. The course the rule governs. |
| `requiredQuota` | `RequiredQuota` | `INT` | NO | The number of dishes the client must take from this course (>= 1). |
| `portionGrams` | `PortionGrams` | `INT` | YES | The standard portion per guest in grams. `NULL` = not specified. |
| `ruleDescription` | `RuleDescription` | `NVARCHAR(500)` | NO | The enforced rule in plain words, as the coordinator wrote it. |
| `inclusionTag` | `InclusionTag` | `NVARCHAR(50)` | YES | The operator's inclusion label for the category (e.g. `Included`). `NULL` = no tag. |
| `addonId` | `UpgradeAddonId` | `INT` | YES | Foreign Key referencing `AddonDetail.addonId`. The allowed tier-1 upgrade. `NULL` = no upgrade. |
| `createdDate` | `CreatedDate` | `DATETIME2` | YES | Audit: system creation timestamp (DEC-038). |
| `createdByUser` | `CreatedByUser` | `NVARCHAR(100)` | YES | Audit: username that created the row (DEC-038). |
| `updatedDate` | `UpdatedDate` | `DATETIME2` | YES | Audit: system last-update timestamp (DEC-038). |
| `is_deleted` | `IsDeleted` | `BIT` | NO | Audit: soft-delete flag, database default `0`. A rule removed from a ruleset is **soft-deleted**, not destroyed (DEC-085). |

Uniqueness: one live rule per (`p_id`, `courseId`). The write use case rejects a duplicate inside one submitted set with `400` (DEC-085).

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
