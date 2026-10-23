using backend.Domain.LearningStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configuration.LearningStructure
{
    public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
    {
        public void Configure(EntityTypeBuilder<Enrollment> builder)
        {
            builder.ToTable("enrollments");

            // PK
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Version).IsConcurrencyToken().ValueGeneratedNever();

            builder.HasIndex(x => x.OrderId);
            builder.HasIndex(x => x.OrderItemId);
            builder.HasIndex(x => x.CohortId);
            builder.HasIndex(x => x.GroupId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.EnrolledAt);
            builder.HasIndex(x => x.IsAddedToChat);
            builder.HasIndex(x => x.IsAddedToPlatform);

            builder
                .HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(x => x.OrderItem)
                .WithMany()
                .HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder
                .HasOne(x => x.Cohort)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.CohortId)
                .OnDelete(DeleteBehavior.SetNull);

            builder
                .HasOne(x => x.Group)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.OrderId)
                .IsRequired();

            builder.Property(x => x.CohortId)
                .IsRequired(false);

            builder.Property(x => x.GroupId)
                .IsRequired(false);

            builder.Property(x => x.DesiredStartDate)
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.EnrolledAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(x => x.IsAddedToChat)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(x => x.AddedToChatAt)
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.IsAddedToPlatform)
                .IsRequired(false)
                .HasDefaultValue(false);

            builder.Property(x => x.AddedToPlatformAt)
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32);
        }
    }
}
