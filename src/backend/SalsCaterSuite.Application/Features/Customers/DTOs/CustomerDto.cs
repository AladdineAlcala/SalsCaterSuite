namespace SalsCaterSuite.Application.Features.Customers.DTOs;

public class CustomerDto
{
    public int CustomerId { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? Middle { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact1 { get; set; }
    public string? Contact2 { get; set; }
    public string? Company { get; set; }
    public DateTime? DateRegistered { get; set; }
    public int TotalBookings { get; set; }
    public decimal TotalReceivables { get; set; }
}

public class CustomerStatementDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string? Address { get; set; }
    public decimal TotalBilled { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal OutstandingBalance { get; set; }
    public List<CustomerStatementLineDto> Transactions { get; set; } = new();
}

public class CustomerStatementLineDto
{
    public int TransactionId { get; set; }
    public DateTime? EventDate { get; set; }
    public string Occasion { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Request body for creating or updating a customer.</summary>
public record CreateCustomerRequestDto(
    string LastName,
    string FirstName,
    string? Middle,
    string? Address,
    string? Contact1,
    string? Contact2,
    string? Company);

