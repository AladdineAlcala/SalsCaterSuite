namespace SalsCaterSuite.UnitTests.Common.Fakes;

using System.Reflection;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Minimal in-memory store shared by the hand-written repository fakes. It models the
/// parts of a single request-scoped <c>DbContext</c> that the migrated Application use
/// cases rely on: identity assignment on save, change tracking by reference (so in-place
/// entity mutations raise through the store), and navigation "include" semantics for the
/// detail / calendar / statement projections.
/// </summary>
public sealed class FakeSalsCaterSuiteDataStore
{
    private static readonly PropertyInfo TransactionIdProperty =
        typeof(Booking).GetProperty(nameof(Booking.TransactionId))!;

    private int _nextTransactionId = 100;
    private int _nextCustomerId = 100;
    private int _nextPackageId = 100;
    private int _nextAreaId = 100;
    private int _nextPackageCourseRuleId = 100;
    private int _nextPackageAreaCoverageId = 100;
    private int _nextAddonId = 100;

    public List<Customer> Customers { get; } = new();

    public List<Package> Packages { get; } = new();

    public List<Booking> Bookings { get; } = new();

    public List<Payment> Payments { get; } = new();

    public List<Refund> Refunds { get; } = new();

    public List<Menu> Menus { get; } = new();

    public List<CourseCategory> CourseCategories { get; } = new();

    public List<AddonDetail> AddonDetails { get; } = new();

    /// <summary>The add-on master's category level (REQ-BKG-07, DEC-142). Empty (BR-13).</summary>
    public List<AddonCategory> AddonCategories { get; } = new();

    /// <summary>The department master (the owner's 2026-10-24 request). Empty (BR-13).</summary>
    public List<Department> Departments { get; } = new();

    /// <summary>The discount catalogue (REQ-BKG-05). It is empty in production (BR-13).</summary>
    public List<Discount> Discounts { get; } = new();

    /// <summary>The municipality catalogue (REQ-PKG-02).</summary>
    public List<Area> Areas { get; } = new();

    /// <summary>The course-coverage rulesets (REQ-PKG-02).</summary>
    public List<PackageCourseRule> PackageCourseRules { get; } = new();

    /// <summary>The package-to-municipality coverage rows (REQ-PKG-02).</summary>
    public List<PackageAreaCoverage> PackageAreaCoverages { get; } = new();

    /// <summary>
    /// When true the store never assigns an IDENTITY <c>TransactionId</c>, so the
    /// post-INSERT step behaves like a persistence failure. Used to exercise the
    /// compensating-delete path of <c>CreateBookingCommand</c>.
    /// </summary>
    public bool SuppressIdentityAssignment { get; set; }

    /// <summary>
    /// Assigns the EF Core IDENTITY value to a booking. The setter is private (only the
    /// database may assign it in production), so a test fake has to use reflection.
    /// </summary>
    internal void AssignTransactionId(Booking booking)
    {
        if (SuppressIdentityAssignment)
        {
            return;
        }

        TransactionIdProperty.SetValue(booking, ++_nextTransactionId);
    }

    /// <summary>Assigns the IDENTITY <c>CustomerId</c> on INSERT, mirroring SQL Server.</summary>
    internal void AssignCustomerId(Customer customer)
    {
        if (customer.CustomerId == 0)
        {
            customer.CustomerId = ++_nextCustomerId;
        }
    }

    /// <summary>Assigns the IDENTITY <c>PackageId</c> on INSERT, mirroring SQL Server.</summary>
    internal void AssignPackageId(Package package)
    {
        if (package.PackageId == 0)
        {
            package.PackageId = ++_nextPackageId;
        }
    }

    /// <summary>Assigns the IDENTITY <c>AreaId</c> on INSERT, mirroring SQL Server.</summary>
    internal void AssignAreaId(Area area)
    {
        if (area.AreaId == 0)
        {
            area.AreaId = ++_nextAreaId;
        }
    }

    /// <summary>Assigns the IDENTITY <c>PackageCourseRuleId</c> on INSERT, mirroring SQL Server.</summary>
    internal void AssignPackageCourseRuleId(PackageCourseRule rule)
    {
        if (rule.PackageCourseRuleId == 0)
        {
            rule.PackageCourseRuleId = ++_nextPackageCourseRuleId;
        }
    }

    /// <summary>Assigns the IDENTITY <c>PackageAreaCoverageId</c> on INSERT, mirroring SQL Server.</summary>
    internal void AssignPackageAreaCoverageId(PackageAreaCoverage coverage)
    {
        if (coverage.PackageAreaCoverageId == 0)
        {
            coverage.PackageAreaCoverageId = ++_nextPackageAreaCoverageId;
        }
    }

    /// <summary>Assigns the IDENTITY <c>AddonId</c> on INSERT, mirroring SQL Server.</summary>
    internal void AssignAddonId(AddonDetail addon)
    {
        if (addon.AddonId == 0)
        {
            addon.AddonId = ++_nextAddonId;
        }
    }
}
