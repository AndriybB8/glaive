using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", tab =>
            tab.HasCheckConstraint("ck_users_status", "status IN ('Active', 'Inactive')"));

        builder.HasKey(u => u.Id).HasName("pk_users");
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_users_email");

        builder.Property(u => u.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);

        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
    }
}
