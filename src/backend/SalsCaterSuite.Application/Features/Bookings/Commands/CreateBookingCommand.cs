using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Bookings.Commands;

public class CreateBookingCommand
{
    private readonly ISalsCaterSuiteDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateBookingCommand(ISalsCaterSuiteDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> ExecuteAsync(CreateBookingRequestDto request, CancellationToken cancellationToken = default)
    {
        // ── 1. Validate customer ─────────────────────────────────────────────
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId, cancellationToken);
        if (customer == null)
        {
            return Result<int>.Failure("Customer not found.");
        }

        // ── 2. Resolve package (optional for custom / bespoke bookings) ──────
        Package? package = null;
        if (request.PackageId.HasValue)
        {
            package = await _context.Packages
                .FirstOrDefaultAsync(p => p.PackageId == request.PackageId.Value, cancellationToken);
            if (package == null)
            {
                return Result<int>.Failure($"Package with ID {request.PackageId} not found.");
            }
        }

        // ── 3. Resolve price per head ────────────────────────────────────────
        // Priority: explicit override → package rate → 0 (bespoke, to be set later)
        decimal pricePerHead = request.OverridePricePerHead
            ?? package?.AmountPax
            ?? 0m;

        // ── 4. Create booking via domain factory ─────────────────────────────
        var booking = Booking.Create(
            customerId: request.CustomerId,
            noOfPerson: request.NoOfPerson,
            bookType: request.BookType,
            typeOfService: request.TypeOfService,
            startDate: request.StartDate,
            endDate: request.EndDate,
            occasion: request.Occasion,
            venue: request.Venue,
            eventColor: request.EventColor,
            reference: request.Reference,
            packageId: request.PackageId,
            packageName: package?.Description,
            pricePerHead: pricePerHead,
            extendedAreaId: request.ExtendedAreaId,
            applyExtendedAmount: request.ApplyExtendedAmount,
            specialInstructions: request.SpecialInstructions
        );

        booking.CreatedByUser = _currentUser.UserName ?? "system";
        booking.CreatedDate = DateTime.UtcNow;

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(booking.TransactionId);
    }
}
