using backend.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Configurations.Applications;

public sealed class ApplicationReplyConfiguration : IEntityTypeConfiguration<ApplicationReply>
{
    public void Configure(EntityTypeBuilder<ApplicationReply> builder)
    {
        builder.ToTable("application_replies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.ApplicationId, x.CreatedAt, x.Id });
        builder.HasOne(x => x.Application).WithMany(x => x.Replies)
            .HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}
