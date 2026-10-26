# Sal's CaterSuite — Acceptance Test Scenarios (Gherkin Scenarios)

These acceptance test scenarios validate business correctness and financial integrity across the entire application.

---

### Scenario 1: Standard Buffet Booking with Add-ons, Extended Area, and Fixed Discount
```gherkin
Feature: Booking Financial Calculation
  As a catering sales coordinator
  I want the system to calculate gross, discount, and net amount due accurately
  So that the client is billed the exact authorized amount

  Scenario: Create booking with extended area and fixed discount
    Given a customer "Maria Santos" (ID: 10) exists
    And a catering package "VIP Buffet" (ID: 5) exists with price ₱500.00 per pax
    And an extended delivery area "Subic Bay" exists with additional rate ₱5,000.00
    When I create a booking for 100 pax with:
      | Field               | Value        |
      | Occasion            | Debut Party  |
      | ApplyExtendedAmount | true         |
      | ExtendedAreaRate    | ₱5,000.00    |
      | AddonsTotal         | ₱3,500.00    |
      | OtherChargesTotal   | ₱1,500.00    |
      | FixedDiscount       | ₱6,000.00    |
    Then the Base Package Amount must be ₱50,000.00
    And the Gross Contract Amount must be ₱60,000.00 (50000 + 5000 + 3500 + 1500)
    And the Discount Amount applied must be ₱6,000.00
    And the Net Amount Due must be ₱54,000.00 (60000 - 6000)
    And the Remaining Balance must be ₱54,000.00
    And the booking status must be "With Balance" and IsFullyPaid false
```

---

### Scenario 2: Chronological Payment Allocation and Fully Paid Status
```gherkin
Feature: Payment Allocation and Ledger Summary
  As a catering cashier
  I want to record installment payments against a booking
  So that the client's balance due is accurately reduced and an Official Receipt is generated

  Scenario: Two payments leading to full settlement
    Given booking ID 101 has a Net Amount Due of ₱54,000.00
    When the cashier records a 50% downpayment of ₱27,000.00 via "GCash"
    Then the system generates a receipt with format "OR-YYYYMMDD-XXXX"
    And the booking Total Payments Received becomes ₱27,000.00
    And the Remaining Balance becomes ₱27,000.00
    And IsFullyPaid is false
    When the cashier records a final payment of ₱27,000.00 via "Cash"
    Then the Total Payments Received becomes ₱54,000.00
    And the Remaining Balance becomes ₱0.00
    And IsFullyPaid becomes true
```

---

### Scenario 3: Prevent Cancellation of Already-Served Catering Events
```gherkin
Feature: Booking Cancellation Guard Rails
  As a catering manager
  I want the system to reject cancellation requests for events that have already been served
  So that delivered services cannot be fraudulently refunded

  Scenario: Attempting to cancel an event with ServeStatus true
    Given booking ID 205 has ServeStatus = true
    When staff attempts to cancel booking ID 205 with reason "Client requested refund"
    Then the system must reject the request with HTTP 400 Bad Request
    And the error message must state "Cannot cancel event that has already been served."
    And the booking ServeStatus remains true and IsCancelled remains false
```

---

### Scenario 4: Refund Amount Cannot Exceed Payments Received
```gherkin
Feature: Refund Validation
  As a financial controller
  I want refund payouts to be strictly capped at the actual payments received
  So that Sal's Catering does not pay out uncollected funds

  Scenario: Attempting to refund more than total payments received
    Given booking ID 308 has total payments received of ₱10,000.00
    When staff attempts to cancel the booking with a requested refund amount of ₱15,000.00
    Then the system must reject the request with HTTP 400 Bad Request
    And the error message must state "Refund amount cannot exceed the total payments received."
```

---

### Scenario 5: Historical Pricing Lock Against Catalog Price Increases
```gherkin
Feature: Historical Pricing Protection
  As an auditor
  I want confirmed bookings to retain their original agreed package price
  Even when package catalog rates are increased in the future

  Scenario: Package rate updated after booking confirmation
    Given booking ID 402 was confirmed with package "Regular Buffet" at ₱350.00 per pax for 100 pax (₱35,000.00)
    When an administrator updates the catalog rate for "Regular Buffet" to ₱450.00 per pax
    Then viewing booking ID 402 must still display Base Package Amount of ₱35,000.00
    And the Net Amount Due for booking ID 402 must remain completely unchanged
```

> **Documentation alignment note (Agent 01) — updated after owner approval:**
> - Scenario 3: the implemented domain rejects `Completed`/already-`Cancelled` bookings with the messages `"A completed booking cannot be cancelled."` and `"Booking is already cancelled."`. The scenario wording has been aligned to the implemented messages.
> - Scenario 4 (refund cannot exceed payments received, BR-06): **APPROVED as DEC-024 (F7) and DELIVERED.** A refund above the authoritative payments-received total is rejected with `400` and `"Refund amount cannot exceed the total payments received."`; a negative amount is rejected with `"Refund amount must not be negative."`. Covered by unit tests and by integration Scenario 13 against real SQL Server.

---

### Scenario 6: Booking Creation Persists a Unique, Formatted Booking Number (integration)
```gherkin
Feature: Booking Identity
  Scenario: Create a booking through the API against a real SQL Server database
    Given a customer and a package exist in the database
    When I POST /api/bookings with a valid payload
    Then the response is 201 Created with a Location header
    And the created booking has a non-blank BookingNumber matching "BK-YYYYMMDD-NNNNN"
    When I POST a second valid booking for the same day
    Then its BookingNumber is different from the first booking's number
```

---

### Scenario 7: Payment Recording Keeps Details and Ledger Reconciled (integration)
```gherkin
Feature: Authoritative Payment Figures (BR-10)
  Scenario: Recording a payment updates every financial view consistently
    Given booking BK-... has a Net Amount Due of ₱45,000.00 and no payments
    When the cashier records a payment of ₱15,000.00 via "GCash"
    Then the receipt number matches "OR-YYYYMMDD-XXXX"
    And booking details report amountPaid ₱15,000.00 and balance ₱30,000.00
    And the ledger reports totalPaid ₱15,000.00 and balanceDue ₱30,000.00
    And the booking status is "DepositPaid"
    And booking details' amountPaid equals the ledger's totalPaid
```

---

### Scenario 8: Both Ledger Endpoints Return the Same Authoritative Values (integration)
```gherkin
Feature: Single Ledger Projection (DEC-015)
  Scenario: Bookings ledger and payments ledger agree
    Given a booking with recorded payments and a refund
    When I GET /api/bookings/{id}/ledger
    And I GET /api/payments/ledger/{transactionId}
    Then both responses have identical contractAmount, totalPaid, totalRefunded, balanceDue and isFullyPaid
    And both responses contain the same number of payment entries
```

---

### Scenario 9: Invalid Requests Return 400, Never 500 (integration)
```gherkin
Feature: Validation & Error Contract (BR-11)
  Scenario Outline: Rejected payloads
    When I POST /api/bookings with <invalid payload>
    Then the response is 400 Bad Request
    And the body contains an "errors" array
    And no booking row is created

    Examples:
      | invalid payload                                  |
      | noOfPerson = 0                                   |
      | endDate earlier than startDate                   |
      | occasion longer than 150 characters              |
      | customerId that does not exist                   |
```

---

### Scenario 10: Role-Based Authorization (integration)
```gherkin
Feature: Authorization Policies (BR-12)
  Scenario: Cancellation is restricted to authorized roles
    Given a booking exists
    When I POST /api/bookings/{id}/cancel with a token whose roles are "Cashier"
    Then the response is 403 Forbidden
    And the booking status is unchanged
    When I POST /api/bookings/{id}/cancel with a token whose roles include "Manager"
    Then the response is 200 OK
    And the booking status is "Cancelled"

  Scenario: Payment recording allows cashier
    When I POST /api/payments with a token whose roles are "Cashier"
    Then the response is 200 OK

  Scenario: Anonymous access is rejected
    When I call any business endpoint without a token
    Then the response is 401 Unauthorized
```

---

### Scenario 11: Migrations and Seeding Apply Cleanly to an Empty Database (integration)
```gherkin
Feature: Database Migration & Seeding (DEC-018, DEC-019)
  Scenario: Fresh database provisioning
    Given an empty SQL Server database
    When all EF Core migrations are applied
    Then every mapped table exists and the schema matches the model
    And no migration remains pending
    When the seeding routine runs
    Then the roles Administrator, Manager, Cashier and Accountant exist
    And exactly one bootstrap administrator account exists
    And standard course categories exist
    When the seeding routine runs a second time
    Then no duplicate roles, users or reference rows are created
```

---

### Scenario 12: Architectural Boundary Guard
```gherkin
Feature: Clean Architecture Enforcement
  Scenario: No controller accesses persistence
    Given the WebApi assembly
    Then no controller type references ISalsCaterSuiteDbContext or Microsoft.EntityFrameworkCore
    And every command/query used by a controller resolves from the Application layer
```

---

