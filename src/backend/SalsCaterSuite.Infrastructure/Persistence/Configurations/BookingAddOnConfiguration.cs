using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

/// <summary>
/// The mapping of a booking's frozen add-on lines (REQ-BKG-07, DEC-139, BR-39, DEC-158).
/// <para>
/// The column names are the data dictionary's: <c>bookingAddOnId</c>, <c>trn_Id</c>,
/// <c>addonId</c>, <c>description</c>, <c>amount</c>, <c>unit</c> and <c>pricingBasis</c>.
/// There is <b>no</b> quantity column: the line stores the <b>line total</b>, which is the
/// commercial truth, so an amount and a quantity can never disagree (DEC-140, DEC-158).
/// </para>
/// <para>
/// The <b>filtered</b> unique index on (<c>trn_Id</c>, <c>addonId</c>) is the database-level
/// guarantee that a booking carries at most one live line per add-on. The filter
/// (<c>is_deleted = 0</c>) is what keeps the resurrect path open (DEC-134, DEC-085, DEC-088).
/// </para>
/// </summary>
public class BookingAddOnConfiguration : IEntityTypeConfiguration<BookingAddOn>
{
    public void Configure(EntityTypeBuilder<BookingAddOn> builder)
    {
        builder.ToTable("BookingAddOn");

        builder.HasKey(e => e.BookingAddOnId);
        builder.Property(e => e.BookingAddOnId).HasColumnName("bookingAddOnId");

        builder.Property(e => e.TransactionId).HasColumnName("trn_Id");
        builder.Property(e => e.AddonId).HasColumnName("addonId");

        // The frozen values (BR-04): a later catalogue change never moves a stored booking.
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(150).IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.Unit).HasColumnName("unit").HasMaxLength(50);
        builder.Property(e => e.PricingBasis)
            .HasColumnName("pricingBasis")
            .HasConversion<int>()
            .IsRequired();

        // DEC-038: BaseEntity audit columns (the documented generic pattern).
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // The lines die with their booking (Cascade). The catalogue row is Restrict (M4).
        builder.HasOne(e => e.Booking)
            .WithMany(b => b.AddOns)
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.AddonDetail)
            .WithMany()
            .HasForeignKey(e => e.AddonId)
            .OnDelete(DeleteBehavior.Restrict);

        // DEC-088 / DEC-134: one LIVE line per (booking, add-on).
        builder.HasIndex(e => new { e.TransactionId, e.AddonId })
            .IsUnique()
            .HasFilter("[is_deleted] = 0")
            .HasDatabaseName("UX_BookingAddOn_Booking_Addon");

        builder.HasIndex(e => e.TransactionId)
            .HasDatabaseName("IX_BookingAddOn_Booking");
    }
}
