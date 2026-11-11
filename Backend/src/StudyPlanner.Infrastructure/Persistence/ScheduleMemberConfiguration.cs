using DefaultNamespace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Schedules;
using StudyPlanner.Domain.Users;

namespace StudyPlanner.Infrastructure.Persistence;

internal sealed class ScheduleMemberConfiguration : IEntityTypeConfiguration<ScheduleMember>
{
    public void Configure(EntityTypeBuilder<ScheduleMember> builder)
    {
        builder.ToTable("schedule_members", table =>
            table.HasCheckConstraint("ck_schedule_members_role", "role IN ('Viewer', 'Editor')"));

        builder.HasKey(m => new { m.ScheduleId, m.UserId }).HasName("pk_schedule_members");

        builder.Property(m => m.ScheduleId).HasColumnName("schedule_id");
        builder.HasOne<Schedule>()
            .WithMany()
            .HasForeignKey(m => m.ScheduleId)
            .HasConstraintName("fk_schedule_members_schedules_schedule_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(m => m.UserId).HasColumnName("user_id");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .HasConstraintName("fk_schedule_members_users_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.UserId).HasDatabaseName("ix_schedule_members_user_id");

        builder.Property(m => m.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.AcceptedAt).HasColumnName("accepted_at");
    }
}
