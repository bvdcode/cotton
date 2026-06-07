using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Sync.App.State.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncPausedPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_sync_paused",
                table: "app_preferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_sync_paused",
                table: "app_preferences");
        }
    }
}
