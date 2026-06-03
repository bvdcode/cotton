using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Sync.App.State.Migrations
{
    /// <inheritdoc />
    public partial class InitialSyncAppState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_preferences",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    remembered_server_url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    remembered_username = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    start_with_operating_system = table.Column<bool>(type: "INTEGER", nullable: false),
                    start_minimized_to_tray = table.Column<bool>(type: "INTEGER", nullable: false),
                    enable_notifications = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_pair_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    local_root_path = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    remote_root_node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    remote_display_path = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    mode = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_pair_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_preferences");

            migrationBuilder.DropTable(
                name: "sync_pair_settings");
        }
    }
}
