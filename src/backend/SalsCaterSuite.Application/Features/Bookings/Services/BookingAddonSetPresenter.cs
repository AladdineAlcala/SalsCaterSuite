using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The <b>one</b> projection of a booking's add-on lines onto <see cref="BookingAddonSetDto"/>
/// (contract §16.3).
/// <para>
/// <c>description</c>, <c>amount</c>, <c>unit</c> and <c>pricingBasis</c> are the <b>frozen</b>
/// line values; <c>catalogueAmount</c> and <c>catalogueDescription</c> are the catalogue's
/// <b>current</b> values, published alongside so an operator can see that a price has moved
/// since the line was frozen (BR-04, DEC-158). A <c>null</c> catalogue field means the catalogue
/// row is gone or soft-deleted — <b>not</b> that the line is zero.
/// </para>
/// </summary>
public class BookingAddonSetPresenter
{
    private readonly IBookingPolicyProvider _policy;
    private readonly IDateTime _clock;

    public BookingAddonSetPresenter(IBookingPolicyProvider policy, IDateTime clock)
    {
        _policy = policy;
        _clock = clock;
    }

    /// <summary>Describes the booking's <b>live</b> add-on lines and the menu-change window.</summary>
    public BookingAddonSetDto Build(Booking booking)
    {
        var window = BookingMenuWindow.Resolve(
            booking.EventDate,
            _policy.MenuSelectionLockDaysBeforeEvent,
            DateOnly.FromDateTime(_clock.UtcNow));

        return new BookingAddonSetDto
        {
            TransactionId = booking.TransactionId,
            BookingNumber = booking.BookingNumber,
            EventDate = booking.EventDate,
            MenuSelectionLockDate = window.LockDate,
            MenuSelectionLockDays = window.LockDays,
            IsLocked = window.IsLocked,
            AddonsTotal = booking.AddonsTotal,
            Addons = booking.AddOns
                .Where(line => !line.IsDeleted)
                .OrderBy(line => line.AddonId)
                .Select(Map)
                .ToList()
        };
    }

    private static BookingAddonLineDto Map(BookingAddOn line)
    {
        // The category is not frozen on the line (the line freezes the commercial truth: the
        // description, the line total, the unit and the basis). It is read from the catalogue
        // row, so it is null when that row is gone — "not stated", never a guess.
        var catalogue = line.AddonDetail is { IsDeleted: false } ? line.AddonDetail : null;

        return new BookingAddonLineDto
        {
            AddonId = line.AddonId,
            Description = line.Description,
            Amount = line.Amount,
            Unit = line.Unit,
            PricingBasis = line.PricingBasis,
            AddonCategoryId = catalogue?.AddonCategoryId,
            AddonCategoryName = catalogue?.AddonCategory?.AddonCategoryName,
            CatalogueAmount = catalogue?.UnitPrice,
            CatalogueDescription = catalogue?.Description
        };
    }
}
