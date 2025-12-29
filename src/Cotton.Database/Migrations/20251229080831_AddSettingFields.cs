using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "compution_mode",
                table: "server_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "email_mode",
                table: "server_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int[]>(
                name: "import_sources",
                table: "server_settings",
                type: "integer[]",
                nullable: false,
                defaultValue: Array.Empty<int>());

            migrationBuilder.AddColumn<int[]>(
                name: "server_usage",
                table: "server_settings",
                type: "integer[]",
                nullable: false,
                defaultValue: Array.Empty<int>());

            migrationBuilder.AddColumn<int>(
                name: "storage_space_mode",
                table: "server_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "storage_type",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "compution_mode",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "email_mode",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "import_sources",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "server_usage",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "storage_space_mode",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "storage_type",
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
    }
}
