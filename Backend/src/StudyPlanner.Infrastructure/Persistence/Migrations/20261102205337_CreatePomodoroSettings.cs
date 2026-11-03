using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatePomodoroSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pomodoro_settings",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_minutes = table.Column<int>(type: "integer", nullable: false),
                    short_break_minutes = table.Column<int>(type: "integer", nullable: false),
                    long_break_minutes = table.Column<int>(type: "integer", nullable: false),
                    sessions_before_long_break = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pomodoro_settings", x => x.user_id);
                    table.CheckConstraint("ck_pomodoro_settings_long_break_minutes", "long_break_minutes BETWEEN 1 AND 60");
                    table.CheckConstraint("ck_pomodoro_settings_sessions_before_long_break", "sessions_before_long_break BETWEEN 1 AND 10");
                    table.CheckConstraint("ck_pomodoro_settings_short_break_minutes", "short_break_minutes BETWEEN 1 AND 30");
                    table.CheckConstraint("ck_pomodoro_settings_work_minutes", "work_minutes BETWEEN 1 AND 120");
                    table.ForeignKey(
                        name: "fk_pomodoro_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pomodoro_settings");
        }
    }
}
