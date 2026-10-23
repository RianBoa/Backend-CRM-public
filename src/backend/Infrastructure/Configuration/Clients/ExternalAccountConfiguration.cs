using backend.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Persistence.Configurations.Clients
{
    public sealed class ExternalAccountConfiguration : IEntityTypeConfiguration<ExternalAccount>
    {
        public void Configure(EntityTypeBuilder<ExternalAccount> builder)
        {
            builder.ToTable("external_accounts");

            // Primary Key
            builder.HasKey(x => x.Ulid);

            builder.HasIndex(x => new { x.ClientId, x.Provider }).IsUnique();
            builder.HasIndex(x => x.ExternalId);
            builder.HasIndex(x => new { x.Provider, x.ExternalId })
                .HasDatabaseName("ux_external_accounts_provider_external_id").IsUnique();

            // Свойства
            builder.Property(x => x.Ulid)
                .ValueGeneratedNever();

            builder.Property(x => x.ClientId)
                .IsRequired();

            builder.Property(x => x.Provider)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.ExternalId)
                .IsRequired()
                .HasMaxLength(100);

            builder
                .HasOne<Client>()
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
