using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFolderName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_file_manifests_owner_id_folder",
                table: "file_manifests");

            migrationBuilder.DropIndex(
                name: "IX_file_manifests_owner_id_folder_name",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "folder",
                table: "file_manifests");

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_owner_id",
                table: "file_manifests",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_file_manifests_owner_id",
                table: "file_manifests");

            migrationBuilder.AddColumn<string>(
                name: "folder",
                table: "file_manifests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_owner_id_folder",
                table: "file_manifests",
                columns: new[] { "owner_id", "folder" });

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_owner_id_folder_name",
                table: "file_manifests",
                columns: new[] { "owner_id", "folder", "name" },
                unique: true);
        }
    }
}
