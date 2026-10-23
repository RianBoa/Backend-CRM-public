using backend.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.Orders
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("payments");

            // === Primary Key ===
            builder.HasKey(p => p.Id);

            // === Foreign Key ===
            builder.Property(p => p.OrderId)
                   .HasColumnName("order_id")
                   .IsRequired();

            builder.HasOne(p => p.Order)
                   .WithMany(o => o.Payments)
                   .HasForeignKey(p => p.OrderId)
                   .OnDelete(DeleteBehavior.Cascade);

            // === Financial Fields ===
            builder.Property(p => p.Amount)
                   .HasColumnName("amount")
                   .HasPrecision(12, 2)
                   .IsRequired();

            builder.Property(p => p.Currency)
                   .HasColumnName("currency")
                   .HasMaxLength(10)
                   .IsRequired();

            builder.Property(p => p.AmountUA)
                   .HasColumnName("amount_ua")
                   .HasPrecision(12, 2);

            builder.Property(p => p.ExchangeRate)
                   .HasColumnName("exchange_rate")
                   .HasPrecision(12, 4);

            // === Provider / Invoice ===
            builder.Property(p => p.Provider)
                   .HasColumnName("provider")
                   .HasMaxLength(100);

            builder.Property(p => p.Invoice)
                   .HasColumnName("invoice")
                   .HasMaxLength(100);

            // === Installments ===
            builder.Property(p => p.TotalInstallments)
                   .HasColumnName("total_installments")
                   .IsRequired();

            builder.Property(p => p.CurrentInstallment)
                   .HasColumnName("current_installment");

            // === Status and Dates ===
            builder.Property(p => p.Status)
                   .HasColumnName("status")
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(p => p.PaidAt)
                   .HasColumnName("paid_at");

            builder.Property(p => p.PaidUntil)
                   .HasColumnName("paid_until");

            // === Indexes ===
            builder.HasIndex(p => p.OrderId);
            builder.HasIndex(p => p.Status);
            builder.HasIndex(p => p.PaidAt);
        }
    }
}
