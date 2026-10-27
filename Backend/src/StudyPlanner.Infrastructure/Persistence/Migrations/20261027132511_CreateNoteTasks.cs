using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateNoteTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "note_tasks",
                columns: table => new
                {
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_note_tasks", x => new { x.note_id, x.task_id });
                    table.ForeignKey(
                        name: "fk_note_tasks_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_note_tasks_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_note_tasks_task_id",
                table: "note_tasks",
                column: "task_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "note_tasks");
        }
    }
}
