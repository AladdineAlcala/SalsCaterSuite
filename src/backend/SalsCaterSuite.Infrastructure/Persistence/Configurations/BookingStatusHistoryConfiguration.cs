using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

public class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {
        builder.ToTable("BookingStatusHistory");

        builder.HasKey(e => e.BookingStatusHistoryId);
        builder.Property(e => e.BookingStatusHistoryId)
            .HasColumnName("bookingStatusHistoryId")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.TransactionId).HasColumnName("trn_Id");
        builder.Property(e => e.StatusOrdinal).HasColumnName("statusOrdinal");
        builder.Property(e => e.StatusName).HasColumnName("statusName").HasMaxLength(50).IsRequired();
        builder.Property(e => e.PreviousStatusOrdinal).HasColumnName("previousStatusOrdinal");
        builder.Property(e => e.PreviousStatusName).HasColumnName("previousStatusName").HasMaxLength(50);
        builder.Property(e => e.ActorSource).HasColumnName("actorSource").HasMaxLength(50).IsRequired();
        builder.Property(e => e.EffectiveAtUtc).HasColumnName("effectiveAtUtc");
        builder.Property(e => e.RecordedAtUtc).HasColumnName("recordedAtUtc");

        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(e => e.TransactionId);

        builder.HasOne(e => e.Booking)
            .WithMany(b => b.StatusHistory)
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
