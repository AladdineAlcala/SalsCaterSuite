using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Exceptions;

namespace SalsCaterSuite.Application.Features.Bookings.Commands;

public class CreateBookingCommand
{
    private readonly ICustomerRepository _customers;
    private readonly IPackageRepository _packages;
    private readonly IBookingRepository _bookings;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateBookingRequestDto> _validator;

    public CreateBookingCommand(
        ICustomerRepository customers,
        IPackageRepository packages,
        IBookingRepository bookings,
        ICurrentUserService currentUser,
        IValidator<CreateBookingRequestDto> validator)
    {
        _customers = customers;
        _packages = packages;
        _bookings = bookings;
        _currentUser = currentUser;
        _validator = validator;
    }

    public async Task<Result<int>> ExecuteAsync(CreateBookingRequestDto request, CancellationToken cancellationToken = default)
    {
        // ── 0. Validate the request before any persistence (DEC-014) ─────────
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<int>.Failure(validation.Errors.Select(e => e.ErrorMessage));
        }

        // ── 1. Validate customer ─────────────────────────────────────────────
        var customer = await _customers.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer == null)
        {
            return Result<int>.Failure("Customer not found.");
        }

        // ── 2. Resolve package (optional for custom / bespoke bookings) ──────
        Package? package = null;
        if (request.PackageId.HasValue)
        {
            package = await _packages.GetByIdAsync(request.PackageId.Value, cancellationToken);
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
        Booking booking;
        try
        {
            booking = Booking.Create(
                customerId: request.CustomerId,
                noOfPerson: request.NoOfPerson,
                bookType: request.BookType,
                typeOfService: request.TypeOfService,
                eventDate: DateOnly.FromDateTime(request.StartDate),
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
        }
        catch (DomainException ex)
        {
            // Domain invariant violated → return a failed result (mapped to HTTP 400).
            return Result<int>.Failure(ex.Message);
        }

        booking.CreatedByUser = _currentUser.UserName ?? "system";
        booking.CreatedDate = DateTime.UtcNow;

        await _bookings.AddAsync(booking, cancellationToken);
        await _bookings.SaveChangesAsync(cancellationToken);   // assigns the IDENTITY TransactionId

        // ── 5. Assign the human-readable booking number (DEC-016) ────────────
        try
        {
            booking.AssignBookingNumber();
            await _bookings.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // A booking must never be left without a booking number, so the whole
            // create operation fails and the just-inserted row is removed.
            _bookings.Remove(booking);
            await _bookings.SaveChangesAsync(cancellationToken);
            return Result<int>.Failure($"Failed to assign a booking number: {ex.Message}");
        }

        return Result<int>.Success(booking.TransactionId);
    }
}
