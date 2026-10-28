using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Infrastructure.Identity;
using SalsCaterSuite.Infrastructure.Persistence.Configurations;

namespace SalsCaterSuite.Infrastructure.Persistence;

public class SalsCaterSuiteDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public SalsCaterSuiteDbContext(DbContextOptions<SalsCaterSuiteDbContext> options) : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackagePaxApplicable> PackagePaxApplicables => Set<PackagePaxApplicable>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<CourseCategory> CourseCategories => Set<CourseCategory>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<AddonDetail> AddonDetails => Set<AddonDetail>();
    public DbSet<AddonCategory> AddonCategories => Set<AddonCategory>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Discount> Discounts => Set<Discount>();

    // REQ-BKG-06 / 07 (DEC-127, DEC-139): the booking's frozen dish selection and its
    // frozen add-on lines. They are child rows of the booking aggregate.
    public DbSet<BookingMenu> BookingMenus => Set<BookingMenu>();
    public DbSet<BookingAddOn> BookingAddOns => Set<BookingAddOn>();

    // REQ-PKG-02 (DEC-077): the course-coverage ruleset and the package-to-municipality
    // coverage matrix.
    public DbSet<PackageCourseRule> PackageCourseRules => Set<PackageCourseRule>();
    public DbSet<PackageAreaCoverage> PackageAreaCoverages => Set<PackageAreaCoverage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Map Identity tables to match legacy schema
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("Roles");
        });

        builder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.ToTable("UserRoles");
        });

        builder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.ToTable("UserClaims");
        });

        builder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.ToTable("UserLogins");
        });

        builder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.ToTable("UserTokens");
        });

        builder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.ToTable("RoleClaims");
        });

        // Other domain entities
        builder.Entity<CourseCategory>(entity =>
        {
            entity.ToTable("CourseCategory");
            entity.HasKey(e => e.CourseId).HasName("PK_CourseCategory");
            entity.Property(e => e.CourseId).HasColumnName("courseId");
            entity.Property(e => e.CourseName).HasColumnName("course").HasMaxLength(100);

            // DEC-038: BaseEntity audit columns (documented generic pattern).
            entity.Property(e => e.CreatedDate).HasColumnName("createdDate");
            entity.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
            entity.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        builder.Entity<AddonDetail>(entity =>
        {
            entity.ToTable("AddonDetail");
            entity.HasKey(e => e.AddonId);
            entity.Property(e => e.AddonId).HasColumnName("addonId");
            entity.Property(e => e.Description).HasColumnName("addonDescription").HasMaxLength(150);
            entity.Property(e => e.UnitPrice).HasColumnName("unitPrice").HasPrecision(18, 2);
            entity.Property(e => e.Unit).HasColumnName("unit").HasMaxLength(50);
            entity.Property(e => e.IsActive).HasColumnName("isActive");

            // REQ-BKG-07 (DEC-142, DEC-158): the category and the explicit pricing basis.
            // A null category is "not stated" (never a category named "General"), and a null
            // basis is "not stated" and is treated as Flat by the server.
            entity.Property(e => e.AddonCategoryId).HasColumnName("addonCategoryId");
            entity.Property(e => e.PricingBasis).HasColumnName("pricingBasis").HasConversion<int>();

            entity.HasOne(e => e.AddonCategory)
                .WithMany()
                .HasForeignKey(e => e.AddonCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // The owner's 2026-10-24 request: the add-on's owning department. Nullable, because
            // the legacy add-on rows pre-date the department master.
            entity.Property(e => e.DeptId).HasColumnName("deptId");
            entity.HasOne(e => e.Department)
                .WithMany(department => department.AddonDetails)
                .HasForeignKey(e => e.DeptId)
                .OnDelete(DeleteBehavior.Restrict);

            // DEC-038: BaseEntity audit columns (documented generic pattern).
            entity.Property(e => e.CreatedDate).HasColumnName("createdDate");
            entity.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
            entity.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        builder.Entity<Discount>(entity =>
        {
            entity.ToTable("Discount");
            entity.HasKey(e => e.DiscountId);
            entity.Property(e => e.DiscountId).HasColumnName("disc_Id");
            entity.Property(e => e.DiscountCode).HasColumnName("disc_code").HasMaxLength(50);
            entity.Property(e => e.Description).HasColumnName("disc_description").HasMaxLength(150);
            entity.Property(e => e.Percentage).HasColumnName("percentage").HasPrecision(18, 2);
            entity.Property(e => e.FixedAmount).HasColumnName("fixed_amount").HasPrecision(18, 2);
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            // DEC-038: all four BaseEntity audit columns are persisted. Discount keeps its
            // existing date_Created/date_Updated convention; the other two use the generic
            // pattern (is_deleted matches Booking's existing soft-delete column name).
            entity.Property(e => e.CreatedDate).HasColumnName("date_Created");
            entity.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
            entity.Property(e => e.UpdatedDate).HasColumnName("date_Updated");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        builder.Entity<Reservation>(entity =>
        {
            entity.ToTable("Reservation");
            entity.HasKey(e => e.ReservationId);
            entity.Property(e => e.ReservationId).HasColumnName("resId");
            entity.Property(e => e.CustomerId).HasColumnName("c_Id");
            entity.Property(e => e.ReservationDate).HasColumnName("resDate");
            entity.Property(e => e.NoOfPax).HasColumnName("noofPax");
            entity.Property(e => e.Occasion).HasColumnName("occasion").HasMaxLength(150);
            entity.Property(e => e.ReserveStatus).HasColumnName("reserveStat");
            entity.Property(e => e.EventVenue).HasColumnName("eventVenue").HasMaxLength(200);

            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Reservations)
                .HasForeignKey(e => e.CustomerId);

            // DEC-038: BaseEntity audit columns (documented generic pattern).
            entity.Property(e => e.CreatedDate).HasColumnName("createdDate");
            entity.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
            entity.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        builder.Entity<Refund>(entity =>
        {
            entity.ToTable("Refund");
            entity.HasKey(e => e.RefundId);
            entity.Property(e => e.RefundId).HasColumnName("Rf_id");
            entity.Property(e => e.RefundDate).HasColumnName("rfDate");
            // Non-nullable — a refund must always belong to a booking.
            entity.Property(e => e.TransactionId).HasColumnName("trn_Id").IsRequired();
            entity.Property(e => e.RefundReason).HasColumnName("rf_Reason").HasMaxLength(250);
            entity.Property(e => e.RefundAmount).HasColumnName("rf_Amount").HasPrecision(18, 2);
            entity.Property(e => e.Deduction).HasColumnName("rfDeduction").HasPrecision(18, 2);
            entity.Property(e => e.NetAmount).HasColumnName("rfNetAmount").HasPrecision(18, 2);
            // Status stored as int; 0=Pending, 1=Processed, 2=Rejected — preserves legacy data.
            entity.Property(e => e.Status)
                .HasColumnName("rf_Stat")
                .HasConversion<int>()
                .HasDefaultValue(Domain.Enums.RefundStatus.Pending);

            // DEC-038: BaseEntity audit columns (documented generic pattern).
            entity.Property(e => e.CreatedDate).HasColumnName("createdDate");
            entity.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
            entity.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        });

        // Apply custom configurations
        builder.ApplyConfiguration(new BookingConfiguration());
        builder.ApplyConfiguration(new PaymentConfiguration());
        builder.ApplyConfiguration(new MenuConfiguration());
        builder.ApplyConfiguration(new CustomerConfiguration());
        builder.ApplyConfiguration(new PackageConfiguration());
        builder.ApplyConfiguration(new AreaConfiguration());

        // REQ-PKG-04 (DEC-173): the read-only applicable-pax classification lookup.
        builder.ApplyConfiguration(new PackagePaxApplicableConfiguration());

        // REQ-PKG-02 (DEC-077): the course-coverage ruleset and the coverage matrix, with their
        // filtered unique indexes (DEC-088).
        builder.ApplyConfiguration(new PackageCourseRuleConfiguration());
        builder.ApplyConfiguration(new PackageAreaCoverageConfiguration());

        // REQ-BKG-06 / 07 (DEC-127, DEC-139, DEC-142): the add-on master's category level and
        // the booking's two frozen line-item tables, each with its filtered unique index.
        builder.ApplyConfiguration(new AddonCategoryConfiguration());
        builder.ApplyConfiguration(new DepartmentConfiguration());
        builder.ApplyConfiguration(new BookingMenuConfiguration());
        builder.ApplyConfiguration(new BookingAddOnConfiguration());
    }
}

