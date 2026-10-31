---
applyTo: "**/Features/**/DTOs/**/*.cs"
---
# DTO File Organization Rules

1. Each DTO must be defined in a separate `.cs` file.
2. Place the file according to the feature and type:
   - Request DTO → `Projectname.Application/Features/{FeatureName}/DTOs/Request/`
   - Response DTO → `Projectname.Application/Features/{FeatureName}/DTOs/Response/`
3. Naming convention:
   - Request DTOs end with `Request` (e.g., `CreateBookingRequest.cs`)
   - Response DTOs end with `Response` (e.g., `CreateBookingResponse.cs`)