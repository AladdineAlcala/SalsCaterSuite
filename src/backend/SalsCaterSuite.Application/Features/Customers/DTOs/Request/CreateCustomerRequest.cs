namespace SalsCaterSuite.Application.Features.Customers.DTOs.Request;

/// <summary>Request body for creating or updating a customer.</summary>
public record CreateCustomerRequest(
    string LastName,
    string FirstName,
    string? Middle,
    string? Address,
    string? Contact1,
    string? Contact2,
    string? Company);
