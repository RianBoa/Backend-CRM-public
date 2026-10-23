using backend.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Persistence.Configurations.Orders
{
    public sealed class AppliedBenefitConfiguration : IEntityTypeConfiguration<AppliedBenefit>
    {
        public void Configure(EntityTypeBuilder<AppliedBenefit> builder)
        {
            builder.ToTable("applied_benefits");

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.OrderId);
            builder.HasIndex(x => x.OrderItemId);
            builder.HasIndex(x => x.PromoBenefitRuleId);

            builder
                .HasOne(x => x.Order)
                .WithMany(x => x.AppliedBenefits)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(x => x.OrderItem)
                .WithMany()
                .HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(x => x.PromoBenefitRule)
                .WithMany(x => x.AppliedBenefits)
                .HasForeignKey(x => x.PromoBenefitRuleId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.Id)
                .IsRequired()
                .ValueGeneratedNever();

            builder.Property(x => x.OrderId)
                .IsRequired();

            builder.Property(x => x.OrderItemId)
                .IsRequired();

            builder.Property(x => x.PromoBenefitRuleId)
                .IsRequired();
        }
    }
}
