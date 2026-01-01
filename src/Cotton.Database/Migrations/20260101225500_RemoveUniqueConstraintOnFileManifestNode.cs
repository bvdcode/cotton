using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueConstraintOnFileManifestNode : Migration
    {
        private static readonly string[] columns = ["file_manifest_hash", "node_id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_node_files_file_manifest_hash_node_id",
                table: "node_files");

            migrationBuilder.CreateIndex(
                name: "IX_node_files_file_manifest_hash_node_id",
                table: "node_files",
                columns: columns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_node_files_file_manifest_hash_node_id",
                table: "node_files");

            migrationBuilder.CreateIndex(
                name: "IX_node_files_file_manifest_hash_node_id",
                table: "node_files",
                columns: ["file_manifest_hash", "node_id"],
                unique: true);
        }
    }
}
