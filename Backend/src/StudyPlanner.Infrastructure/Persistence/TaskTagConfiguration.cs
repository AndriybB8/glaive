using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPlanner.Domain.Tags;
using StudyPlanner.Domain.Tasks;
using StudyPlanner.Domain.TaskTags;

namespace StudyPlanner.Infrastructure.Persistence;

public class TaskTagConfiguration: IEntityTypeConfiguration<TaskTag>
{
    public void Configure(EntityTypeBuilder<TaskTag> builder)
    {
        builder.ToTable("task_tags");

        builder.HasKey(tt => new { tt.TaskId, tt.TagId }).HasName("pk_task_tags");

        builder.Property(tt => tt.TaskId).HasColumnName("task_id");
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(tt => tt.TaskId)
            .HasConstraintName("fk_task_tags_tasks_task_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(tt => tt.TagId).HasColumnName("tag_id");
        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(tt => tt.TagId)
            .HasConstraintName("fk_task_tags_tags_tag_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tt => tt.TagId).HasDatabaseName("ix_task_tags_tag_id");
    }
}
