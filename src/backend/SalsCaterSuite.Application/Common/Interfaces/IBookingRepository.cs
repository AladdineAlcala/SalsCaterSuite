namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Domain.Entities;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(int transactionId, CancellationToken cancellationToken = default);
    Task<Booking?> GetWithDetailsAsync(int transactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);
    void Update(Booking booking);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

