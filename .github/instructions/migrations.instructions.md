---
applyTo: "**/SalsCaterSuite.Infrastructure/Persistence/Migrations/**/*.cs, **/SalsCaterSuite.Infrastructure/Persistence/Configurations/**/*.cs"
---
# Migration Safety Rules (DEC-020)

- Prefer additive migrations.
- Review generated migration scripts before execution.
- Do not apply a migration to an existing or production database without explicit approval.
- Document rollback and data-validation requirements.
- Never rename, delete, restore over, truncate, or alter production databases without explicit human authorization.
- Never assume a backup is valid until its existence and restore have been verified.