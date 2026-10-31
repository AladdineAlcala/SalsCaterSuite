---
applyTo: "**/SalsCaterSuite.WebApi/Controllers/**/*.cs, **/SalsCaterSuite.Application/Features/**/DTOs/**/*.cs, **/SalsCaterSuite.Application/Common/Models/**/*.cs"
---
# API Contract Rules

- Validation failures return: `{ "errors": [ "..." ] }`
- Use consistent request/response DTOs across all endpoints.
- Apply server-side validation and authorization; enforce resource-level access checks.
- Avoid mass assignment of protected financial fields or role assignments.
- Use pagination, filtering, and sorting for large collections; avoid N+1 queries and uncontrolled entity graph serialization.
- Document API contracts and breaking changes.