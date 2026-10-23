using backend.Domain.LearningStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configuration.LearningStructure
{
    public sealed class CohortConfiguration : IEntityTypeConfiguration<Cohort>
    {
        public void Configure(EntityTypeBuilder<Cohort> builder)
        {
            builder.ToTable("cohorts");

            // Primary Key
            builder.HasKey(x => x.Id);

            // Indexes
            builder.HasIndex(x => x.ProductId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.StartAt);

            // Likns with Products
            builder
                .HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Likns with Groups
            builder
                .HasMany(x => x.Groups)
                .WithOne(x => x.Cohort)
                .HasForeignKey(x => x.CohortId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relation with Enrollments
            builder
                .HasMany(x => x.Enrollments)
                .WithOne(x => x.Cohort)
                .HasForeignKey(x => x.CohortId)
                .OnDelete(DeleteBehavior.Cascade);

            // Properties
            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.ProductId)
                .IsRequired(false);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.StartAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.EndsAt)
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.Status)
                .IsRequired()
                .HasConversion<string>() // enum -> string
                .HasMaxLength(32);

            builder.Property(x => x.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
