using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using FluentValidation;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Validators;

/// <summary>
/// The filter validator of the Bookings &amp; Events Register (BR-32, contract §14.1).
/// <para>
/// An unknown status name or an unknown payment-state name is a <c>400</c>: the vocabulary is
/// never silently widened. An unknown <em>tier type</em> or <em>venue</em> is <b>not</b> a
/// rejection — those are free-text values stored on the booking, and the facet list publishes
/// exactly the values the population carries.
/// </para>
/// </summary>
public class BookingRegisterFilterValidator : AbstractValidator<BookingRegisterFilterRequest>
{
    /// <summary>The page sizes the contract publishes.</summary>
    public static readonly int[] PermittedPageSizes = { 10, 25, 50 };

    public BookingRegisterFilterValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("The page number must be greater than 0.");

        RuleFor(x => x.PageSize)
            .Must(size => PermittedPageSizes.Contains(size))
            .WithMessage("The page size must be 10, 25 or 50.");

        RuleFor(x => x)
            .Must(request =>
                !request.FromDate.HasValue ||
                !request.ToDate.HasValue ||
                request.ToDate.Value >= request.FromDate.Value)
            .WithMessage("The end of the date range must not be before its start.");

        RuleFor(x => x.Status)
            .Must(BeKnownStatuses)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("One or more of the booking statuses is not a known booking status.");

        RuleFor(x => x.PaymentState)
            .Must(BeKnownPaymentStates)
            .When(x => !string.IsNullOrWhiteSpace(x.PaymentState))
            .WithMessage("One or more of the payment states is not a known payment state.");

        RuleFor(x => x.Sort)
            .Must(BeKnownSort)
            .When(x => !string.IsNullOrWhiteSpace(x.Sort))
            .WithMessage("The requested sort is not a known sort.");
    }

    /// <summary>Every comma-separated status name is a <see cref="BookingStatus"/> member.</summary>
    private static bool BeKnownStatuses(string? csv)
    {
        foreach (var name in Split(csv))
        {
            if (!Enum.TryParse<BookingStatus>(name, ignoreCase: false, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Every comma-separated payment state is one of the three published states.</summary>
    private static bool BeKnownPaymentStates(string? csv) =>
        Split(csv).All(BookingPaymentStateClassifier.IsKnown);

    /// <summary>The requested sort is one of the published sorts.</summary>
    private static bool BeKnownSort(string? sort) =>
        BookingRegisterSort.All.Contains(sort!.Trim(), StringComparer.Ordinal);

    private static IEnumerable<string> Split(string? csv) =>
        (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => value.Length > 0);
}