### Scenario 13: Cancellation Refund Guard (DEC-024 / F7)
```gherkin
Feature: Refund Boundary Enforcement
  As a finance manager
  I want refunds limited to what was actually collected
  So that a cancellation can never pay out more than the client paid

  Scenario: Refund exceeds payments received
    Given booking 501 has received total payments of ₱10,000.00
    When I cancel booking 501 with a refund amount of ₱15,000.00
    Then the response is 400 Bad Request
    And the body contains "Refund amount cannot exceed the total payments received."
    And booking 501 is still not cancelled
    And no refund row is created

  Scenario: Refund equal to payments received is allowed
    Given booking 502 has received total payments of ₱10,000.00
    When I cancel booking 502 with a refund amount of ₱10,000.00
    Then the response is 200 OK
    And booking 502 is cancelled with a processed refund of ₱10,000.00

  Scenario: Zero refund means no refund row
    Given booking 503 has received total payments of ₱10,000.00
    When I cancel booking 503 with a refund amount of ₱0.00
    Then the response is 200 OK
    And booking 503 is cancelled
    And no refund row is created for booking 503
```

---

### Scenario 14: Accounts Receivable Reconciliation (DEC-025)
```gherkin
Feature: Receivables Reporting
  As the business owner
  I want receivables to report money actually owed
  So that collections are never mistaken for receivables

  Scenario: Receivables keep only unpaid balances
    Given three non-cancelled bookings exist:
      | Booking | Net Amount Due | Payments Received |
      | 601     | ₱50,000.00     | ₱20,000.00        |
      | 602     | ₱30,000.00     | ₱30,000.00        |
      | 603     | ₱40,000.00     | ₱0.00             |
    And booking 604 was cancelled with ₱10,000.00 collected
    When I GET /api/customers (or the receivables summary)
    Then TotalReceivables equals ₱70,000.00
    And the total equals the sum of each booking's ledger balanceDue
    And no fully paid (602) or cancelled (604) booking contributes to the total
```

---

### Scenario 15: Client-Supplied Payment Amount Is Never Trusted as the Balance
```gherkin
Feature: Server-Authoritative Balances
  Scenario: A large overpayment does not create a negative balance
    Given booking 701 has a net amount due of ₱20,000.00
    When a payment of ₱25,000.00 is recorded
    Then the ledger reports totalPaid ₱25,000.00
    And balanceDue is ₱0.00 (never negative)
    And isFullyPaid is true
```

---

## Dashboard Acceptance Scenarios (REQ-DSH-01, added 2026-10-19)

> Specification: `business/dashboard-feature-spec.md` v1.0 · Contract: `architecture/approved-api-contracts.md` §9.
> Every figure below is **server-calculated**. The frontend displays; it never derives a financial value (DEC-002).

---

### Scenario 16: Dashboard Reconciles With the Per-Booking Ledger (integration) — RELEASE GATE
```gherkin
Feature: Dashboard  Reconciliation (DEC-015, DEC-025)
  As the business owner
  I want the dashboard's receivables to equal the sum of the individual booking ledgers
  So that the executive view and the transaction detail can never disagree

  Scenario: Dashboard AR equals the sum of ledger balances
    Given the following non-cancelled bookings exist:
      | Booking | Event Date  | Net Amount Due | Payments Received |
      | 801     | 2026-10-05  | ₱50,000.00     | ₱20,000.00        |
      | 802     | 2026-10-12  | ₱30,000.00     | ₱30,000.00        |
      | 803     | 2026-09-20  | ₱40,000.00     | ₱0.00             |
      | 804     | 2026-10-25  | ₱60,000.00     | ₱10,000.00        |
    And booking 805 was cancelled with ₱15,000.00 collected
    When I GET /api/dashboard?period=2026-10
    Then receivables.outstandingAmount equals ₱120,000.00
    And receivables.bookingCount equals 3
    And the sum of receivables.agingBuckets[].amount equals ₱120,000.00
    And outstandingAmount equals the sum of the ledger balanceDue of bookings 801, 802, 803 and 804
    And no cancelled booking contributes to the total
```

---

### Scenario 17: AR Aging Buckets Use the Event Date as the Due Date (DEC-041)
```gherkin
Feature: Receivables Aging
  As a finance manager
  I want to know how long each outstanding balance has been overdue
  So that collection priority is clear

  Scenario Outline: A booking lands in exactly one bucket
    Given a non-cancelled booking with a net amount due of ₱10,000.00 and no payments
    And its event date is <eventDate> relative to today
    When I GET /api/dashboard
    Then its balance appears in exactly one aging bucket
    And the bucket is <expectedBucket>
    And the overdue count includes this booking only when the bucket is Days31To60, Days61To90 or Over90Days

    Examples:
      | eventDate   | expectedBucket |
      | tomorrow    | NotYetDue      |
      | today       | NotYetDue      |
      | 1 day ago   | Days1To30      |
      | 30 days ago | Days1To30      |
      | 31 days ago | Days31To60     |
      | 60 days ago | Days31To60     |
      | 61 days ago | Days61To90     |
      | 90 days ago | Days61To90     |
      | 91 days ago | Over90Days     |
```

---

### Scenario 18: Dashboard Bookings Split and Period Scoping
```gherkin
Feature: Period-Scoped Bookings KPI (KPI-07)
  Scenario: Committed, Tentative and Other bookings are counted separately
    Given the following bookings exist in period 2026-10:
      | Booking | Event Date | Status       | Net Amount Due | Pax |
      | 901     | 2026-10-02 | Confirmed    | ₱100,000.00   | 200 |
      | 902     | 2026-10-05 | DepositPaid  | ₱50,000.00    | 100 |
      | 903     | 2026-10-09 | FullyPaid    | ₱75,000.00    | 150 |
      | 904     | 2026-10-15 | Pending      | ₱25,000.00    | 50  |
      | 905     | 2026-10-18 | Completed    | ₱40,000.00    | 80  |
      | 906     | 2026-10-20 | Cancelled    | ₱60,000.00    | 120 |
      | 907     | 2026-11-04 | Confirmed    | ₱90,000.00    | 180 |
    When I GET /api/dashboard?period=2026-10
    Then bookings.totalCount equals 5
    And bookings.committedCount equals 3
    And bookings.tentativeCount equals 1
    And bookings.otherCount equals 1
    And bookings.contractedValue equals ₱290,000.00
    And bookings.totalPax equals 580
    And revenue.netContractedRevenue equals ₱290,000.00
    And revenue.averageBookingValue equals ₱58,000.00
    And the November booking and the cancelled booking are excluded
```

---

### Scenario 19: Undefined Ratios Are Null, Never Zero
```gherkin
Feature: KPI Null Semantics
  Scenario: A period with no revenue has no collection ratio
    Given there are no bookings and no payments in period 2026-10
    When I GET /api/dashboard?period=2026-10
    Then revenue.netContractedRevenue equals ₱0.00
    And revenue.averageBookingValue is null
    And collections.collectionRatioPercentage is null
    And collections.changePercentage is null
    And bookings.totalCount equals 0
    And the API returns 200 OK
    And the dashboard renders an empty state and the ratio renders as "—", never 0% or 100%
```

---

### Scenario 20: The Benchmark Is Configuration, Not a Hardcoded Constant
```gherkin
Feature: Configurable Margin Benchmark (DEC-040)
  Scenario: The API returns the configured benchmark and the menu costing service agrees with it
    Given the configuration Dashboard:Benchmarks:TargetFoodCostPercentage is 32.5
    When I GET /api/dashboard
    Then benchmarks.targetFoodCostPercentage equals 32.5
    And benchmarks.targetMarginPercentage equals 67.5
    And benchmarks.isConfigured is true
    And the target food-cost percentage returned by GET /api/menus/costing/all equals 32.5

  Scenario: The default applies when the configuration is absent
    Given the configuration Dashboard:Benchmarks:TargetFoodCostPercentage is not set
    When I GET /api/dashboard
    Then benchmarks.targetFoodCostPercentage equals 35.0
    And benchmarks.isConfigured is false
```

---

### Scenario 21: The Dashboard Never Invents A Figure
```gherkin
Feature: Server-Authoritative Dashboard (DEC-002, DEC-044)
  Scenario Outline: Every dashboard panel is bound to an approved field
    Then the dashboard renders no peso amount, percentage or count that is not present in the API payload
    And the margin panel is labelled "Catalogue Margin" and shows no "vs last month" comparison
    And the receivables panel shows no BEO production state
    And the deposit panel is labelled "Bookings Awaiting Deposit" and shows no percentage
    And the receipts panel shows no "Compliant" badge
    And the footer shows no product version number

  Scenario: A design element with no backing data is not fabricated
    Given the API payload contains no benchmark
    Then the margin card renders the benchmark as "Not configured" rather than a default number
```

---

### Scenario 22: The Dashboard Requires Authentication
```gherkin
Feature: Dashboard Authorization (BR-12)
  Scenario: Anonymous access is rejected and a malformed period is a business error
    When I GET /api/dashboard without a token
    Then the response is 401 Unauthorized
    And the body contains "Authentication is required."
    When I GET /api/dashboard?period=banana with a valid token
    Then the response is 400 Bad Request
    And the body contains "Period must be in the format YYYY-MM."
```


