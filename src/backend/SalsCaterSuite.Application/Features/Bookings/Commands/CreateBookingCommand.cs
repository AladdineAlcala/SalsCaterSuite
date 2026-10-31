using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Features.Payments.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Exceptions;

namespace SalsCaterSuite.Application.Features.Bookings.Commands;

public class CreateBookingCommand
{
    private readonly ICustomerRepository _customers;
    private readonly IPackageRepository _packages;
    private readonly IBookingRepository _bookings;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateBookingRequest> _validator;
    private readonly BookingCreateCapabilities? _capabilities;

    /// <summary>
    /// Creates a booking — a <b>draft</b> (<c>Pending</c>) or a <b>confirmed</b> booking, with an
    /// optional initial payment (BR-33, DEC-107, contract §14.4).
    /// <para>
    /// The frozen snapshot is stored: the price per head, the package amount, the subtotal, the
    /// out-of-town surcharge, the discount and the net amount due. The stored
    /// <c>Booking.AmountDue</c> therefore <b>equals</b> the shared calculator's
    /// <c>NetAmountDue</c> to the cent (BR-25, DEC-099, DEC-115).
    /// </para>
    /// <para>
    /// The extended capabilities (the municipality catalogue, the discount catalogue, the payment
    /// ledger and the receipt-number generator) arrive in one optional object, so the command's
    /// original collaborators stay exactly where they were.
    /// </para>
    /// </summary>
    public CreateBookingCommand(
        ICustomerRepository customers,
        IPackageRepository packages,
        IBookingRepository bookings,
        ICurrentUserService currentUser,
        IValidator<CreateBookingRequest> validator,
        BookingCreateCapabilities? capabilities = null)
    {
        _customers = customers;
        _packages = packages;
        _bookings = bookings;
        _currentUser = currentUser;
        _validator = validator;
        _capabilities = capabilities;
    }

    public async Task<Result<int>> ExecuteAsync(CreateBookingRequest request, CancellationToken cancellationToken = default)
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

