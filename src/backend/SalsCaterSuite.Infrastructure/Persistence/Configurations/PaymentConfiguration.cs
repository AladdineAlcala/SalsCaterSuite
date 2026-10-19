using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payment");

        builder.HasKey(e => e.PaymentNo);
        builder.Property(e => e.PaymentNo).HasColumnName("payNo").HasMaxLength(50);

        // Non-nullable FK to Booking
        builder.Property(e => e.TransactionId).HasColumnName("trn_Id");

        builder.Property(e => e.DateOfPayment).HasColumnName("dateofPayment");
        builder.Property(e => e.Particular).HasColumnName("particular").HasMaxLength(150);

        // PaymentMeans stored as int (enum value). Legacy column 'pay_means' was nvarchar(50);
        // a migration is needed to change the column type.
        // PayType (legacy int) is replaced by the PaymentMeans enum.
        builder.Property(e => e.PaymentMeans)
            .HasColumnName("pay_means")
            .HasConversion<string>()  // keep as string for backward-compat with existing nvarchar(50) data
            .HasMaxLength(50);

        builder.Property(e => e.AmountPaid).HasColumnName("amtPay").HasPrecision(18, 2);
        builder.Property(e => e.CheckNo).HasColumnName("checkNo").HasMaxLength(50);
        builder.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);
        // DEC-038: all four BaseEntity audit columns are persisted. Payment keeps its
        // existing p_* convention; is_deleted follows the schema-wide is_deleted name
        // already used by Booking (the only pre-existing soft-delete flag).
        builder.Property(e => e.CreatedByUser).HasColumnName("p_createdbyUser").HasMaxLength(100);
        builder.Property(e => e.CreatedDate).HasColumnName("p_createdDate");
        builder.Property(e => e.UpdatedDate).HasColumnName("p_updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // PayType was a legacy magic-number int column — no longer in the entity.

        // PaymentId is not persisted — Payment PK is PaymentNo (string).
        builder.Ignore(e => e.PaymentId);
    }
}