---

### Scenario 23: The Customer Master Directory Reconciles With the Ledger (integration) — RELEASE GATE
```gherkin
Feature: Customer Master Directory (REQ-CUS-01)
  As the business owner
  I want the customer directory's receivables to report money actually owed
  So that the directory and the dashboard can never disagree

  Scenario: Directory AR equals the sum of the per-customer balances and the dashboard AR
    Given the following non-cancelled bookings exist:
      | Booking | Customer | Event Date  | Net Amount Due | Payments Received |
      | 901     | 1        | 2026-10-05  | ₱50,000.00     | ₱20,000.00        |
      | 902     | 1        | 2026-10-12  | ₱30,000.00     | ₱30,000.00        |
      | 903     | 2        | 2026-09-20  | ₱40,000.00     | ₱0.00             |
      | 904     | 2        | 2026-10-25  | ₱60,000.00     | ₱10,000.00        |
    And booking 905 was cancelled with ₱15,000.00 collected
    When I GET /api/customers/directory
    Then summary.activeArAmount equals ₱120,000.00
    And summary.clientsWithBalance equals 2
    And the sum of items[].outstandingBalance equals ₱120,000.00
    And the customer with booking 901 has an outstandingBalance of ₱30,000.00
    And the customer with booking 902 has an outstandingBalance of ₱0.00
    And the cancelled booking contributes nothing
    And GET /api/dashboard reports the same receivables.outstandingAmount
```

### Scenario 24: A Per-Customer Statement Uses the Authoritative Net Amount Due
```gherkin
Feature: Customer Statement of Account (REQ-REC-01, F11)
  Scenario: The statement reconciles with the per-booking ledger
    Given a non-cancelled booking has a net amount due of ₱198,000.00 and ₱198,000.00 paid
    When I GET /api/customers/{id}/statement
    Then the statement line amount equals ₱198,000.00
    And the statement line balance equals ₱0.00
    And the statement line status is a BR-10 booking lifecycle name
    And the statement totalBilled equals the ledger contractAmount for that booking
```

### Scenario 25: AR Aging Uses the Event Date on the Customer Statement
```gherkin
Feature: Receivables Aging (DEC-041, DEC-053)
  Scenario Outline: A customer lands in exactly one bucket
    Given a non-cancelled booking with a net amount due of ₱10,000.00 and no payments
    And its event date is <eventDate> relative to today
    When I GET /api/customers/directory
    Then that customer's ledgerState is <expectedBucket>
    And the client renders the server's ledgerStateLabel verbatim

    Examples:
      | eventDate   | expectedBucket |
      | tomorrow    | NotYetDue      |
      | today       | NotYetDue      |
      | 1 day ago   | Days1To30      |
      | 30 days ago | Days1To30      |
      | 31 days ago | Days31To60     |
      | 60 days ago | Days31To60     |
      | 61 days ago | Days61To90     |
      | 90 days ago | Days61To90     |
      | 91 days ago | Over90Days     |
```

### Scenario 26: An Overpaid Booking Never Shows a Negative Balance
```gherkin
Feature: Server-Authoritative Balances (BR-03)
  Scenario: Overpayment clamps to zero on the directory and the statement
    Given a booking has a net amount due of ₱20,000.00 and ₱25,000.00 paid
    When I GET /api/customers/directory
    Then that customer's outstandingBalance is ₱0.00
    And its ledgerState is "Clean"
    And the statement line balance is ₱0.00
```

### Scenario 27: Customer Writes Are Validated Server-Side
```gherkin
Feature: Customer Validation (BR-11)
  Scenario: An invalid customer is rejected and nothing is persisted
    When I POST /api/customers with a blank last name
    Then the response is 400 Bad Request
    And the body contains "Last name is required."
    When I PUT /api/customers/{id} with a blank last name
    Then the response is 400 Bad Request
    And the body contains "Last name is required."
    And the stored customer is unchanged
    When I POST /api/customers with a 200-character last name
    Then the response is 400 Bad Request
    And the body contains a maximum-length message
    And no customer was created
```

### Scenario 28: The Statement of Account Is Downloadable
```gherkin
Feature: Statement of Account (REQ-REC-01)
  Scenario: A customer can download their SOA as a PDF
    When I GET /api/customers/{id}/soa
    Then the response is 200 OK
    And the content type is "application/pdf"
    And the body is a non-empty PDF document
    When I GET /api/customers/999999/soa
    Then the response is 404 Not Found
```

### Scenario 29: The Customer Master Never Invents A Figure
```gherkin
Feature: Customer Master Integrity (DEC-002, AC-C2, AC-C3)
  Scenario: Every displayed figure comes from the API
    Given the directory payload is the only data source
    Then no peso literal, count literal or date literal exists in the feature folder
    And the browser performs no day arithmetic and derives no aging bucket
    And every status chip renders a text label
    And the summary's averageValuePerClient renders as an em dash when it is null
```

### Scenario 30: The Customer Master Excludes Soft-Deleted Customers
```gherkin
Feature: Customer Master Data Integrity (DEC-050)
  Scenario: A soft-deleted customer is absent from the directory and the summary
    Given a customer is soft-deleted
    When I GET /api/customers/directory
    Then that customer does not appear in items
    And summary.totalClients does not count it
```

---

### Scenario 31: The Menu Master Reconciles With the Dashboard (integration) — RELEASE GATE
```gherkin
Feature: Menu & Food Costing Master (REQ-MNU-01/02)
  As the business owner
  I want the menu catalogue's costing to agree with the operations dashboard
  So that the two screens can never tell me different things

  Scenario: The master's aggregates equal the dashboard's catalogue figures
    Given the following dishes exist:
      | Dish  | Course      | Price  | Food Cost |
      | DS-A  | Main Course | 420.00 | 138.60    |
      | DS-B  | Main Course | 380.00 | 104.50    |
      | DS-C  | Dessert     | 150.00 | 36.00     |
      | DS-D  | Appetizer   | 475.00 | 0.00      |
    When I GET /api/menus/master
    Then summary.totalDishes equals 4
    And summary.costedDishes equals 3
    And summary.uncostedDishes equals 1
    And summary.costingCoveragePercentage equals the dashboard's dishCosting.coveragePercentage
    And 100 minus summary.averageFoodCostPercentage equals the dashboard's margin.catalogueMarginPercentage
    And summary.averageFoodCostPercentage equals the catalogue's sum of food cost divided by sum of price
    And summary.marginAlertCount equals the number of dishes whose food cost percentage exceeds the target
    And courses.total equals the number of dishes matching the search and status filters, ignoring the course filter
```

### Scenario 32: A Dish Cannot Be Created Without a Course
```gherkin
Feature: Menu Master Validation (BR-11)
  Scenario: A missing or unknown course is rejected
    When I POST /api/menus with courseId 0
    Then the response is 400 Bad Request
    And the body contains "A course category is required."
    When I POST /api/menus with courseId 999
    Then the response is 400 Bad Request
    And the body contains "The selected course category does not exist."
    And no dish was created
```

### Scenario 33: A Dish Code Cannot Be Duplicated By Case
```gherkin
Feature: Menu Master Business Key (RSK-028)
  Scenario: A case-differing duplicate is a conflict, not a server fault
    Given a dish with the code "DS-A" exists
    When I POST /api/menus with menuId "ds-a"
    Then the response is 409 Conflict
    And the body contains "Menu item 'DS-A' already exists."
    And no second dish was created
```

### Scenario 34: A Deleted Dish Disappears Without Being Destroyed
```gherkin
Feature: Menu Master Soft Delete (DEC-063)
  Scenario: Deleting a dish flags it and it leaves the catalogue
    Given a dish with the code "DS-E" exists
    When I DELETE /api/menus/DS-E
    Then the response is 204 No Content
    And the dish does not appear in GET /api/menus/master
    And the dish does not appear in GET /api/menus
    And the dish's code is still taken, so creating it again is a 409
```

### Scenario 35: The Margin Preview Is Computed Server-Side
```gherkin
Feature: Recipe Pricing (DEC-002)
  Scenario: The preview matches the saved record
    When I POST /api/menus/costing/preview with a price of 380.00 and a food cost of 104.20
    Then the response is 200 OK
    And foodCostPercentage equals 27.42
    And grossMarginPercentage equals 72.58
    And isWithinTarget is true
    And the target equals the configured benchmark
    When I POST /api/menus/costing/preview with a negative price
    Then the response is 400 Bad Request
```

### Scenario 36: The Catalogue Rounds One Way Everywhere
```gherkin
Feature: One Costing Basis (BR-10, DEC-025, RSK-029)
  Scenario: The master card, the costing endpoint and the calculator agree
    Given a dish whose food cost percentage lands on a rounding midpoint
    When I GET /api/menus/master and GET /api/menus/{id}/costing
    Then both publish the same food cost percentage
    And it equals the calculator's value rounded away from zero
    And the dashboard's catalogue margin equals 100 minus that percentage
```

