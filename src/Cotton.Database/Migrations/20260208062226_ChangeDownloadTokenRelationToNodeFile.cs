using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDownloadTokenRelationToNodeFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_download_tokens_file_manifests_file_manifest_id",
                table: "download_tokens");

            migrationBuilder.RenameColumn(
                name: "file_manifest_id",
                table: "download_tokens",
                newName: "node_file_id");

            migrationBuilder.RenameIndex(
                name: "IX_download_tokens_file_manifest_id",
                table: "download_tokens",
                newName: "IX_download_tokens_node_file_id");

            migrationBuilder.AddForeignKey(
                name: "FK_download_tokens_node_files_node_file_id",
                table: "download_tokens",
                column: "node_file_id",
                principalTable: "node_files",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_download_tokens_node_files_node_file_id",
                table: "download_tokens");

            migrationBuilder.RenameColumn(
                name: "node_file_id",
                table: "download_tokens",
                newName: "file_manifest_id");

            migrationBuilder.RenameIndex(
                name: "IX_download_tokens_node_file_id",
                table: "download_tokens",
                newName: "IX_download_tokens_file_manifest_id");

            migrationBuilder.AddForeignKey(
                name: "FK_download_tokens_file_manifests_file_manifest_id",
                table: "download_tokens",
                column: "file_manifest_id",
                principalTable: "file_manifests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
