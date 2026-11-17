using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Files;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

public class UploadedFileConfiguration: IEntityTypeConfiguration<UploadedFile>
{
    public void Configure(EntityTypeBuilder<UploadedFile> builder)
    {
        builder.ToTable("files", table =>
            table.HasCheckConstraint("ck_files_content_hash_length", "length(content_hash) = 64"));

        builder.HasKey(f => f.Id).HasName("pk_files");
        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(f => f.OwnerId).HasColumnName("owner_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.OwnerId)
            .HasConstraintName("fk_files_users_owner_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.OwnerId).HasDatabaseName("ix_files_owner_id");

        builder.HasIndex(f => new { f.OwnerId, f.ContentHash }).HasDatabaseName("ix_files_owner_id_content_hash");

        builder.Property(f => f.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(UploadedFile.MaxFileNameLength)
            .IsRequired();

        builder.Property(f => f.StoragePath).HasColumnName("storage_path").IsRequired();

        builder.Property(f => f.ContentType).HasColumnName("content_type").HasMaxLength(200).IsRequired();

        builder.Property(f => f.SizeBytes).HasColumnName("size_bytes");

        builder.Property(f => f.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();

        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        builder.Property(f => f.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
