using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Bookings;

/// <summary>
/// A booking failure that must be surfaced as HTTP <c>404 Not Found</c> (contract §7, §15.1 ..
/// §16.4).
/// <para>
/// It exists so the WebApi layer can distinguish "the booking does not exist" from "the payload
/// was rejected" <b>by result type</b>, never by the error message text — the precedent set by
/// <c>PackageNotFoundResult</c>.
/// </para>
/// </summary>
public sealed class BookingNotFoundResult<T> : Result<T>
{
    public BookingNotFoundResult(string error)
        : base(false, default, new[] { error })
    {
    }
}
