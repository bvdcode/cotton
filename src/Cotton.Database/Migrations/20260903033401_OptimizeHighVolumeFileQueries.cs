using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeHighVolumeFileQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_node_files_node_id_name_key",
                table: "node_files");

            migrationBuilder.DropIndex(
                name: "IX_node_files_owner_id",
                table: "node_files");

            migrationBuilder.CreateIndex(
                name: "IX_node_files_node_id_name_key_owner_id_id",
                table: "node_files",
                columns: new[] { "node_id", "name_key", "owner_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_node_files_owner_id_created_at",
                table: "node_files",
                columns: new[] { "owner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_content_type_preview_generator_version",
                table: "file_manifests",
                columns: new[] { "content_type", "preview_generator_version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_node_files_node_id_name_key_owner_id_id",
                table: "node_files");

            migrationBuilder.DropIndex(
                name: "IX_node_files_owner_id_created_at",
                table: "node_files");

            migrationBuilder.DropIndex(
                name: "IX_file_manifests_content_type_preview_generator_version",
                table: "file_manifests");

            migrationBuilder.CreateIndex(
                name: "IX_node_files_node_id_name_key",
                table: "node_files",
                columns: new[] { "node_id", "name_key" });

            migrationBuilder.CreateIndex(
                name: "IX_node_files_owner_id",
                table: "node_files",
                column: "owner_id");
        }
    }
}
