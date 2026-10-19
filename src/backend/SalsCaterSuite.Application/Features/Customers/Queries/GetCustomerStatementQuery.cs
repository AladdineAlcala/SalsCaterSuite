using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Customers.DTOs;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Customers.Queries;

/// <summary>GET /api/customers/{id}/statement — full customer statement of account.</summary>
public class GetCustomerStatementQuery
{
    private readonly ICustomerRepository _customers;

    public GetCustomerStatementQuery(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<Result<CustomerStatementDto>> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetWithStatementDetailsAsync(id, cancellationToken);

        if (customer is null)
        {
            return Result<CustomerStatementDto>.Failure("Customer not found.");
        }

        var activeBookings = customer.Bookings!.Where(b => b.Status != BookingStatus.Cancelled).ToList();

        var lines = activeBookings.Select(b =>
        {
            var paid = b.Payments.Sum(p => p.AmountPaid);
            var amount = b.PackageAmount;
            return new CustomerStatementLineDto
            {
                TransactionId = b.TransactionId,
                EventDate = b.EventDate.ToDateTime(TimeOnly.MinValue),
                Occasion = b.Occasion ?? "Event",
                Amount = amount,
                Paid = paid,
                Balance = amount - paid,
                Status = b.Status is BookingStatus.Completed or BookingStatus.InProgress ? "Served" : "Upcoming"
            };
        }).OrderByDescending(l => l.EventDate).ToList();

        var statement = new CustomerStatementDto
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.FullName,
            Contact = customer.Contact1 ?? customer.Contact2,
            Address = customer.Address,
            TotalBilled = lines.Sum(l => l.Amount),
            TotalPaid = lines.Sum(l => l.Paid),
            OutstandingBalance = lines.Sum(l => l.Balance),
            Transactions = lines
        };

        return Result<CustomerStatementDto>.Success(statement);
    }
}