### Scenario 37: The Packages Catalogue Aggregate Is Server-Computed
```gherkin
Feature: Packages & Buffets Catalogue (REQ-PKG-01, DEC-068)
  Scenario: The summary endpoint publishes the catalogue aggregate
    Given the catalogue holds 3 tiers, of which 2 are active and 2 are priced
    And the priced tiers are 500.00, 950.00 and 1450.00 per guest
    And the minimum headcount floors are 50, 80 and 150
    When I GET /api/packages/summary
    Then the response is 200 OK
    And totalPackages is 3
    And activePackages is 2
    And inactivePackages is 1
    And pricedPackages is 2
    And averageAmountPax is 1200.00
    And lowestAmountPax is 950.00
    And highestAmountPax is 1450.00
    And lowestMinPax is 80
    And highestMinPax is 150
```

### Scenario 38: An Unpriced Catalogue Publishes Unknown, Not Zero
```gherkin
Feature: Packages & Buffets Catalogue (REQ-PKG-01, contract rule 6)
  Scenario: No priced tier means an em dash, never a peso zero
    Given every tier in the catalogue has a null AmountPax
    When I GET /api/packages/summary
    Then the response is 200 OK
    And pricedPackages is 0
    And averageAmountPax is null
    And lowestAmountPax is null
    And highestAmountPax is null
    And the client renders an em dash for each of them
    And the client never renders 0.00
```

### Scenario 39: A Package Write Is Validated Before It Reaches the Database
```gherkin
Feature: Package Write Validation (REQ-PKG-01, BR-11, DEC-069)
  Scenario: An invalid tier is rejected and nothing is persisted
    When I POST /api/packages with a blank description
    Then the response is 400 Bad Request
    And the body is { "errors": [ ... ] }
    And the message names the description
    When I POST /api/packages with a negative rate per guest
    Then the response is 400 Bad Request
    And the message names the rate
    When I POST /api/packages with a minimum pax of 0
    Then the response is 400 Bad Request
    And the message names the minimum pax
    And no tier was persisted
    When I POST /api/packages with a valid body
    Then the response is 201 Created
    And the tier appears in GET /api/packages
    And the catalogue summary counts it
```

### Scenario 40: The Catalogue Screen Never Shows An Unsourced Figure
```gherkin
Feature: Packages & Buffets Catalogue UI (REQ-PKG-01, DEC-067)
  Scenario: The reference's unsourced blocks become capability-gap notices
    Given a catalogue holding one active tier priced at 1450.00 per guest
    When I open /packages
    Then the rate card shows the server's 1450.00
    And the screen shows a capability-gap notice where the food-cost and margin band would be
    And the screen shows a capability-gap notice where the course-allocation matrix would be
    And the screen shows a capability-gap notice where the event inclusions would be
    And the screen shows a capability-gap notice where the venue coverage would be
    And no course list, inclusion list, venue list or margin percentage is rendered
    And the export, duplicate and SignalR affordances are absent
```
### Scenario 41: A Course Rule Is Enforced Against A Client Selection
```gherkin
Feature: Course Coverage Rules (REQ-PKG-02, DEC-077, DEC-085)
  Scenario: A ruleset is saved, replaced and read back
    Given a tier priced at 850.00 per guest with a minimum of 80 guests
    When I PUT /api/packages/{id}/course-rules with one rule for the Main Course
      And requiredQuota is 1
      And ruleDescription is "Enforce exactly 1 Main Course selection."
    Then the response is 200 OK
    And GET /api/packages/{id}/course-rules returns that rule
    And the rule's pool holds the non-deleted dishes of the Main Course
    And the pool size is greater than zero
    When I PUT /api/packages/{id}/course-rules with an empty rule set
    Then the response is 200 OK
    And GET /api/packages/{id}/course-rules returns no rule
    And the removed rule's row still exists and is soft-deleted
    And it is not destroyed
```

### Scenario 42: A Ruleset Rejects An Unknown Course And A Duplicate Course
```gherkin
Feature: Course Rule Validation (REQ-PKG-02, BR-11)
  Scenario: An invalid ruleset is rejected and nothing is persisted
    When I PUT /api/packages/{id}/course-rules with a courseId that does not exist
    Then the response is 400 Bad Request
    And the body is { "errors": [ ... ] }
    And the message names the course
    When I PUT /api/packages/{id}/course-rules with the same courseId twice
    Then the response is 400 Bad Request
    And the message says the course is configured more than once
    When I PUT /api/packages/{id}/course-rules with a requiredQuota of 0
    Then the response is 400 Bad Request
    And the message says the quota must be at least 1
    And no rule was persisted
```

### Scenario 43: The Selection Simulator Reports The Ceiling Breach And Does Not Reprice
```gherkin
Feature: Selection Simulator (REQ-PKG-02, DEC-080)
  Scenario: A selection that breaches the cost ceiling warns
    Given a tier priced at 850.00 per guest
    And the configured food-cost benchmark is 35.00
    And the cost ceiling is 297.50 per guest
    When I POST /api/packages/{id}/course-rules/preview with one selection
      And the selected dish's food cost is 91.45
    Then the response is 200 OK
    And the selected food cost per pax is 91.45
    And the effective food cost percentage is the server's value
    And the ceiling is 297.50
    And isCeilingBreached is false
    And isCeilingBreached does not change the price
    When I POST the preview with a selection whose food cost is 320.00
    Then isCeilingBreached is true
    And the ceiling shortfall is 22.50
    And the percentage over the ceiling is the server's value
    And isQuotaCompliant is still true
    And isCompliant is false
    And no price was changed
    And the tier's rate is still 850.00
```

### Scenario 44: The Rule Compliance Verdict Is Exact
```gherkin
Feature: Rule Compliance (REQ-PKG-02, DEC-085)
  Scenario: The quota verdict compares the selected count with the required quota
    Given a rule with a required quota of 1 for the Main Course
    When I preview a selection of one Main Course dish
    Then the Main Course compliance is compliant
    And isQuotaCompliant is true
    And isCompliant is true
    When I preview a selection of two Main Course dishes
    Then the Main Course compliance is not compliant
    And the required quota is 1
    And the selected count is 2
    And isQuotaCompliant is false
    When I preview a selection naming a course the tier has no rule for
    Then isQuotaCompliant is false
    And that course is reported as having no rule
```

### Scenario 49: The Tier Population, The Empty Set And The Resurrection
```gherkin
Feature: Workbench Write Semantics (REQ-PKG-02, DEC-085, DEC-088)
  Scenario: A soft-deleted tier is refused and a null collection is rejected
    Given a tier that has been soft-deleted
    When I GET /api/packages/{id}/course-rules
    Then the response is 404 Not Found
    And the body is { "errors": [ "Package not found." ] }
    When I PUT /api/packages/{id}/course-rules
    Then the response is 404 Not Found
    And no rule was written
    When I PUT /api/packages/{id}/course-rules with a null rules member
    Then the response is 400 Bad Request
    And the message says the rules must be supplied
    And nothing was persisted
    Scenario: An empty set clears the tier and a re-added key resurrects its row
    Given a tier holding one course rule
    When I PUT /api/packages/{id}/course-rules with an empty rule set
    Then the response is 200 OK
    And the tier holds no live rule
    And the removed rule's row still exists and is soft-deleted
    When I PUT /api/packages/{id}/course-rules with that same course again
    Then the response is 200 OK
    And the tier holds exactly one live rule for that course
    And the database holds exactly one row for that natural key
    Scenario: The database refuses a second live row for the same natural key
    When the database is asked to persist a second live rule for the same tier and course
    Then the insert is rejected
```

### Scenario 50: An Uncosted Dish And An Unstated Rate
```gherkin
Feature: Workbench Null Semantics (REQ-PKG-02, DEC-086, DEC-087)
  Scenario: An uncosted selected dish makes the food cost unknown, not zero
    Given a selected dish whose food cost is zero
    When I POST /api/packages/{id}/course-rules/preview
    Then the selected food cost per pax is null
    And isCostKnown is false
    And isCeilingBreached is false
    And the client renders an em dash for it
    And the client never renders 0.00 for it
    And the quota verdict still counts the selected dish
  Scenario: A municipality with no stated rate still yields a complete contract value
    Given a municipality with no additional rate
    When I POST /api/packages/{id}/areas/preview with 100 pax
    Then the additional rate is null
    And the extended total is 0.00
    And the effective rate per pax is the base rate
    And the total contract value is the base total
    And the client renders an em dash for the rate
```

### Scenario 45: The Out-of-Town Matrix And Its Simulator Reconcile
```gherkin
Feature: Location Coverage (REQ-PKG-02, DEC-078, DEC-081)
  Scenario: The matrix, the catalogue and the preview describe one rate
    Given a tier priced at 820.00 per guest with a minimum of 100 guests
    And a municipality with an additional rate of 50.00 per pax
    And a distance of 42 km
    And a dispatch note
    And a logistics cost of 3200.00
    When I attach the municipality to the tier
    Then the response is 201 Created
    And GET /api/packages/{id}/areas shows the municipality
    And the row's rate is 50.00
    And the row's distance is 42
    And the coverage count is 1
    When I POST /api/packages/{id}/areas/preview with 100 pax
    Then the base total is 82000.00
    And the extended total is 5000.00
    And the effective rate per pax is 870.00
    And the total contract value is 87000.00
    And the minimum contract value is 82000.00
```

