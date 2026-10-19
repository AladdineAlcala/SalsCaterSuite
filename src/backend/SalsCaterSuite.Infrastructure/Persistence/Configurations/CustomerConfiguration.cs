using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customer");

        builder.HasKey(e => e.CustomerId);
        builder.Property(e => e.CustomerId).HasColumnName("c_Id");

        builder.Property(e => e.LastName).HasColumnName("lastname").HasMaxLength(100);
        builder.Property(e => e.FirstName).HasColumnName("firstname").HasMaxLength(100);
        builder.Property(e => e.Middle).HasColumnName("middle").HasMaxLength(50);
        builder.Property(e => e.Address).HasColumnName("address").HasMaxLength(250);
        builder.Property(e => e.Contact1).HasColumnName("contact1").HasMaxLength(50);
        builder.Property(e => e.Contact2).HasColumnName("contact2").HasMaxLength(50);
        builder.Property(e => e.DateRegistered).HasColumnName("datereg");
        builder.Property(e => e.Company).HasColumnName("company").HasMaxLength(150);

        // DEC-038: BaseEntity audit columns are persisted for every domain entity.
        // Customer has no pre-existing audit naming convention, so the documented
        // generic pattern is used (and is shared with Menu, Refund, Reservation,
        // CourseCategory, Area and AddonDetail).
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Ignore(e => e.FullName);
    }
}

