using backend.Domain.CuratorNote;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.CuratorNotes
{
    public class CuratorNoteConfiguration : IEntityTypeConfiguration<CuratorNote>
    {
        public void Configure(EntityTypeBuilder<CuratorNote> builder)
        {
            builder.ToTable("curator_notes");

            // Primary Key
            builder.HasKey(n => n.Id);

            // Foreign Keys
            builder.Property(n => n.TypeId)
                   .HasColumnName("type_id")
                   .IsRequired();

            builder.Property(n => n.ClientId)
                   .HasColumnName("client_id")
                   .IsRequired();

            builder.Property(n => n.CreatedById)
                   .HasColumnName("created_by_id")
                   .IsRequired();

            // Navigation
            builder.HasOne(n => n.Type)
                   .WithMany()
                   .HasForeignKey(n => n.TypeId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(n => n.Client)
                   .WithMany()
                   .HasForeignKey(n => n.ClientId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(n => n.CreatedBy)
                   .WithMany()
                   .HasForeignKey(n => n.CreatedById)
                   .OnDelete(DeleteBehavior.Restrict);

            // Properties
            builder.Property(n => n.Comment)
                   .HasColumnName("comment")
                   .HasMaxLength(2000)
                   .IsRequired();

            builder.Property(n => n.CreatedAt)
                   .HasColumnName("created_at")
                   .IsRequired();

            // Indexes
            builder.HasIndex(n => n.ClientId);
            builder.HasIndex(n => n.TypeId);
            builder.HasIndex(n => n.CreatedById);
        }
    }
}
