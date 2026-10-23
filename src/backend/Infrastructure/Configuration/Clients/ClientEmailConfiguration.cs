using backend.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.Clients
{
    public sealed class ClientEmailConfiguration : IEntityTypeConfiguration<ClientEmail>
    {
        public void Configure(EntityTypeBuilder<ClientEmail> builder)
        {
            builder.ToTable("client_emails");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ClientId)
                   .HasColumnName("client_id")
                   .IsRequired();

            builder.Property(x => x.IsPrimary)
                   .HasColumnName("is_primary")
                   .HasDefaultValue(false);

            builder.Property(x => x.CreatedAtUtc)
                   .HasColumnName("created_at_utc")
                   .IsRequired();

            builder.OwnsOne(x => x.Email, email =>
            {
                email.Property(v => v.Value)
                     .HasColumnName("email")
                     .HasMaxLength(150)
                     .IsRequired();

                email.HasIndex(v => v.Value)
                     .HasDatabaseName("ix_client_emails_email");
            });

            builder.HasIndex(x => new { x.ClientId, x.IsPrimary })
                   .HasDatabaseName("ix_client_emails_primary_per_client")
                   .HasFilter("\"is_primary\" = true")
                   .IsUnique();

        }
    }
}