### Scenario 46: A Municipality With No Logistics Cost Omits The Band
```gherkin
Feature: Location Preview Null Semantics (REQ-PKG-02, DEC-081)
  Scenario: An unknown logistics cost is null, never zero
    Given a municipality with no logistics cost
    When I POST /api/packages/{id}/areas/preview with 100 pax
    Then logisticsCost is null
    And the client renders an em dash for it
    And the client never renders 0.00 for it
    And the gross profit is null
    And the gross profit percentage is null
```

### Scenario 47: The Workbench Screens Never Show An Unsourced Figure
```gherkin
Feature: Package Configuration Workbench UI (REQ-PKG-02, DEC-084)
  Scenario: The references' unsourced blocks become capability-gap notices
    Given a tier with no course rule and no coverage
    When I open /packages/{id}/rules
    Then the screen shows an empty ruleset with the add action
    And no rule, dish, portion or quota is fabricated
    And the screen shows a capability-gap notice where the banqueting amenities would be
    And the screen shows a capability-gap notice where the complimentary staples would be
    And the duplicate, publish and stress-stress-telemetry affordances are absent
    When I open /packages/{id}/locations
    Then the screen shows an empty matrix with the attach action
    And no municipality, rate, distance or logistics cost is fabricated
    And the screen shows a capability-gap notice where the mobile logistics checklist would be
    And the flat out-of-town percentage surcharge is absent
```

### Scenario 48: The Catalogue Inspector Now Offers The Workbenches
```gherkin
Feature: Catalogue Navigation (REQ-PKG-02, DEC-084)
  Scenario: The two capability gaps that closed become the two workbench actions
    Given a catalogue holding one active tier
    When I select the tier in /packages
    Then the inspector shows a Course Rules action
    And the inspector shows a Location Coverage action
    And the course-allocation capability-gap notice is absent
    And the venue-coverage capability-gap notice is absent
    And the notice that remains names the figure that is still unavailable
```

---

## Operational Logging (REQ-OPS-01)

### Scenario 55: Every Endpoint Leaves A Record
```gherkin
Feature: Operational Logging (REQ-OPS-01, BR-21..BR-24)
  Scenario: A successful call is recorded once, with its context
    Given the API is running with request logging enabled
    When I call GET /api/dashboard with a valid session
    Then the log contains exactly one HttpRequestCompleted event for that request
    And the event carries the method, the path, the status code and the elapsed time
    And the event carries the correlation id of the request
```

### Scenario 56: A Failure Is Recorded With Its Stack Trace
```gherkin
Feature: Operational Logging (REQ-OPS-01, BR-21..BR-24)
  Scenario: An unhandled fault is logged at Error and never swallowed
    Given the API is running with request logging enabled
    When an endpoint raises an unhandled exception
    Then the log contains an HttpRequestFailed event at the Error level
    And the event carries the exception type, the message and the stack trace
    And the event carries the same correlation id as the request
    And the request still produces exactly one HttpRequestCompleted event
    And the exception still reaches the host's error handling
```

### Scenario 57: A Credential Is Never Written To The Log
```gherkin
Feature: Operational Logging (REQ-OPS-01, BR-21, DEC-093, DEC-096)
  Scenario: The login exchange leaves no credential on disk
    Given the API is running with request logging enabled
    When I call POST /api/auth/login with a username and a password
    Then the logged request body carries the password property as ***REDACTED***
    And the logged request body carries the username property as ***REDACTED***
    And the logged response body carries the token property as ***REDACTED***
    And the log file contains neither the password, nor the username, nor the issued token
```

### Scenario 58: A Customer Contact Identifier Is Redacted, And The Name Is Not
```gherkin
Feature: Operational Logging (REQ-OPS-01, BR-21, DEC-093, DEC-096)
  Scenario: The record is useful without carrying the contact details
    Given the API is running with request logging enabled
    When I create a customer with an address and two contact numbers
    Then the logged request body carries the address, contact1 and contact2 as ***REDACTED***
    And the logged request body still carries the customer name
    And a booking's customerContact is carried as ***REDACTED***
    And a payment's createdByUser is carried as ***REDACTED***
```

### Scenario 59: A Large Or Binary Response Is Not Copied Into The Log
```gherkin
Feature: Operational Logging (REQ-OPS-01, BR-22, BR-23)
  Scenario: The export is recorded by type and size, and the download is unchanged
    Given the API is running with request logging enabled
    When I download the customer directory export
    Then the log records the response content type and the byte count
    And the log contains no body text for that response
    And the downloaded file is byte-for-byte the file the application produced
    And a body longer than the capture cap is truncated and flagged as truncated
    And a JSON request body larger than the framework's buffering threshold is still bound in full
```

### Scenario 60: The Destination Is Configuration
```gherkin
Feature: Operational Logging (REQ-OPS-01, BR-23, DEC-095)
  Scenario: The record reaches the configured destination
    Given the Serilog section names a file sink
    When the API starts
    Then a rolling JSON log file is written under the logs directory
    And every line is one JSON object
    And the correlation id of an in-request log line is readable from that JSON
    And with the sink list emptied the API still logs to the console
```

---

## Bookings & Events Register & Reservation Workbench (REQ-BKG-05, REQ-BKG-01, added 2026-10-22)

### Scenario 61: The Register And The Dashboard Describe One Population
```gherkin
Feature: Bookings & Events Register (REQ-BKG-05, DEC-099, DEC-111, BR-25, BR-27)
  Scenario: The unfiltered register reconciles with the operations dashboard
    Given three bookings - one confirmed and fully paid, one confirmed with a partial payment and one tentative with no payment
    When I GET /api/bookings with no filter
    And I GET /api/dashboard
    Then the register's summary.outstandingAmount equals the dashboard's receivables.outstandingAmount
    And the register's summary.collectedAmount plus its summary.outstandingAmount equals its summary.netContractedValue
    And every item's netAmountDue equals the net amount due published by GET /api/bookings/{id}/ledger for the same booking
```
---

### Scenario 62: There Is Exactly One Net-Due Figure
```gherkin
Feature: Bookings & Events Register (REQ-BKG-05, DEC-099, BR-25)
  Scenario: The stored snapshot and the shared calculator agree
    Given a tier priced at 820.00 per guest
    And a municipality with an additional rate of 50.00 per pax
    And a discount of 10 percent
    When I create a booking for 120 guests with the extended area applied and the discount applied
    Then the stored Booking.AmountDue equals the calculator's NetAmountDue to the cent
    And the surcharge equals 50.00 multiplied by 120
    And the surcharge is stored in its own frozen column
    And the discount is charged on the gross amount and does not exceed it
    And the register, the ledger, the booking details and the customer statement publish the same net amount due
    And a soft-deleted payment is excluded from every money surface
```
---

### Scenario 63: A Frozen Booking Never Moves
```gherkin
Feature: Bookings & Events Register (REQ-BKG-05, DEC-100, BR-04, BR-26)
  Scenario: A later catalogue change does not alter a stored booking
    Given a confirmed booking of 120 guests with a municipality surcharge applied
    When the municipality's additional rate is changed
    And the tier's rate per guest is changed
    Then the booking's net amount due is unchanged
    And the register still publishes the frozen figure
    And a new booking quoted after the change uses the new rate
```
---

### Scenario 64: The Register Is Filtered, Paged And Faceted
```gherkin
Feature: Bookings & Events Register (REQ-BKG-05, BR-25, BR-28, BR-32)
  Scenario: The filters narrow the population and the facets ignore their own dimension
    Given bookings across three tiers, three venues, three statuses and three payment states
    When I GET /api/bookings with a tier filter and a date range
    Then the totals describe the whole filtered population and not the page
    And the tier facet counts are computed with the tier filter removed
    And the venue facet counts are computed with the venue filter removed
    And an unknown status name is rejected with 400 and an errors array
```
---

### Scenario 65: The Payment States Are Exclusive And Exhaustive
```gherkin
Feature: Bookings & Events Register (REQ-BKG-05, BR-28, DEC-041)
  Scenario: Every booking has exactly one payment state
    Given a booking with no payment whose event date has passed
    And a booking with no payment whose event date is in the future
    And a booking with a partial payment whose event date is in the future
    And a booking that is fully paid
    And a booking that has been overpaid
    And a zero-value booking with no payment
    Then the first booking's payment state is AwaitingDeposit and it is overdue
    And the second booking's payment state is AwaitingDeposit and it is not overdue
    And the third booking's payment state is PartiallyPaid
    And the fourth booking's payment state is Settled
    And the fifth booking's payment state is Settled
    And the sixth booking's payment state is Settled
    And the three state counts sum to the population's booking count
```
---

