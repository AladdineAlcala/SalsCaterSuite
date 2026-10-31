---
applyTo: "**/*.cs"
---
# C# Conventions

- Target framework: net10.0, C# with nullable reference types enabled.
- Unit tests use xUnit.
- API integration tests use `WebApplicationFactory`.
- Never claim a test passed without executed evidence — copy counts from an actual run.