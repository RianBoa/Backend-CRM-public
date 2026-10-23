using backend.Domain.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Persistence.Configurations.Cases
{
    public sealed class CaseConfiguration : IEntityTypeConfiguration<Case>
    {
        public void Configure(EntityTypeBuilder<Case> builder)
        {
            builder.ToTable("cases");

            // Primary key
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.ClientId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => x.Type);

            builder
                .HasOne(x => x.Client)
                .WithMany(x => x.Cases)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.Currency)
                .IsRequired()
                .HasMaxLength(10)
                .HasDefaultValue("USD");

            builder.Property(x => x.ScreenshotUrl)
                .HasMaxLength(500);

            builder.Property(x => x.TelegramUrl)
                .HasMaxLength(500);

            builder.Property(x => x.Comment)
                .HasMaxLength(1000);

            builder.Property(x => x.IsFeatured)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(x => x.HasConsent)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(Case.CaseStatus.New);

            builder.Property(x => x.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(x => x.Type)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32);
        }
    }
}
