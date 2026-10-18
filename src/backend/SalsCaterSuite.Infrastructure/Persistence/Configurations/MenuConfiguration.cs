using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("Menu");

        builder.HasKey(e => e.MenuId);
        builder.Property(e => e.MenuId).HasColumnName("menuid").HasMaxLength(50);

        builder.Property(e => e.CourseId).HasColumnName("courseId");
        builder.Property(e => e.MenuName).HasColumnName("menu_name").HasMaxLength(150);
        builder.Property(e => e.DeptId).HasColumnName("deptId");
        builder.Property(e => e.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(e => e.Price).HasColumnName("price").HasPrecision(18, 2);
        builder.Property(e => e.FoodCost).HasColumnName("food_cost").HasPrecision(18, 2).HasDefaultValue(0.00m);
        builder.Property(e => e.Image).HasColumnName("image").HasMaxLength(250);
        builder.Property(e => e.DateAdded).HasColumnName("date_added");
        builder.Property(e => e.DateUpdated).HasColumnName("date_update");

        builder.HasOne(e => e.CourseCategory)
            .WithMany(c => c.Menus)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

