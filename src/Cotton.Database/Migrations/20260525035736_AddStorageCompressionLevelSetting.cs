using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageCompressionLevelSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "compression_level",
                table: "server_settings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE server_settings SET integrity_mac = NULL, integrity_version = NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "compression_level",
                table: "server_settings");

            migrationBuilder.Sql("UPDATE server_settings SET integrity_mac = NULL, integrity_version = NULL;");
        }
    }
}
