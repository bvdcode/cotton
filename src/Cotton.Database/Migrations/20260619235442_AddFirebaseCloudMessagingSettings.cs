using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFirebaseCloudMessagingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fcm_project_id",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fcm_service_account_json_encrypted",
                table: "server_settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fcm_project_id",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "fcm_service_account_json_encrypted",
                table: "server_settings");
        }
    }
}
