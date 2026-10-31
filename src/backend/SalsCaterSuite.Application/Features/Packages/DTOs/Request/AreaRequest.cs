namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>
/// The body of <c>POST /api/areas</c> and <c>PUT /api/areas/{id}</c> (contract §13.10, §13.11).
/// <para>
/// Every member except <see cref="AreaDetails"/> may be <c>null</c> — a null rate is "no rate has
/// been stated" and a null logistics cost omits the logistics band (DEC-086, DEC-081). The
/// defaults exist so a member that is <b>absent</b> from the payload reaches the validator
/// instead of the serializer (BR-11).
/// </para>
/// </summary>
public record AreaRequest(
    string AreaDetails = "",
    decimal? AdditionalRate = null,
    int? DistanceKm = null,
    string? LogisticsNote = null,
    decimal? LogisticsCost = null);