            // A deactivated tier is not offered, so a booking cannot be created on one (BR-29).
            if (!package.IsActive || package.IsDeleted)
            {
                return Result<int>.Failure($"Package with ID {request.PackageId} is not an active package.");
            }
        }

        // ── 3. Resolve price per head ────────────────────────────────────────
        // Priority: explicit override → package rate → 0 (bespoke, to be set later).
        // The rate is rounded once, to two decimals away from the midpoint (DEC-115), so the
        // frozen package amount is exact and the stored AmountDue equals the calculator's
        // NetAmountDue to the cent.
        decimal pricePerHead = BookingAmountDueCalculator.Round2(request.OverridePricePerHead
            ?? package?.AmountPax
            ?? 0m);

        // ── 4. Resolve the municipality (the out-of-town surcharge's rate) ────
        Area? area = null;
        if (request.ExtendedAreaId.HasValue)
        {
            if (_capabilities?.Areas is null)
            {
                return Result<int>.Failure("The municipality catalogue is not available.");
            }

            area = await _capabilities.Areas.GetLiveByIdAsync(request.ExtendedAreaId.Value, cancellationToken);
            if (area == null)
            {
                return Result<int>.Failure($"Municipality with ID {request.ExtendedAreaId} not found.");
            }
        }

        // ── 4b. BR-40: the surcharge is limited to the Regular and Premier types ────
        // The test is on the tier's own type column, never on its description (DEC-156).
        // The tier's own per-guest amount is part of the same surcharge (the owner's
        // 2026-10-24 request), so either source makes the tier subject to the eligibility rule.
        var carriesExtendedAmount = request.ExtendedAreaId.HasValue
            || (package?.ExtendedLocationAmount ?? 0m) > 0m;

        if (request.ApplyExtendedAmount
            && carriesExtendedAmount
            && !PackageSurchargeEligibility.IsEligible(package?.PackageType))
        {
            return Result<int>.Failure(
                PackageSurchargeEligibility.RejectionMessage(package?.PackageType));
        }

        // ── 5. Resolve the discount (BR-02) ──────────────────────────────────
        Discount? discount = null;
        string? discountCode = null;
        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            if (_capabilities?.Discounts is null)
            {
                return Result<int>.Failure("The discount catalogue is not available.");
            }

            discountCode = request.DiscountCode.Trim();
            var matches = await _capabilities.Discounts.GetActiveByCodeAsync(discountCode, cancellationToken);

            if (matches.Count == 0)
            {
                return Result<int>.Failure("The discount code was not found.");
            }

            if (matches.Count > 1)
            {
                return Result<int>.Failure("The discount code is ambiguous.");
            }

            discount = matches[0];
        }

        // ── 5b. Resolve the dish selection and the add-on lines (BR-15, BR-39) ──
        // The SAME resolver the quote uses, so the quoted figure and the frozen figure can
        // never diverge (BR-31, DEC-153). An absent member means "none".
        var lineItems = new ResolvedBookingLineItems(
            Array.Empty<ResolvedSelectionRow>(),
            Array.Empty<ResolvedAddonLine>(),
            0m);

        var carriesLineItems = request.Selections is { Count: > 0 } || request.Addons is { Count: > 0 };

        if (carriesLineItems)
        {
            if (_capabilities?.LineItems is null)
            {
                return Result<int>.Failure("Dish selection and add-ons are not available.");
            }

            var resolved = await _capabilities.LineItems.ResolveAsync(
                request.PackageId,
                request.NoOfPerson,
                request.Selections,
                request.Addons,
                cancellationToken);

            if (!resolved.Succeeded)
            {
                return Result<int>.Failure(resolved.Errors);
            }

            lineItems = resolved.Data!;
        }

        // ── 6. Price the booking through the one pricing computation ─────────
        var pricing = (_capabilities?.Pricing ?? new BookingPricingService(
                _capabilities?.Policy ?? DefaultBookingPolicyProvider.Instance))
            .Price(new BookingPricingInput
            {
                PricePerHead = pricePerHead,
                NumberOfHeads = request.NoOfPerson,
                ExtendedAreaRate = area?.AdditionalRate,
                PackageExtendedLocationRate = package?.ExtendedLocationAmount,
                ApplyExtendedAmount = request.ApplyExtendedAmount,
                Discount = discount,
                DiscountCode = discountCode,
                AddonsTotal = lineItems.AddonsTotal,
                StatedDepositAmount = request.DepositAmount
            });

        // ── 7. The overpayment guard (BR-05, DEC-118) ───────────────────────
        // A payment that would take the total above the net amount due is refused, and
        // NOTHING is written: no booking row and no payment row.
        if (request.InitialPayment is not null && request.InitialPayment.AmountPaid > pricing.NetAmountDue)
        {
            return Result<int>.Failure(
                "The initial payment cannot exceed the net amount due of the booking.");
        }

        // ── 8. Create booking via domain factory ─────────────────────────────
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
                specialInstructions: request.SpecialInstructions,
                callTime: request.CallTime,
                packoutTime: request.PackoutTime,
                discountCode: discountCode,
                extendedAreaAmount: pricing.ExtendedAreaAmount
            );
        }
        catch (DomainException ex)
        {
            // Domain invariant violated → return a failed result (mapped to HTTP 400).
            return Result<int>.Failure(ex.Message);
        }

        // The frozen money snapshot. Every input is already two decimals, so the stored
        // AmountDue is exact and equals the calculator's NetAmountDue (DEC-115, DEC-099).
        booking.DiscountType = pricing.DiscountType;
        booking.DiscountPercentage = pricing.DiscountPercentage;
        booking.RecalculateFinancials(
            extendedAreaAmount: pricing.ExtendedAreaAmount,
            addonsAmount: pricing.AddonsTotal,
            discountAmount: pricing.DiscountAmount,
            taxAmount: 0m);

        // ── 8b. Stage the frozen line items in the SAME change set (DEC-134, DEC-151) ──
        // A booking and its dishes (or its add-on lines) are never half-written.
        foreach (var row in lineItems.Selections)
        {
            booking.Menus.Add(new BookingMenu
            {
                CourseId = row.CourseId,
                MenuId = row.MenuId,
                PrepInstruction = row.PrepInstruction,
                Booking = booking
            });
        }

        foreach (var line in lineItems.Addons)
        {
            booking.AddOns.Add(new BookingAddOn
            {
                AddonId = line.AddonId,
                Description = line.Description,
                Amount = line.Amount,
                Unit = line.Unit,
                PricingBasis = line.PricingBasis,
                Booking = booking
            });
        }

        // The deposit is advisory — it never changes the balance (BR-30).
        booking.SetDepositAmount(pricing.DepositAmount);

        booking.CreatedByUser = _currentUser.UserName ?? "system";
        booking.CreatedDate = DateTime.UtcNow;

        // ── 9. The initial payment, staged in the same change set ────────────
        if (request.InitialPayment is not null)
        {
            if (_capabilities?.Payments is null)
            {
                return Result<int>.Failure("Payment recording is not available.");
            }

            // The receipt number comes from the ONE generator, shared with the payment ledger
            // (DEC-112).
            var now = DateTime.UtcNow;
            var paymentNo = await ReceiptNumberGenerator.GenerateAsync(_capabilities.Payments, now, cancellationToken);

            var payment = new Payment
            {
                PaymentNo = paymentNo,
                DateOfPayment = now,
                Particular = request.InitialPayment.Particular,
                AmountPaid = request.InitialPayment.AmountPaid,
                PaymentMeans = request.InitialPayment.PaymentMeans,
                CheckNo = request.InitialPayment.CheckNo,
                Notes = request.InitialPayment.Notes,
                CreatedByUser = _currentUser.UserName ?? "system",
                CreatedDate = now
            };

            // The payment belongs to the booking's own collection, so EF Core inserts the
            // booking and the payment in ONE transaction and in the right order: the booking
            // first (its IDENTITY key is generated), then the payment with that key (BR-33).
            booking.Payments.Add(payment);
            payment.Booking = booking;

            // The domain method refreshes the denormalized AmountPaid cache and the lifecycle
            // status. The confirmation is applied afterwards, so a confirmed booking that
            // carries a deposit is published as Confirmed (BR-33, AC-BKG-24).
            booking.RecordPayment(request.InitialPayment.AmountPaid);
        }

        // A draft stays Pending; a confirmed booking becomes Confirmed (BR-33, DEC-107).
        if (request.Confirm)
        {
            try
            {
                booking.Confirm();
            }
            catch (DomainException ex)
            {
                return Result<int>.Failure(ex.Message);
            }
        }

        await _bookings.AddAsync(booking, cancellationToken);

        // The payment is NOT added through the payment repository: it already belongs to the
        // booking's own collection, so EF Core's graph traversal marks it Added with the
        // booking and inserts both rows in ONE transaction, in the right order (the booking
        // first, then the payment with the generated foreign key). Adding it a second time
        // would be a no-op on an already-tracked entity and would only mislead the reader.

        try
        {
            await _bookings.SaveChangesAsync(cancellationToken);   // assigns the IDENTITY TransactionId
        }
        catch (Exception ex)
        {
            // Nothing is persisted; the failure is reported as a business failure rather than
            // an unhandled fault (BR-11).
            return Result<int>.Failure($"The booking could not be created: {ex.Message}");
        }

        // ── 10. Assign the human-readable booking number (DEC-016) ────────────
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
