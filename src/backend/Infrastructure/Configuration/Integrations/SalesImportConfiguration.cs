using backend.Domain.Integrations;
using backend.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configuration.Integrations;

public sealed class SalesOrderLinkConfiguration : IEntityTypeConfiguration<SalesOrderLink>
{
    public void Configure(EntityTypeBuilder<SalesOrderLink> b)
    {
        b.ToTable("sales_order_links");
        b.HasKey(x => new { x.Source, x.DealId });
        b.Property(x => x.Source).HasMaxLength(50);
        b.Property(x => x.DealId).HasMaxLength(19);
        b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        b.Property(x => x.PurchasedAt).HasColumnType("timestamp with time zone");
        b.Property(x => x.ItemsFingerprint).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.OrderId).IsUnique();
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SalesPaymentLinkConfiguration : IEntityTypeConfiguration<SalesPaymentLink>
{
    public void Configure(EntityTypeBuilder<SalesPaymentLink> b)
    {
        b.ToTable("sales_payment_links");
        b.HasKey(x => new { x.Source, x.ExternalId });
        b.Property(x => x.Source).HasMaxLength(50);
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.PaymentId).IsUnique();
        b.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SalesImportReceiptConfiguration : IEntityTypeConfiguration<SalesImportReceipt>
{
    public void Configure(EntityTypeBuilder<SalesImportReceipt> b)
    {
        b.ToTable("sales_import_receipts");
        b.HasKey(x => new { x.Source, x.EventId });
        b.Property(x => x.Source).HasMaxLength(50);
        b.Property(x => x.EventId).HasMaxLength(100);
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.ResponseJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ReceivedAt).HasColumnType("timestamp with time zone");
    }
}