### Scenario 66: The Quote Prices The Booking And Persists Nothing
```gherkin
Feature: Reservation Workbench (REQ-BKG-01, BR-31, BR-25, BR-29, BR-30)
  Scenario: The workbench's ledger is the server's quote
    Given a tier priced at 820.00 per guest with a minimum of 100 guests
    And a municipality with an additional rate of 50.00 per pax
    When I POST /api/bookings/quote for 120 guests with the extended area applied
    Then the quote publishes the base amount, the surcharge, the gross amount and the net amount due
    And no booking row was created
    And the deposit required is not stated because no deposit percentage is configured
    And the capacity is not stated because no capacity is configured
    And the tier-active check is the only blocking check
```
---

### Scenario 67: A Draft And A Confirmed Booking
```gherkin
Feature: Reservation Workbench (REQ-BKG-01, BR-33, DEC-107)
  Scenario: Saving a draft and confirming a booking are two states of one create
    When I POST /api/bookings with confirm false
    Then the response is 201 Created
    And the booking's status is Pending
    And no payment row was created
    When I POST /api/bookings with confirm true and an initial payment
    Then the response is 201 Created
    And the booking's status is Confirmed
    And the payment row and the booking were committed together
    And the ledger shows the payment
```
---

### Scenario 68: An Overpayment Is Refused And Nothing Is Written
```gherkin
Feature: Reservation Workbench (REQ-BKG-01, BR-05, BR-33, DEC-118)
  Scenario: An initial payment larger than the net amount due is rejected
    When I POST /api/bookings with an initial payment larger than the net amount due
    Then the response is 400 Bad Request with an errors array
    And no booking row was created
    And no payment row was created
    When I POST /api/payments for a booking with the same overpayment
    Then the response is 400 Bad Request with an errors array
    And no payment row was created
    And the booking's status is unchanged
    And the booking's cached AmountPaid is unchanged
    And the ledger still shows the same remaining balance
```
---

### Scenario 71: The Money Chain Rounds Once
```gherkin
Feature: Bookings & Events Register (REQ-BKG-05, BR-25, BR-26, DEC-115, DEC-116)
  Scenario: A percentage discount that needs a third decimal still reconciles
    Given a tier priced at 100.00 per guest for 1 guest
    And a discount of 0.005 percent
    When I create a booking with that discount
    Then the stored Booking.AmountDue equals the calculator's NetAmountDue to the cent
    And the published discount amount is rounded to two decimals away from the midpoint
    And the register publishes the same figure as the ledger
```
---

### Scenario 72: The Surcharge Is Stored, Not Inferred
```gherkin
Feature: Bookings & Events Register (REQ-BKG-05, BR-26, BR-04, DEC-116)
  Scenario: The frozen surcharge is a column of its own
    Given a confirmed booking of 120 guests with a 50.00 per pax surcharge applied
    Then the booking's stored extended area amount is 6000.00
    When the booking's apply-extended-area flag is changed
    Then the published net amount due is unchanged
    When the municipality's rate is changed
    Then the published net amount due is still unchanged
```
---

### Scenario 69: The Register Never Shows An Unsourced Figure
```gherkin
Feature: Bookings & Events Register & Reservation Workbench (REQ-BKG-05, REQ-BKG-01, DEC-103, DEC-104, DEC-105, DEC-108)
  Scenario: The gaps are named and nothing is fabricated
    When I open the Bookings & Events Register
    Then the batch BEO action is inactive and explains itself
    And no BEO production state is rendered
    When I open the Reservation Workbench
    Then the per-event dish selection renders a capability-gap notice
    And the chef batching yield renders a capability-gap notice naming REQ-MNU-03
    And the package tier inclusions render a capability-gap notice
    And the tier's margin is labelled a planning estimate
    And no peso figure, percentage or count appears that the API did not publish
```
---

### Scenario 70: The Workplan And The Contract Describe The As-Built System
```gherkin
Feature: Governance (REQ-BKG-05, AC-BKG-39, AC-BKG-40, AC-BKG-41)
  Scenario: The structural record matches the delivered code
    Given the delivery of the Bookings & Events Register and the Reservation Workbench
    Then the project structure workplan lists the new frontend feature folder, the two routes and the client endpoints
    And the workplan lists the new backend use cases, the repository projection and the migration
    And the migration is purely additive - three nullable columns and no data change
    And the Application project references neither Entity Framework Core nor Identity
```
### Scenario 73: The Printed Contract Itemises What It Charges
```gherkin
Feature: Bookings & Events Register (REQ-BKG-05, BR-26, BR-02, DEC-113, DEC-121, DEC-122)
  Scenario: The client can see how the contract value was derived
    Given a confirmed booking of 120 guests at 820.00 per guest with a 50.00 per pax out-of-town surcharge and a 10 percent discount
    When I download the printable contract
    Then the service details show the package line and the out-of-town surcharge line with its rate, its pax and its amount
    And the totals show the total charges, the discount with its frozen code, the contract amount, the payments received and the remaining balance
    And the contract amount equals the shared calculator's net amount due
    And the dispatch schedule is printed once, in the information block, when both times are stated
    And no line reads 0.00 for a charge that was never applied
```
---

### Scenario 74: No Read Model Publishes A Field Nobody Fills
```gherkin
Feature: Governance (REQ-BKG-05, AC-BKG-48)
  Scenario: A declared DTO member is always projected
    Given the booking details, the register item and the quote read models
    When every public member of each read model is checked against the projection that populates it
    Then every member is assigned by that projection
    And a member added to a read model without a projection fails the guard
```
---

## Booking Dish Selection & the Menu-Change Window (REQ-BKG-06, added 2026-10-22)

### Scenario 75: A Booking Carries Its Own Dish Selection
```gherkin
Feature: Booking Dish Selection (REQ-BKG-06, BR-35, DEC-127)
  Scenario: The chosen dishes are recorded on the booking
    Given a confirmed booking on the "Premier" tier whose Beef course requires one dish
    When I choose "Beef Calderita" for the Beef course and save the selection
    Then the selection is recorded on the booking
    And GET /api/bookings/{id}/menus returns it
    And the other courses still report their required quota as unmet
```
---

### Scenario 76: The Selection Is Validated By The Tier's Own Rules
```gherkin
Feature: Booking Dish Selection (REQ-BKG-06, BR-15, BR-35, DEC-130)
  Scenario: A selection that breaks the quota is refused
    Given a tier whose Beef course requires two dishes
    When I save a selection of one dish for the Beef course
    Then the response is 400 Bad Request
    And the message names the Beef course and the quota
    And nothing was written
    When I save a selection of a dish that is not in the course's pool
    Then the response is 400 Bad Request
    And nothing was written
```
---

### Scenario 77: A Selection Never Moves The Money
```gherkin
Feature: Booking Dish Selection (REQ-BKG-06, BR-37, BR-04, DEC-129)
  Scenario: Changing the dishes leaves the contract value untouched
    Given a confirmed booking with a deposit taken
    When I replace its dish selection
    Then every money field of the booking is unchanged
    And the ledger's contract amount is unchanged
    And the register still publishes the same net amount due
```
---

### Scenario 78: The Menu-Change Window Is Configuration
```gherkin
Feature: Booking Dish Selection (REQ-BKG-06, BR-36, DEC-128)
  Scenario: The window comes from the configuration and its deadline is always published
    Given the window is configured as 7 days
    Then the published lock date is 7 days before the event date
    And the published days figure is 7
    When the window is not configured
    Then the published lock date is the event date
    And the published days figure is null
```
---

### Scenario 79: A Write After The Lock Is Refused
```gherkin
Feature: Booking Dish Selection (REQ-BKG-06, BR-36, DEC-128)
  Scenario: The kitchen's production commitment is honoured
    Given a booking whose event is inside the configured menu-change window
    When I replace its dish selection
    Then the response is 200 OK
    And the selection is stored
    When the booking's event passes its lock date
    And I replace its dish selection
    Then the response is 400 Bad Request
    And the message names the lock date
    And nothing was written
```
---

### Scenario 80: The Standard Run Is The Default Menu
```gherkin
Feature: Booking Dish Selection (REQ-BKG-06, BR-35, BR-19, DEC-131)
  Scenario: A booking with no selection is served the tier's standard run
    Given a confirmed booking with no dish selection
    Then the register publishes the tier's standard-run estimate for it
    And the card's label says it is the tier's estimate
    And saving an explicitly empty selection clears any selection the booking had
    And an absent selections member is rejected with 400
```
---

### Scenario 81: The Selection Is Replaced, Never Patched
```gherkin
Feature: Booking Dish Selection (REQ-BKG-06, BR-35, DEC-134)
  Scenario: A replaced dish is soft-deleted and re-chosen resurrects it
    Given a booking whose selection holds "Beef Calderita"
    When I replace the selection with "Beef Ribeye"
    Then the Beef Calderita row is soft-deleted and the Beef Ribeye row is active
    When I choose "Beef Calderita" again
    Then its row is resurrected and no second row exists for that dish
```
---

