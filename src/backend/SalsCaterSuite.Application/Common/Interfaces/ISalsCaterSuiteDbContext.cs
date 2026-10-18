using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Common.Interfaces;

public interface ISalsCaterSuiteDbContext
{
    DbSet<Booking> Bookings { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Menu> Menus { get; }
    DbSet<Package> Packages { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Refund> Refunds { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<CourseCategory> CourseCategories { get; }
    DbSet<Area> Areas { get; }
    DbSet<AddonDetail> AddonDetails { get; }
    DbSet<Discount> Discounts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

