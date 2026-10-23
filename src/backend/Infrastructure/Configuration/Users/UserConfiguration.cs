using backend.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configuration.Users
{
    public sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("users");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.SecurityVersion).IsConcurrencyToken();

            builder.HasIndex(x => x.Email).IsUnique();
            builder.HasIndex(x => x.TelegramUsername).IsUnique();
            builder.HasIndex(x => x.Role);
            builder.HasIndex(x => x.IsActive);
            builder.HasIndex(x => x.CreatedAt);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.FullName)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.TelegramUsername)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.TelegramWork)
                .HasMaxLength(100);

            builder.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Role)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Ignore(x => x.ShortId);
        }
    }
}