### Scenario 82: The Menu Editor Is Reachable And Honest
```gherkin
Feature: Booking Dish Selection (REQ-BKG-06, DEC-133)
  Scenario: The dossier card offers the menu selection and nothing it cannot deliver
    Given the Bookings & Events Register
    Then each dossier card carries a "Menu selection" action
    And it opens the editor for that booking
    And the reference's "Manage Event" action is not rendered
    And a locked booking's editor is read-only and says when it locks
```
---
---

## Bookings & Events Operations - Add-Ons, Automatic Discounts and the Extended Location (REQ-BKG-07 / REQ-BKG-08, added 2026-10-22)

### Scenario 83: The Headcount Bands Are Exactly As The Owner Stated
```gherkin
Feature: Automatic Headcount Discounts (REQ-BKG-08, BR-38, DEC-136, DEC-137)
  Scenario: Each band earns its own per-head discount and the gap earns nothing
    Given a booking of 89 heads
    Then the discount is 890.00 and the discount source is HeadcountBand
    Given a booking of 95 heads
    Then the discount is zero and the discount source is None
    Given a booking of 120 heads
    Then the discount is 2400.00 and the discount source is HeadcountBand
    Given a booking of 1000 heads
    Then the discount is zero and the discount source is None
```
---

### Scenario 84: One Discount Applies
```gherkin
Feature: Automatic Headcount Discounts (REQ-BKG-08, BR-38, DEC-138)
  Scenario: An explicit code replaces the automatic band discount
    Given a booking of 120 heads and a valid discount code
    When the booking is priced
    Then the discount is the code's amount and the discount source is Code
    And the band discount is not applied on top
```
---

### Scenario 85: The Out-Of-Town Amount Is Limited To Two Tier Types
```gherkin
Feature: Extended Location (REQ-BKG-06, BR-40, DEC-141)
  Scenario: Only Regular and Premier tiers carry the out-of-town amount
    Given a Regular tier and a municipality with a rate
    When I quote the booking with the extended area applied
    Then the surcharge is applied
    Given a Wedding tier
    When I quote the booking with the extended area applied
    Then the response is 400 Bad Request
    And the workbench does not offer the area filter for that tier type
```
---

### Scenario 86: The Add-On Master And The Booking's Lines
```gherkin
Feature: Booking Add-Ons (REQ-BKG-07, BR-39, DEC-139, DEC-140)
  Scenario: The lines freeze what was agreed
    Given an active add-on in the master with a description, an amount and a unit
    When I attach it to a booking with a confirmed amount
    Then the line freezes the description, the amount and the unit
    When the catalogue's price is changed afterwards
    Then the stored line is unchanged
    And the register still publishes the booking's original net amount due
```
---

### Scenario 87: The Add-On Total Joins The Gross
```gherkin
Feature: Booking Add-Ons (REQ-BKG-07, BR-41, DEC-135, BR-25)
  Scenario: The gross is base plus surcharge plus add-ons
    Given a tier at 820.00 per guest for 120 guests
    And a municipality rate of 50.00 per pax applied
    And an add-on line of 1500.00
    When the booking is priced
    Then the gross is 104400.00 plus 1500.00
    And the stored AmountDue equals the calculator's NetAmountDue to the cent
```
---

### Scenario 88: The Add-Ons Follow The Same Menu-Change Window
```gherkin
Feature: Booking Add-Ons (REQ-BKG-07, BR-36, BR-39)
  Scenario: A locked booking takes no add-on change
    Given a booking whose event is inside the configured menu-change window
    When I replace its add-ons
    Then the response is 200 OK
    When the booking's event passes its lock date
    And I replace its add-ons
    Then the response is 400 Bad Request
    And the message names the lock date
    And nothing was written
```
---
---

## The Menu-Selection Screen (REQ-BKG-06 / 07 / 08, added 2026-10-22)

### Scenario 89: The Tax Is A VAT-Inclusive Disclosure And The Rate Is Configuration
```gherkin
Feature: The Contract Ledger (REQ-BKG-06, BR-42, DEC-143)
  Scenario: The VAT is disclosed within the total, and only when it is configured
    Given the VAT percentage is configured as 12
    When a booking's net amount due is 120300.00
    Then the published included VAT amount is 12889.29
    And the net amount due is unchanged
    When the VAT percentage is not configured
    Then the published included VAT amount is null
    And no VAT line is rendered
```
---

### Scenario 90: The Batch Mass Is Derived From The Portion And The Headcount
```gherkin
Feature: The Commissary Batch Yield (REQ-BKG-06, DEC-145)
  Scenario: The mass the kitchen produces is computable
    Given a course rule that states a portion of 180 grams per guest
    And a booking of 120 guests
    Then the published batch mass is 21.6 kilograms
    And it is labelled a planning figure
    Given a course rule that states no portion
    Then the published batch mass is null
```
---

### Scenario 91: The Add-On's Pricing Basis Is An Explicit Catalogue Column
```gherkin
Feature: Booking Add-Ons (REQ-BKG-07, BR-39, DEC-158)
  Scenario: A per-guest add-on multiplies by the headcount and a flat one does not
    Given an active add-on priced 150.00 whose pricing basis is PerGuest
    When it is attached to a booking of 120 guests
    Then the frozen line total is 18000.00
    Given an active add-on priced 1500.00 whose pricing basis is Flat
    When it is attached to the same booking
    Then the frozen line total is 1500.00
    Given an active add-on whose pricing basis is not stated
    When it is attached to the same booking
    Then the frozen line total is its catalogue amount
    And the unit of measure never decides the basis
```
---

### Scenario 92: The Chef Prep Instruction Belongs To The Event
```gherkin
Feature: Booking Dish Selection (REQ-BKG-06, DEC-146)
  Scenario: The instruction is stored per booking, per dish
    Given a booking whose Beef course has the dish "Beef Caldereta" selected
    When I enter the prep instruction "Walnuts on side for allergy table" and save
    Then the instruction is stored on the booking's selection
    And the catalogue's dish is unchanged
    And another booking of the same dish carries no instruction
```
---

### Scenario 93: The Kitchen Production Order Prints The Selection
```gherkin
Feature: The Kitchen Banquet Event Order (REQ-BKG-06, REQ-REP-02, DEC-148, DEC-159)
  Scenario: The BEO is generated from the booking's selection
    Given a booking whose selection holds three dishes
    And one of them carries a prep instruction
    And the booking carries two add-on lines
    When I GET /api/bookings/{id}/beo
    Then the response is 200 with a PDF
    And the document lists the three chosen dishes
    And the document carries the prep instruction
    And the document lists the add-on lines
    Given a booking with no selection
    When I GET /api/bookings/{id}/beo
    Then the document lists the tier's course rules
    And it states that no dish selection has been recorded
    And it lists no dish
```
---

---

## The Plan Review's Corrections (REQ-BKG-06 / 07 / 08, added 2026-10-22)

### Scenario 94: The Add-On Total Reaches The Authoritative Calculator
```gherkin
Feature: Booking Add-Ons (REQ-BKG-07, DEC-151, DEC-099, BR-25)
  Scenario: The stored snapshot and the calculator agree with an add-on on the booking
    Given a tier at 820.00 per guest for 120 guests
    And a confirmed add-on line of 1500.00
    When the booking is priced
    Then the booking's frozen AddonsTotal is 1500.00
    And the stored AmountDue equals the calculator's NetAmountDue to the cent
    And the register's published net amount due is the same figure
    When the booking is read again
    Then the published net amount due is unchanged
```
---

### Scenario 95: An Add-On Write Re-Freezes The Money
```gherkin
Feature: Booking Add-Ons (REQ-BKG-07, BR-43, DEC-152)
  Scenario: Adding an add-on moves the contract value and re-caps the discount
    Given a confirmed booking of 120 heads with a 2400.00 band discount and a 36120.00 deposit
    When I attach an add-on line of 1500.00
    Then the booking's Subtotal and AmountDue are re-frozen
    And the discount amount is still 2400.00
    And the deposit amount is unchanged
    When I remove an add-on so that the new net amount due falls below the amount collected
    Then the response is 400 Bad Request
    And nothing was written
```
---

### Scenario 96: The Quote And The Create Cannot Diverge
```gherkin
Feature: The Booking Workbench (REQ-BKG-06 / 07, BR-31, DEC-153)
  Scenario: A quote that carries a selection and add-ons agrees with the created booking
    Given a quote request that carries a selection and one add-on
    Then the quote's gross, discount and net amount due are published
    When I create the booking with the same selection and add-on
    Then the created booking's net amount due equals the quote's
```
---

### Scenario 97: The Surcharge Tier Test Is Pinned
```gherkin
Feature: Extended Location (REQ-BKG-06, BR-40, DEC-156)
  Scenario: The tier test is equality on the type column only
    Given a tier whose PackageType is "Premier"
    When I quote the booking with the extended area applied
    Then the surcharge is applied
    Given a tier whose PackageType is "Premier Buffet"
    When I quote the booking with the extended area applied
    Then the response is 400 Bad Request
    And the message names the two eligible tier types
    Given a tier whose description reads "2026 Premier Package B" but whose type is "Regular"
    When I quote the booking with the extended area applied
    Then the surcharge is applied
```
---

