using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTagsToSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tags_users_user_id",
                table: "tags");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "tags",
                newName: "schedule_id");

            migrationBuilder.RenameIndex(
                name: "ux_tags_user_id_name",
                table: "tags",
                newName: "ux_tags_schedule_id_name");

            migrationBuilder.AddForeignKey(
                name: "fk_tags_schedules_schedule_id",
                table: "tags",
                column: "schedule_id",
                principalTable: "schedules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tags_schedules_schedule_id",
                table: "tags");

            migrationBuilder.RenameColumn(
                name: "schedule_id",
                table: "tags",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "ux_tags_schedule_id_name",
                table: "tags",
                newName: "ux_tags_user_id_name");

            migrationBuilder.AddForeignKey(
                name: "fk_tags_users_user_id",
                table: "tags",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
