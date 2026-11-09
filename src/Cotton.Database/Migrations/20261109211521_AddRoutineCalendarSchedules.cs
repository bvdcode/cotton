using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutineCalendarSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "calendar_settings",
                table: "routines",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "schedule_kind",
                table: "routines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "start_at",
                table: "routines",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "calendar_settings",
                table: "routines");

            migrationBuilder.DropColumn(
                name: "schedule_kind",
                table: "routines");

            migrationBuilder.DropColumn(
                name: "start_at",
                table: "routines");
        }
    }
}
