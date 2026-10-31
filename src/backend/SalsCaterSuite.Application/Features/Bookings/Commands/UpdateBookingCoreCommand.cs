using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;

namespace SalsCaterSuite.Application.Features.Bookings.Commands;

/// <summary>
/// Updates editable booking core data such as the event date and headcount.
/// </summary>
public class UpdateBookingCoreCommand
{
    private readonly IBookingRepository _bookings;
    private readonly IValidator<UpdateBookingCoreRequest> _validator;

    public UpdateBookingCoreCommand(
        IBookingRepository bookings,
        IValidator<UpdateBookingCoreRequest> validator)
    {
        _bookings = bookings;
        _validator = validator;
    }

    public async Task<Result> ExecuteAsync(
        int transactionId,
        UpdateBookingCoreRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var booking = await _bookings.GetByIdAsync(transactionId, cancellationToken);
        if (booking is null)
        {
            return Result.Failure("Booking not found.");
        }

        try
        {
            booking.UpdateCoreDetails(request.EventDate, request.NumberOfHeads);
        }
        catch (Domain.Exceptions.DomainException ex)
        {
            return Result.Failure(ex.Message);
        }

        booking.UpdatedDate = DateTime.UtcNow;
        await _bookings.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
