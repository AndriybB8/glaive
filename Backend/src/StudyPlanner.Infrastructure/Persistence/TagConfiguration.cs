using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Tags;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");

        builder.HasKey(t => t.Id).HasName("pk_tags");
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.Name).HasColumnName("user_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .HasConstraintName("fk_tags_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(Tag.NameMaxLength).IsRequired();

        builder.HasIndex(t => new {t.UserId, t.Name}).IsUnique().HasDatabaseName("ux_tags_user_id_name");

        builder.Property(t => t.Color).HasColumnName("color").HasMaxLength(7).IsRequired();

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.Property(t => t.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
