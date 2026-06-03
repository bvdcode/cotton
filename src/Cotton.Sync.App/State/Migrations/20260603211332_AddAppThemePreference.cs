using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Sync.App.State.Migrations
{
    /// <inheritdoc />
    public partial class AddAppThemePreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "theme_mode",
                table: "app_preferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "theme_mode",
                table: "app_preferences");
        }
    }
}
