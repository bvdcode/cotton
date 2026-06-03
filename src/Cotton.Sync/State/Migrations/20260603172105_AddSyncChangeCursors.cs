using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Sync.State.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncChangeCursors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_change_cursors",
                columns: table => new
                {
                    sync_pair_id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    last_cursor = table.Column<long>(type: "INTEGER", nullable: false),
                    cursor_expired = table.Column<bool>(type: "INTEGER", nullable: false),
                    earliest_available_cursor = table.Column<long>(type: "INTEGER", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_change_cursors", x => x.sync_pair_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_change_cursors");
        }
    }
}
