using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Booking");

        // ── Primary Key ──────────────────────────────────────────────────────
        builder.HasKey(e => e.TransactionId);
        builder.Property(e => e.TransactionId)
            .HasColumnName("trn_Id")
            .ValueGeneratedOnAdd();

        // ── Audit columns (from BaseEntity) ─────────────────────────────────
        builder.Property(e => e.CreatedByUser).HasColumnName("b_createdbyUser").HasMaxLength(100);
        builder.Property(e => e.CreatedDate).HasColumnName("b_createdDate");
        builder.Property(e => e.UpdatedDate).HasColumnName("b_updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted");

        // BaseEntity.Id (Guid) is not present in the legacy schema — ignore it.
        builder.Ignore(e => e.Id);

        // ── Classification ───────────────────────────────────────────────────
        builder.Property(e => e.TransactionDate).HasColumnName("transdate");

        // BookType stored as int (enum value) in the new schema.
        // Legacy column was 'booktype' nvarchar(50); new migrations will change the type.
        builder.Property(e => e.BookType)
            .HasColumnName("booktype")
            .HasConversion<int>();

        // TypeOfService stored as int (enum value); legacy was also int — compatible.
        builder.Property(e => e.TypeOfService)
            .HasColumnName("typeofservice")
            .HasConversion<int>();

        builder.Property(e => e.Occasion).HasColumnName("occasion").HasMaxLength(150);
        builder.Property(e => e.EventColor).HasColumnName("eventcolor").HasMaxLength(50);
        builder.Property(e => e.Reference).HasColumnName("reference").HasMaxLength(100);

        // ── Relationships ────────────────────────────────────────────────────
        builder.Property(e => e.CustomerId).HasColumnName("c_Id");
        builder.Property(e => e.PackageId).HasColumnName("p_id");
        builder.Property(e => e.ExtendedAreaId).HasColumnName("extendedAreaId");
        builder.Property(e => e.ApplyExtendedAmount).HasColumnName("apply_extendedAmount");

        // ── Event Details ────────────────────────────────────────────────────
        builder.Property(e => e.NoOfPerson).HasColumnName("noofperson");
        builder.Property(e => e.Venue).HasColumnName("venue").HasMaxLength(200);
        builder.Property(e => e.StartDate).HasColumnName("startdate");
        builder.Property(e => e.EndDate).HasColumnName("enddate");
        builder.Property(e => e.SpecialInstructions).HasColumnName("specialInstructions").HasMaxLength(500);

        // ── Price Snapshot (frozen at creation) ──────────────────────────────
        builder.Property(e => e.PackageName).HasColumnName("p_name").HasMaxLength(250);
        builder.Property(e => e.PricePerHead).HasColumnName("p_pricePerHead").HasPrecision(18, 2);
        builder.Property(e => e.PackageAmount).HasColumnName("p_amount").HasPrecision(18, 2);

        // ── Financial Breakdown ──────────────────────────────────────────────
        builder.Property(e => e.Subtotal).HasColumnName("subtotal").HasPrecision(18, 2);
        builder.Property(e => e.DiscountType)
            .HasColumnName("discountType")
            .HasConversion<int>();
        builder.Property(e => e.DiscountPercentage).HasColumnName("discountPercentage").HasPrecision(18, 4);
        builder.Property(e => e.DiscountAmount).HasColumnName("discountAmount").HasPrecision(18, 2);
        builder.Property(e => e.TaxAmount).HasColumnName("taxAmount").HasPrecision(18, 2);
        builder.Property(e => e.AmountDue).HasColumnName("amountDue").HasPrecision(18, 2);
        builder.Property(e => e.DepositAmount).HasColumnName("depositAmount").HasPrecision(18, 2);
        builder.Property(e => e.AmountPaid).HasColumnName("amountPaid").HasPrecision(18, 2);

        // Balance is a computed property — not stored in the database.
        builder.Ignore(e => e.Balance);

        // ── Status ───────────────────────────────────────────────────────────
        // Single Status enum replaces the legacy ServeStatus (bit) + IsCancelled (bit) pair.
        builder.Property(e => e.Status)
            .HasColumnName("booking_status")
            .HasConversion<int>()
            .HasDefaultValue(BookingStatus.Pending);

        builder.Property(e => e.ReminderSent).HasColumnName("reminderSent").HasDefaultValue(false);

        // ── Concurrency ──────────────────────────────────────────────────────
        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion()
            .IsConcurrencyToken();

        // ── Relationships ────────────────────────────────────────────────────
        builder.HasOne(e => e.Customer)
            .WithMany(c => c.Bookings)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Package)
            .WithMany(p => p.Bookings)
            .HasForeignKey(e => e.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Payments)
            .WithOne(p => p.Booking)
            .HasForeignKey(p => p.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Refunds)
            .WithOne(r => r.Booking)
            .HasForeignKey(r => r.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
