using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddLatestReleaseNotificationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "latest_release_checked_at",
                table: "app_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "latest_release_notified_at",
                table: "app_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "latest_release_url",
                table: "app_versions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "latest_release_version",
                table: "app_versions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "latest_release_checked_at",
                table: "app_versions");

            migrationBuilder.DropColumn(
                name: "latest_release_notified_at",
                table: "app_versions");

            migrationBuilder.DropColumn(
                name: "latest_release_url",
                table: "app_versions");

            migrationBuilder.DropColumn(
                name: "latest_release_version",
                table: "app_versions");
        }
    }
}
