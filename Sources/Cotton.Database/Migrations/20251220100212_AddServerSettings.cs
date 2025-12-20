using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "server_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encryption_threads = table.Column<int>(type: "integer", nullable: false),
                    cipher_chunk_size_bytes = table.Column<int>(type: "integer", nullable: false),
                    max_chunk_size_bytes = table.Column<int>(type: "integer", nullable: false),
                    session_timeout_hours = table.Column<int>(type: "integer", nullable: false),
                    allow_cross_user_deduplication = table.Column<bool>(type: "boolean", nullable: false),
                    allow_global_indexing = table.Column<bool>(type: "boolean", nullable: false),
                    telemetry_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "server_settings");
        }
    }
}
