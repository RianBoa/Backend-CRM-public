using backend.Domain.LearningStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configuration.LearningStructure
{
    public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
    {
        public void Configure(EntityTypeBuilder<Group> builder)
        {
            builder.ToTable("groups");

            // PK
            builder.HasKey(x => x.Id);

            // Indexes
            builder.HasIndex(x => x.CohortId);
            builder.HasIndex(x => x.PackageId);
            builder.HasIndex(x => x.MentorId);
            builder.HasIndex(x => x.Name);

            // Link to Cohort
            builder
                .HasOne(x => x.Cohort)
                .WithMany(x => x.Groups)
                .HasForeignKey(x => x.CohortId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(x => x.Mentor)
                .WithMany()
                .HasForeignKey(x => x.MentorId)
                .OnDelete(DeleteBehavior.SetNull);

            // Link to Package (optional)
            builder
                .HasOne(x => x.Package)
                .WithMany()
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Field configurations
            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.CohortId)
                .IsRequired();

            builder.Property(x => x.MentorId)
                .HasColumnName("mentor_id")
                .IsRequired(false);

            builder.Property(x => x.PackageId)
                .IsRequired(false);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.LearningPlatformId)
                .HasMaxLength(100);

            builder.Property(x => x.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
