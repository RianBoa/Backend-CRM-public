using backend.Domain.RoadMaps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.RoadMaps
{
    public class RoadMapAssignmentConfiguration : IEntityTypeConfiguration<RoadMapAssignment>
    {
        public void Configure(EntityTypeBuilder<RoadMapAssignment> builder)
        {
            builder.ToTable("roadmap_assignments");

            // Primary Key
            builder.HasKey(a => a.Id);

            // Foreign Keys
            builder.Property(a => a.RoadMapItemId)
                   .HasColumnName("roadmap_item_id")
                   .IsRequired();

            builder.Property(a => a.EnrollmentId)
                   .HasColumnName("enrollment_id")
                   .IsRequired();

            builder.Property(a => a.UserId)
                   .HasColumnName("user_id")
                   .IsRequired();

            // Relationships
            builder.HasOne(a => a.RoadMapItem)
                   .WithMany()
                   .HasForeignKey(a => a.RoadMapItemId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.Enrollment)
                   .WithMany()
                   .HasForeignKey(a => a.EnrollmentId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.AssignedBy)
                   .WithMany()
                   .HasForeignKey(a => a.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Properties
            builder.Property(a => a.Status)
                   .IsConcurrencyToken()
                   .HasColumnName("status")
                   .HasConversion<string>()
                   .HasMaxLength(32)
                   .IsRequired();

            builder.Property(a => a.DueAtDaysFromEnrollment)
                   .HasColumnName("due_at_days_from_enrollment")
                   .IsRequired();

            builder.Property(a => a.AssignedAt)
                   .HasColumnName("assigned_at")
                   .IsRequired();

            // Indexes
            builder.HasIndex(a => a.RoadMapItemId);
            builder.HasIndex(a => a.EnrollmentId);
            builder.HasIndex(a => a.Status);
            builder.HasIndex(a => a.UserId);
        }
    }
}
