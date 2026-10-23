using backend.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.Clients
{
    public class ClientConfiguration : IEntityTypeConfiguration<Client>
    {
        public void Configure(EntityTypeBuilder<Client> builder)
        {
            builder.ToTable("clients");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name)
                   .HasColumnName("full_name")
                   .HasMaxLength(120)
                   .IsRequired();

            builder.Property(c => c.CreatedAtUtc)
                   .HasColumnName("created_at_utc");

            builder.Property(c => c.CreatedBy)
                   .HasMaxLength(100);

            builder.OwnsOne(c => c.Contacts, cinfo =>
            {
                cinfo.Property(x => x.PhoneNumber)
                     .HasColumnName("phone")
                     .HasMaxLength(32);

                cinfo.Property(x => x.Telegram)
                     .HasColumnName("telegram")
                     .HasMaxLength(64);

                cinfo.Property(x => x.Discord)
                     .HasColumnName("discord")
                     .HasMaxLength(64);

                cinfo.HasIndex(x => x.Telegram)
                     .HasDatabaseName("ix_clients_telegram");

                cinfo.HasIndex(x => x.Discord)
                     .HasDatabaseName("ix_clients_discord");

                cinfo.HasIndex(x => x.PhoneNumber)
                     .HasDatabaseName("ix_clients_phone");
            });

            builder.OwnsOne(c => c.Financials, f =>
            {
                f.Property(x => x.InitialBudget)
                 .HasColumnName("initial_budget")
                 .HasPrecision(12, 2)
                 .HasDefaultValue(0);

                f.Property(x => x.TotalEarnings)
                 .HasColumnName("total_earnings")
                 .HasPrecision(12, 2)
                 .HasDefaultValue(0);
            });

            builder.OwnsOne(c => c.AdditionalInfo, ai =>
            {
                ai.Property(x => x.Notes)
                  .HasColumnName("notes")
                  .HasMaxLength(2000);

                ai.Property(x => x.Source)
                  .HasColumnName("source")
                  .HasMaxLength(100);

                ai.Property(x => x.City)
                  .HasColumnName("city")
                  .HasMaxLength(200);

                ai.Property(x => x.BirthDate)
                  .HasColumnName("birth_date");

                ai.Property(x => x.Occupation)
                  .HasColumnName("occupation")
                  .HasMaxLength(200);

                ai.Property(x => x.PreferredLanguage)
                  .HasColumnName("preferred_language")
                  .HasMaxLength(10);

            });

            builder.HasIndex(c => c.Name)
                   .HasDatabaseName("ix_clients_full_name");

            builder.HasMany(c => c.Emails)
                   .WithOne(x => x.Client)
                   .HasForeignKey(x => x.ClientId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
