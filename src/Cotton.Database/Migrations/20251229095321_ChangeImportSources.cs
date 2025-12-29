using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChangeImportSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "import_sources",
                table: "server_settings");

            migrationBuilder.AddColumn<int>(
                name: "import_source",
                table: "server_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "import_source",
                table: "server_settings");

            migrationBuilder.AddColumn<int[]>(
                name: "import_sources",
                table: "server_settings",
                type: "integer[]",
                nullable: false,
                defaultValue: Array.Empty<int>());
        }
    }
}
