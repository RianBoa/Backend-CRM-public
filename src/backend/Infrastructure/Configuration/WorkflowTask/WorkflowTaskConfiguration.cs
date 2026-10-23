using backend.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Persistence.Configurations.Tasks
{
    public sealed class WorkflowTaskConfiguration : IEntityTypeConfiguration<WorkflowTask>
    {
        public void Configure(EntityTypeBuilder<WorkflowTask> builder)
        {
            builder.ToTable("tasks");

            // Primary key
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.ClientId);
            builder.HasIndex(x => x.SubscriptionId);
            builder.HasIndex(x => x.Status);
            builder.HasIndex(x => x.Type);
            builder.HasIndex(x => x.Priority);
            builder.HasIndex(x => x.AssignedTo);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.ClientId)
                .HasMaxLength(50);

            builder.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Type)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.Property(x => x.Priority)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.Property(x => x.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(WorkflowTaskStatus.Open);

            builder.Property(x => x.Deadline)
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.CompletedAt)
                .HasColumnType("timestamp with time zone");

            builder.Property(x => x.AssignedTo)
                .IsRequired(false);

            builder
                .HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.SetNull);

            builder
                .HasOne(x => x.Subscription)
                .WithMany()
                .HasForeignKey(x => x.SubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);

            builder
                .HasOne(x => x.AssignedUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
