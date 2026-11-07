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
        // DEC-038: is_deleted is BIT NOT NULL DEFAULT 0 on every entity. The column was
        // created by InitialCreate without a default; the accompanying migration adds it.
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // ── Identity (DEC-016) ──────────────────────────────────────────────
        // BK-{yyyyMMdd}-{TransactionId:D5}; assigned after insert, never reused.
        builder.Property(e => e.BookingNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(e => e.BookingNumber)
            .IsUnique();

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
        builder.Property(e => e.NumberOfHeads).HasColumnName("numberOfHeads");
        builder.Property(e => e.Venue).HasColumnName("venue").HasMaxLength(200);
        builder.Property(e => e.EventDate).HasColumnName("eventDate");
        // DEC-178: overnight events store the end calendar date separately from the start date.
        builder.Property(e => e.EventEndDate).HasColumnName("eventEndDate").IsRequired();
        builder.Property(e => e.EventStartTime).HasColumnName("startTime");
        builder.Property(e => e.EventEndTime).HasColumnName("endTime");
        builder.Property(e => e.SpecialInstructions).HasColumnName("specialInstructions").HasMaxLength(500);

        // ── Dispatch Schedule (DEC-106) ──────────────────────────────────────
        // Two nullable time(0) columns: the kitchen call time and the pack-out time. They
        // are frozen at creation.
        builder.Property(e => e.CallTime).HasColumnName("callTime");
        builder.Property(e => e.PackoutTime).HasColumnName("packoutTime");

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

        // The frozen code of the applied discount (DEC-106, BR-02). It is what makes the
        // booking auditable — the amount was already stored.
        builder.Property(e => e.DiscountCode).HasColumnName("discountCode").HasMaxLength(50);

        // The frozen out-of-town surcharge (BR-26, DEC-116). It is a column of its own and is
        // NEVER inferred from Subtotal − PackageAmount: the subtotal conflates the surcharge
        // with the add-on total and the other charges, and ApplyExtendedAmount is a
        // creation-time switch rather than a money switch.
        builder.Property(e => e.ExtendedAreaAmount)
            .HasColumnName("extendedAreaAmount")
            .HasPrecision(18, 2)
            .IsRequired()
            .HasDefaultValue(0m);

        // The frozen add-on total (BR-44, DEC-151, DEC-158). Like the surcharge it is a column
        // of its own and never inferred from Subtotal: the stored amount is what the shared
        // calculator reads, so a later catalogue change can never move a stored booking.
        builder.Property(e => e.AddonsTotal)
            .HasColumnName("addonsTotal")
            .HasPrecision(18, 2)
            .IsRequired()
            .HasDefaultValue(0m);

        // Balance is a computed property — not stored in the database.
        builder.Ignore(e => e.Balance);

        // ── Status ───────────────────────────────────────────────────────────
        // Single Status enum replaces the legacy ServeStatus (bit) + IsCancelled (bit) pair.
        builder.Property(e => e.Status)
            .HasColumnName("booking_status")
            .HasConversion<int>()
            .HasDefaultValue(BookingStatus.Pending);

        // DEC-177: derived payment progress. Not a lifecycle status, and not written by BG-04.
        builder.Property(e => e.PaymentState)
            .HasColumnName("payment_state")
            .HasConversion<int>()
            .HasDefaultValue(PaymentState.PendingPayment);

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

        // The frozen line items (REQ-BKG-06 / 07). Their own configurations carry the column
        // mappings and the filtered unique indexes; the navigation is declared here so the
        // aggregate is loaded from the booking side.
        builder.HasMany(e => e.Menus)
            .WithOne(m => m.Booking)
            .HasForeignKey(m => m.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.AddOns)
            .WithOne(a => a.Booking)
            .HasForeignKey(a => a.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
