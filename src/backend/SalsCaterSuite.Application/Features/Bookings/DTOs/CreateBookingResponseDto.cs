namespace SalsCaterSuite.Application.Features.Bookings.DTOs;

/// <summary>
/// Response body for POST /api/bookings (201 Created).
/// Serializes to <c>{ "transactionId": &lt;id&gt; }</c> under the API's camelCase convention.
/// </summary>
public class CreateBookingResponseDto
{
    public int TransactionId { get; set; }
}
