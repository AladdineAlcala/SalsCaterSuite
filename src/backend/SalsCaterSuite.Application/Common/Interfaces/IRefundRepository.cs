namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Refund persistence (DEC-037, DEC-024). Refunds belong to the booking aggregate,
/// which is why this repository only stages rows: the commit boundary is owned by
/// <see cref="IBookingRepository.SaveChangesAsync"/>, so a cancellation and its
/// refund are committed in a single unit of work.
/// </summary>
public interface IRefundRepository
{
    Task AddAsync(Refund refund, CancellationToken cancellationToken = default);
}