### Scenario 98: The Area Filter Is Answered By The Server
```gherkin
Feature: The Booking Workbench (REQ-BKG-06, DEC-157)
  Scenario: The tiers are filtered by the selected area
    Given a municipality covered by some tiers and not by others
    When I GET /api/packages with that area
    Then only the tiers covering that area are returned
```
---

### Scenario 99: The Client Hardcodes No Bound And No Rate
```gherkin
Feature: The Menu Editor (REQ-BKG-06, DEC-160)
  Scenario: The band bounds and the VAT rate come from the server
    Given a booking priced by the headcount band
    Then the quote publishes the band's label and its rate per head
    And the VAT rate is published when it is configured
    And the feature folder contains no four-digit or percentage literal
```
---

### Scenario 100: The Package Code Is Generated Once and Frozen
```gherkin
Feature: The Packages & Buffets Catalogue (DEC-162)
  Scenario: A new tier receives its per-type code
    Given no live Regular tier numbered 01 exists
    When I create a Regular tier
    Then the server assigns PKG-REG-01
    And a second Regular tier receives PKG-REG-02
    And editing the first tier's type or name leaves PKG-REG-01 unchanged
```
---

### Scenario 101: The Extended Location Amount Is Regular/Premier Only
```gherkin
Feature: The Packages & Buffets Catalogue (DEC-163)
  Scenario: A non-eligible type may not carry the amount
    Given a package payload typed Wedding with a non-null extendedLocationAmount
    When I POST it to /api/packages
    Then the response is 400
    And no tier is written
    And a Regular tier with the same amount is accepted
```
---

### Scenario 102: The Tier Amount Joins the Municipality Rate
```gherkin
Feature: The Reservation Workbench (DEC-164)
  Scenario: The booking surcharge combines both per-guest rates
    Given a Regular tier whose extendedLocationAmount is 25.00
    And a municipality whose AdditionalRate is 50.00
    When I quote a 100-head booking with applyExtendedAmount on
    Then the quote's extendedAreaAmount is 7,500.00
    And with no municipality selected it is 2,500.00
    And with applyExtendedAmount off it is 0.00
```
---

### Scenario 103: Every Money Input Is Formatted
```gherkin
Feature: The shared CurrencyInput (DEC-166)
  Scenario: A currency field formats without a symbol
    Given the value 1425 in a money field
    When the field is not focused
    Then it shows 1,425.00
    And while it is edited it shows the raw value
    And a count field such as the headcount remains a plain number
```
---

### Scenario 104: The Department Foreign Keys
```gherkin
Feature: The Department master (DEC-165)
  Scenario: An add-on and a dish may name a department
    Given a Department row
    When an AddonDetail or a Menu references it
    Then the FK is enforced
    And a missing department leaves the nullable column null
    And the add-on master publishes deptId and deptName
```
---

### Scenario 105: The Add-on Master Is Administered From Settings
```gherkin
Feature: Settings -> Addons (DEC-168)
  Scenario: The master is listed and edited
    Given the add-on master holds an active and an inactive add-on
    When I open /settings/addons
    Then both add-ons are listed with their category, department, basis and unit price
    And I can open one in the form and save a changed description
    And the list shows the saved value after the server confirms
```
---

### Scenario 106: The Add-on Write Is Validated Before Persistence
```gherkin
Feature: Settings -> Addons (DEC-168)
  Scenario: A rejected payload persists nothing
    Given a blank description and a negative unit price
    When I POST it to /api/addons
    Then the response is 400 with the server's messages
    And no add-on is written
    And an unknown category or department is also a 400
```
---

### Scenario 107: The Delete Is a Soft Delete
```gherkin
Feature: Settings -> Addons (DEC-168)
  Scenario: A deleted add-on disappears but a booking keeps its frozen line
    Given an add-on that a booking already carries
    When I DELETE it from the master
    Then the master and the booking picker no longer offer it
    And the booking's frozen line is unchanged
    And a second DELETE returns 404
```
---

### Scenario 108: The Booking Picker Stays Active-Only
```gherkin
Feature: Settings -> Addons (DEC-168)
  Scenario: An inactive add-on is excluded from the picker
    Given an inactive add-on
    Then GET /api/addons?headcount=100 excludes it
    And GET /api/addons/manage includes it so it can be reactivated
```
---

### Scenario 109: The Booking Menu Add-On Picker Is Lazy And Reconciles The Server Total
```gherkin
Feature: Booking Menu Selection add-on picker (REQ-BKG-07, DEC-170, AC-BKG-88)
  Scenario: Choose add-ons in a category-filtered modal and save the server-recomputed total
    Given I open a booking's Menu Selection page
    Then the active add-on master and categories have not been requested
    When I open "Choose add-ons"
    Then the active master is requested with the booking's headcount
    And the category choices are loaded
    When I filter to a category
    Then the active master request includes that category id
    When I select an add-on
    Then it appears in the selected-addons summary
    And the full catalogue is not rendered outside the modal
    And the change remains a draft until I save
    When I save the selection
    Then the existing add-on replacement endpoint re-freezes the booking's money
    And the page reloads the booking details
    And the contract ledger displays the server-returned total contract value
    And the browser has not calculated an authoritative total
```
---

### Scenario 110: The Reservation Workbench Publishes A Tier Rate Before A Quote
```gherkin
Feature: Reservation Workbench rate per head (REQ-BKG-01, AC-BKG-89..91, AC-BKG-98)
  Scenario: Show the catalogue rate while the booking is incomplete and repricing
    Given the active tier catalogue publishes a selected tier's amountPax
    And the booking is not yet complete enough to request a quote
    When I select that tier
    Then the Rate per head cell immediately displays its catalogue amount
    And the Base value remains in its awaiting-quote state
    When the server quote resolves a rate for that tier
    And I change the headcount while the next quote is recalculating
    Then the last resolved rate for that same tier remains populated with a polite recalculating cue
    And the Base value and ledger do not display a stale amount
    When I select a different tier while its quote is not yet available
    Then the Rate per head cell immediately displays the new tier's catalogue amount
    And it never carries the previous tier's rate

  Scenario: Preserve the null-versus-zero distinction
    Given a selected tier whose catalogue amountPax is null
    When the quote is absent or reports zero for that tier
    Then Rate per head reads "not stated"
    And it never reads ₱0.00
    Given a selected tier whose catalogue amountPax is zero
    Then Rate per head reads ₱0.00

  Scenario: A current quote corrects a stale catalogue rate once
    Given a current quote for the selected tier differs from the catalogue amountPax
    When the quote is displayed
    Then the quoted rate is the only rate shown
    And a quiet catalogue-refresh note is shown
    And the tier-catalogue query is invalidated once
```
---

### Scenario 111: The Reservation Workbench Enforces Type, Area And Tier In Order
```gherkin
Feature: Reservation Workbench tier cascade (REQ-BKG-01, AC-BKG-92..97)
  Scenario: The tier cannot be selected before its required gates
    Given the package type is blank
    Then the required type selector says "Select a package type"
    And the empty Buffet tier picker is disabled with a type-first hint
    When I select "Regular"
    Then the required Area filter appears above Buffet tier
    And Buffet tier stays disabled until I select an area
    When I select an area
    Then the client requests the package catalogue once with activeOnly=true and that areaId
    And only server-returned tiers whose trimmed type exactly matches "Regular" are selectable
    And event Municipality is prefilled with that area when the Municipality control is shown
    And the Area prefill does not turn on the out-of-town surcharge switch

  Scenario: Type and area changes clear a stale tier immediately
    Given I selected a Regular tier for an area
    When I change the area
    Then the selected tier is cleared in the same state update
    And no tier from the previous area's placeholder results can be selected
    When I change the type from Regular to Premier
    Then the tier is cleared and the selected area is retained
    When I change the type to Wedding
    Then both the tier and area are cleared
    And the Area filter is hidden
    And only active Wedding tiers from the unfiltered catalogue are offered

  Scenario: The Area prefill remains editable and clearing the filter does not reverse-sync
    Given a non-empty Area filter has prefilled the event Municipality
    When I choose a different event Municipality
    Then my Municipality choice remains editable and is retained
    When I clear the Area filter
    Then the tier is cleared
    And the event Municipality value is not cleared by reverse synchronization

  Scenario: A whitespace-padded stored type still matches after trimming
    Given the catalogue contains an active tier whose packageType is "Regular "
    When I select the "Regular" package type
    Then that tier remains in the filtered options
    And matching is exact and case-sensitive after trimming

  Scenario: An empty area/type result explains recovery and save is gated
    Given a selected eligible type and area have no matching active tiers
    Then the tier picker is enabled with no options
    And the explanation names both the type and area
    And I can clear the area or the type
    When the type/area gate is unsatisfied
    Then Save draft and Confirm booking are blocked with the specified validation messages
    And the create payload contains no packageType or areaFilterId member
    And the event Municipality control remains independent and obeys AC-BKG-72
```
---
