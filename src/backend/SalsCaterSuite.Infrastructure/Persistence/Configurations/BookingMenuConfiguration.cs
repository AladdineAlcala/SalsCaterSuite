using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

/// <summary>
/// The mapping of a booking's frozen dish selection (REQ-BKG-06, DEC-127, BR-35).
/// <para>
/// The column names are the data dictionary's: <c>bookingMenuId</c>, <c>trn_Id</c>,
/// <c>menuId</c>, <c>courseId</c> and <c>prepInstruction</c>. The row is a <b>catalogue
/// reference</b> — it stores no dish name, no price and no food cost, because the catalogue
/// owns those.
/// </para>
/// <para>
/// The <b>filtered</b> unique index on (<c>trn_Id</c>, <c>menuId</c>) is the database-level
/// guarantee that a booking carries at most one live row per dish. The filter
/// (<c>is_deleted = 0</c>) is what keeps the resurrect path open: a dish that is re-chosen after
/// being dropped resurrects its soft-deleted row instead of inserting a second one (DEC-134,
/// DEC-085, DEC-088).
/// </para>
/// </summary>
public class BookingMenuConfiguration : IEntityTypeConfiguration<BookingMenu>
{
    public void Configure(EntityTypeBuilder<BookingMenu> builder)
    {
        builder.ToTable("BookingMenu");

        builder.HasKey(e => e.BookingMenuId);
        builder.Property(e => e.BookingMenuId).HasColumnName("bookingMenuId");

        builder.Property(e => e.TransactionId).HasColumnName("trn_Id");
        builder.Property(e => e.MenuId).HasColumnName("menuId").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CourseId).HasColumnName("courseId");

        // The chef's per-event instruction (DEC-146). It lives on the selection row, never on
        // the catalogue, so one booking's note can never reach another's sheet.
        builder.Property(e => e.PrepInstruction).HasColumnName("prepInstruction").HasMaxLength(500);

        // DEC-038: BaseEntity audit columns (the documented generic pattern).
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // The selection dies with its booking (Cascade). The catalogue rows are Restrict: a
        // dish or a course that a live selection references may not be deleted out from under
        // it (M4).
        builder.HasOne(e => e.Booking)
            .WithMany(b => b.Menus)
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Menu)
            .WithMany()
            .HasForeignKey(e => e.MenuId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CourseCategory)
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // DEC-088 / DEC-134: one LIVE row per (booking, dish). The filtered predicate keeps the
        // resurrect path open.
        builder.HasIndex(e => new { e.TransactionId, e.MenuId })
            .IsUnique()
            .HasFilter("[is_deleted] = 0")
            .HasDatabaseName("UX_BookingMenu_Booking_Menu");

        builder.HasIndex(e => e.TransactionId)
            .HasDatabaseName("IX_BookingMenu_Booking");
    }
}
