using backend.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.Applications
{
    public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
    {
        public void Configure(EntityTypeBuilder<Application> builder)
        {
            builder.ToTable("applications");

            // Primary Key
            builder.HasKey(a => a.Id);

            // Foreign Keys
            builder.Property(a => a.TypeId)
                   .HasColumnName("type_id")
                   .IsRequired();

            builder.Property(a => a.AuthorId)
                   .HasColumnName("author_id")
                   .IsRequired();

            builder.Property(a => a.ClientId)
                   .HasColumnName("client_id");

            builder.Property(a => a.OrderId)
                   .HasColumnName("order_id");

            // Navigation
            builder.HasOne(a => a.Type)
                   .WithMany()
                   .HasForeignKey(a => a.TypeId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.Author)
                   .WithMany()
                   .HasForeignKey(a => a.AuthorId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.Client)
                   .WithMany()
                   .HasForeignKey(a => a.ClientId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(a => a.Order)
                   .WithMany()
                   .HasForeignKey(a => a.OrderId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(a => a.ProcessedBy)
                   .WithMany()
                   .HasForeignKey(a => a.ProcessedById)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.Property(a => a.ProcessedById).HasColumnName("processed_by_id");
            builder.Property(a => a.Revision).HasColumnName("revision").IsConcurrencyToken();

            // Properties
            builder.Property(a => a.Title)
                   .HasColumnName("title")
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(a => a.Description)
                   .HasColumnName("description")
                   .HasMaxLength(2000);

            builder.Property(a => a.Reason)
                   .HasColumnName("reason")
                   .HasConversion<string>()
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(a => a.Status)
                   .HasColumnName("status")
                   .HasConversion<string>()
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(a => a.CreatedAt)
                   .HasColumnName("created_at")
                   .IsRequired();

            builder.Property(a => a.ProcessedAt)
                   .HasColumnName("processed_at");

            // Indexes
            builder.HasIndex(a => a.Status);
            builder.HasIndex(a => a.Reason);
            builder.HasIndex(a => a.TypeId);
            builder.HasIndex(a => a.AuthorId);
            builder.HasIndex(a => a.ClientId);
        }
    }
}
