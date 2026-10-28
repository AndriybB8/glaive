using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkEntitiesToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "fk_note_groups_users_user_id",
                table: "note_groups",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_notes_users_author_id",
                table: "notes",
                column: "author_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_schedules_users_user_id",
                table: "schedules",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_users_user_id",
                table: "tasks",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_note_groups_users_user_id",
                table: "note_groups");

            migrationBuilder.DropForeignKey(
                name: "fk_notes_users_author_id",
                table: "notes");

            migrationBuilder.DropForeignKey(
                name: "fk_schedules_users_user_id",
                table: "schedules");

            migrationBuilder.DropForeignKey(
                name: "fk_tasks_users_user_id",
                table: "tasks");
        }
    }
}
