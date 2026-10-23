using backend.Domain.CuratorNote;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Persistence.Configurations.CuratorNotes
{
    public sealed class NoteTypeConfiguration : IEntityTypeConfiguration<NoteType>
    {
        public void Configure(EntityTypeBuilder<NoteType> builder)
        {
            builder.ToTable("note_types");

            // Primary Key
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.Category);
            builder.HasIndex(x => x.Name).IsUnique();

            builder.Property(x => x.Id)
                .ValueGeneratedOnAdd();

            builder.Property(x => x.Category)
                .IsRequired()
                .HasConversion<string>()  // enum NoteCategory → string
                .HasMaxLength(32);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);
        }
    }
}
