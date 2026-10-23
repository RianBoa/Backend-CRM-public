using backend.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configuration.Security;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.SecurityVersion).IsRequired();
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.ExpiresAtUtc).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(x => x.RevokedAtUtc).IsConcurrencyToken().HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ExpiresAtUtc);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
