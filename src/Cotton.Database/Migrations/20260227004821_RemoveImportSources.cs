using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveImportSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "import_source",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "webdav_host",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "webdav_password_encrypted",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "webdav_username",
                table: "server_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "import_source",
                table: "server_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "webdav_host",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "webdav_password_encrypted",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "webdav_username",
                table: "server_settings",
                type: "text",
                nullable: true);
        }
    }
}
