using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateCalendarEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    is_all_day = table.Column<bool>(type: "boolean", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recurrence_frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recurrence_until = table.Column<DateOnly>(type: "date", nullable: true),
                    series_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_events", x => x.id);
                    table.CheckConstraint("ck_calendar_events_color", "color ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("ck_calendar_events_date_range", "end_date >= start_date");
                    table.CheckConstraint("ck_calendar_events_exception_shape", "(series_id IS NULL AND original_date IS NULL AND NOT is_cancelled) OR (series_id IS NOT NULL AND original_date IS NOT NULL AND recurrence_frequency = 'None')");
                    table.CheckConstraint("ck_calendar_events_recurrence_frequency", "recurrence_frequency IN ('None', 'Daily', 'Weekly', 'Monthly')");
                    table.CheckConstraint("ck_calendar_events_recurrence_until", "recurrence_until IS NULL OR recurrence_frequency <> 'None'");
                    table.CheckConstraint("ck_calendar_events_time_range", "ends_at > starts_at");
                    table.CheckConstraint("ck_calendar_events_time_shape", "(is_all_day AND start_date IS NOT NULL AND end_date IS NOT NULL AND starts_at IS NULL AND ends_at IS NULL) OR (NOT is_all_day AND starts_at IS NOT NULL AND ends_at IS NOT NULL AND start_date IS NULL AND end_date IS NULL)");
                    table.ForeignKey(
                        name: "fk_calendar_events_calendar_events_series_id",
                        column: x => x.series_id,
                        principalTable: "calendar_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calendar_events_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_user_id",
                table: "calendar_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_calendar_events_series_id_original_date",
                table: "calendar_events",
                columns: new[] { "series_id", "original_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_events");
        }
    }
}
