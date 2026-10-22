namespace SalsCaterSuite.Application.Features.Bookings.Models;

using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Enums;

/// <summary>
/// The parsed filter of the Bookings &amp; Events Register (BR-32).
/// <para>
/// The <b>base</b> filters are the search and the date range — they bound the population the
/// repository loads. The <b>facet</b> filters are the status, the tier type, the venue, the
/// payment state and the overdue dimension — they are applied by the Application layer because
/// the payment state and the overdue dimension have no column and cannot be expressed in SQL
/// (spec §3.3, RSK-056).
/// </para>
/// </summary>
public sealed class BookingRegisterFilter
{
    /// <summary>The search term, trimmed. Empty matches everything.</summary>
    public string Search { get; init; } = string.Empty;

    /// <summary>The lifecycle statuses to include. Empty means every status.</summary>
    public IReadOnlyList<BookingStatus> Statuses { get; init; } = Array.Empty<BookingStatus>();

    /// <summary>The stored tier types to include. Empty means every tier.</summary>
    public IReadOnlyList<string> PackageTypes { get; init; } = Array.Empty<string>();

    /// <summary>The stored venues to include. Empty means every venue.</summary>
    public IReadOnlyList<string> Venues { get; init; } = Array.Empty<string>();

    /// <summary>The payment states to include. Empty means every state.</summary>
    public IReadOnlyList<string> PaymentStates { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The overdue filter — a <b>tri-state</b> (BR-32): <c>null</c> applies no overdue filter,
    /// <c>true</c> selects only the overdue bookings and <c>false</c> selects only the
    /// bookings that are <b>not</b> overdue.
    /// </summary>
    public bool? Overdue { get; init; }

    /// <summary>The inclusive lower bound on the event date.</summary>
    public DateOnly? FromDate { get; init; }

    /// <summary>The inclusive upper bound on the event date.</summary>
    public DateOnly? ToDate { get; init; }

    /// <summary>The 1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>The page size.</summary>
    public int PageSize { get; init; } = 10;

    /// <summary>The requested sort.</summary>
    public string Sort { get; init; } = BookingRegisterSort.Default;

    /// <summary>
    /// Parses a validated transport request into the typed filter. The vocabulary is checked by
    /// the validator before this runs, so the parse cannot fail — but it still uses the
    /// <b>one</b> vocabulary (<see cref="BookingPaymentStateClassifier"/>), never a second copy
    /// of the names.
    /// </summary>
    public static BookingRegisterFilter From(BookingRegisterFilterRequest request) => new()
    {
        Search = request.Search?.Trim() ?? string.Empty,
        Statuses = ParseStatuses(request.Status),
        PackageTypes = ParseList(request.PackageType),
        Venues = ParseList(request.Venue),
        PaymentStates = ParseList(request.PaymentState),
        Overdue = request.Overdue,
        FromDate = request.FromDate,
        ToDate = request.ToDate,
        Page = request.Page,
        PageSize = request.PageSize,
        Sort = string.IsNullOrWhiteSpace(request.Sort)
            ? BookingRegisterSort.Default
            : request.Sort.Trim()
    };

    /// <summary>The comma-separated <c>BookingStatus</c> names.</summary>
    private static IReadOnlyList<BookingStatus> ParseStatuses(string? csv)
    {
        var names = ParseList(csv);

        return names
            .Select(name => Enum.Parse<BookingStatus>(name, ignoreCase: false))
            .ToList();
    }

    /// <summary>The comma-separated values, trimmed, with the blanks removed.</summary>
    private static IReadOnlyList<string> ParseList(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<string>();
        }

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => value.Length > 0)
            .ToList();
    }
}
