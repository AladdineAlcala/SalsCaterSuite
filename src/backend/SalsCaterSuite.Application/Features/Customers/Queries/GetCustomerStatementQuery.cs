using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Features.Customers.DTOs.Request;
using SalsCaterSuite.Application.Features.Customers.DTOs.Response;
using SalsCaterSuite.Application.Features.Customers.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Customers.Queries;

/// <summary>GET /api/customers/{id}/statement — full customer statement of account.</summary>
/// <remarks>
/// The statement is corrected per spec §7.1 / §7.2 (F11 / RSK-009):
/// <list type="bullet">
/// <item><description>The line's <c>Amount</c> is the booking's <b>net amount due</b>, computed
/// through the single shared <see cref="BookingAmountDueCalculator"/> overload — not the
/// frozen <c>Booking.PackageAmount</c> column and not the persisted
/// <c>Booking.AmountDue</c> column.</description></item>
/// <item><description>The line's <c>Balance</c> is <c>max(0, net amount due − amount paid)</c>
/// (BR-03) and is never negative.</description></item>
/// <item><description>The line's <c>Status</c> is the approved BR-10 booking lifecycle name;
/// the retired "Served" / "Upcoming" vocabulary is not used.</description></item>
/// <item><description>Refunds are reported separately and never reduce the balance
/// (DEC-013).</description></item>
/// </list>
/// </remarks>
public class GetCustomerStatementQuery
{
    private readonly ICustomerRepository _customers;
    private readonly BookingAmountDueCalculator _calculator;
    private readonly ICustomerAgingService _aging;

    public GetCustomerStatementQuery(
        ICustomerRepository customers,
        BookingAmountDueCalculator calculator,
        ICustomerAgingService aging)
    {
        _customers = customers;
        _calculator = calculator;
        _aging = aging;
    }

    public async Task<Result<CustomerStatementResponse>> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetWithStatementDetailsAsync(id, cancellationToken);

        if (customer is null)
        {
            return Result<CustomerStatementResponse>.Failure("Customer not found.");
        }

        var today = _aging.Today;
        var activeBookings = customer.Bookings!.Where(b => b.Status != BookingStatus.Cancelled).ToList();

        var lines = activeBookings.Select(b =>
        {
            // The authoritative payment sum, excluding the soft-deleted payments (BR-27,
            // DEC-111) — the statement, the ledger and the dashboard describe one population.
            var paid = BookingPaymentPopulation.Collected(b);
            var breakdown = _calculator.Calculate(b, paid);
            var balance = breakdown.RemainingBalance;

            // Only a line that carries a balance ages (DEC-041 / DEC-053).
            var agingBucket = balance > 0m ? _aging.GetBucketKey(b.EventDate, today) : null;

            return new CustomerStatementLineResponse
            {
                TransactionId = b.TransactionId,
                BookingNumber = b.BookingNumber,
                EventDate = b.EventDate.ToDateTime(TimeOnly.MinValue),
                Occasion = b.Occasion ?? "Event",
                Amount = breakdown.NetAmountDue,
                Paid = paid,
                Balance = balance,
                Status = b.Status.ToString(),
                AgingBucket = agingBucket,
                AgingBucketLabel = agingBucket is null ? null : _aging.GetBucketLabel(agingBucket)
            };
        }).OrderByDescending(l => l.EventDate).ToList();

        // The customer's aging state — the worst bucket among the lines that carry a balance.
        var state = _aging.GetWorstBucketKey(
            lines.Select(l => new CustomerAgingBalance(
                l.EventDate.HasValue ? DateOnly.FromDateTime(l.EventDate.Value) : today,
                l.Balance)),
            today);

        // Refunds are reported separately; they never reduce the balance due (DEC-013).
        var totalRefunded = activeBookings
            .SelectMany(b => b.Refunds)
            .Sum(r => r.NetAmount);

        var statement = new CustomerStatementResponse
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.FullName,
            Contact = customer.Contact1 ?? customer.Contact2,
            Address = customer.Address,
            TotalBilled = lines.Sum(l => l.Amount),
            TotalPaid = lines.Sum(l => l.Paid),
            TotalRefunded = totalRefunded,
            OutstandingBalance = lines.Sum(l => l.Balance),
            LedgerState = state,
            LedgerStateLabel = _aging.GetBucketLabel(state),
            Transactions = lines
        };

        return Result<CustomerStatementResponse>.Success(statement);
    }
}
