using backend.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.Orders
{
    public class PromoBenefitRuleConfiguration : IEntityTypeConfiguration<PromoBenefitRule>
    {
        public void Configure(EntityTypeBuilder<PromoBenefitRule> builder)
        {
            builder.ToTable("promo_benefit_rules");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.RuleCode)
                   .HasColumnName("rule_code")
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(p => p.Name)
                   .HasColumnName("name")
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(p => p.Description)
                   .HasColumnName("description")
                   .HasMaxLength(1000)
                   .IsRequired();

            builder.Property(p => p.IsActive)
                   .HasColumnName("is_active")
                   .IsRequired();

            builder.Property(p => p.Type)
                   .HasColumnName("type")
                   .HasConversion<string>()
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(p => p.DiscountPercentage)
                   .HasColumnName("discount_percentage")
                   .HasPrecision(5, 2);

            builder.Property(p => p.DiscountFixedAmount)
                   .HasColumnName("discount_fixed_amount")
                   .HasPrecision(12, 2);

            builder.Property(p => p.EffectDays)
                   .HasColumnName("effect_days");

            builder.Property(p => p.StartAt)
                   .HasColumnName("start_at");

            builder.Property(p => p.EndAt)
                   .HasColumnName("end_at");

            builder.Property(p => p.ProductId)
                   .HasColumnName("product_id");

            builder.Property(p => p.PackageId)
                   .HasColumnName("package_id");

            builder.Property(p => p.BonusProductId)
                   .HasColumnName("bonus_product_id");

            builder.Property(p => p.BonusPackageId)
                   .HasColumnName("bonus_package_id");

            builder.HasOne(p => p.Product)
                   .WithMany()
                   .HasForeignKey(p => p.ProductId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Package)
                   .WithMany()
                   .HasForeignKey(p => p.PackageId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.BonusProduct)
                   .WithMany()
                   .HasForeignKey(p => p.BonusProductId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.BonusPackage)
                   .WithMany()
                   .HasForeignKey(p => p.BonusPackageId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(p => p.RuleCode)
                   .IsUnique()
                   .HasDatabaseName("ix_promo_benefit_rules_rule_code");

            builder.HasIndex(p => p.IsActive);
            builder.HasIndex(p => p.Type);
            builder.HasIndex(p => p.StartAt);
        }
    }
}
