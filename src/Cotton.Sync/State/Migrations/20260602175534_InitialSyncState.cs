using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Sync.State.Migrations
{
    /// <inheritdoc />
    public partial class InitialSyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sync_pair_id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    relative_path_key = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    relative_path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    kind = table.Column<int>(type: "INTEGER", nullable: false),
                    local_content_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    local_last_write_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    remote_node_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    remote_file_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    remote_content_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    remote_etag = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    synced_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_entries_remote_file_id",
                table: "sync_entries",
                column: "remote_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_sync_entries_remote_node_id",
                table: "sync_entries",
                column: "remote_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_sync_entries_sync_pair_id_relative_path_key",
                table: "sync_entries",
                columns: new[] { "sync_pair_id", "relative_path_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_entries");
        }
    }
}
