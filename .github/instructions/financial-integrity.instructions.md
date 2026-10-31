---
applyTo: "**/SalsCaterSuite.Domain/Entities/**/*.cs, **/SalsCaterSuite.Infrastructure/Persistence/Configurations/**/*.cs, **/Features/**/Payments/**/*.cs, **/Features/**/Bookings/**/*.cs"
---
# Financial Integrity Rules

- Currency fields use `decimal`; never `float` or `double`.
- In EF Core configurations, set explicit SQL decimal precision (e.g., `HasPrecision(18, 2)`).
- Do not invent financial formulas, tax rates, cancellation fees, service charges, or payment allocation rules — use only approved business rules.
- Confirmed bookings must not silently change when current menu prices, packages, or discounts are edited (preserve historical transaction meaning).
- Multi-record financial operations require database transactions and concurrency control.
- Enforce valid state transitions, duplicate-payment protection, idempotency where required, and auditability.