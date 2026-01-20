using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCascadeRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chunk_ownerships_chunks_chunk_hash",
                table: "chunk_ownerships");

            migrationBuilder.DropForeignKey(
                name: "FK_download_tokens_file_manifests_file_manifest_id",
                table: "download_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_download_tokens_users_created_by_user_id",
                table: "download_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_file_manifest_chunks_chunks_chunk_hash",
                table: "file_manifest_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_file_manifest_chunks_file_manifests_file_manifest_id",
                table: "file_manifest_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_node_files_file_manifests_file_manifest_id",
                table: "node_files");

            migrationBuilder.DropForeignKey(
                name: "FK_node_files_nodes_node_id",
                table: "node_files");

            migrationBuilder.DropForeignKey(
                name: "FK_nodes_layouts_layout_id",
                table: "nodes");

            migrationBuilder.DropForeignKey(
                name: "FK_nodes_nodes_parent_id",
                table: "nodes");

            migrationBuilder.AddForeignKey(
                name: "FK_chunk_ownerships_chunks_chunk_hash",
                table: "chunk_ownerships",
                column: "chunk_hash",
                principalTable: "chunks",
                principalColumn: "hash",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_download_tokens_file_manifests_file_manifest_id",
                table: "download_tokens",
                column: "file_manifest_id",
                principalTable: "file_manifests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_download_tokens_users_created_by_user_id",
                table: "download_tokens",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_file_manifest_chunks_chunks_chunk_hash",
                table: "file_manifest_chunks",
                column: "chunk_hash",
                principalTable: "chunks",
                principalColumn: "hash",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_file_manifest_chunks_file_manifests_file_manifest_id",
                table: "file_manifest_chunks",
                column: "file_manifest_id",
                principalTable: "file_manifests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_node_files_file_manifests_file_manifest_id",
                table: "node_files",
                column: "file_manifest_id",
                principalTable: "file_manifests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_node_files_nodes_node_id",
                table: "node_files",
                column: "node_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_nodes_layouts_layout_id",
                table: "nodes",
                column: "layout_id",
                principalTable: "layouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_nodes_nodes_parent_id",
                table: "nodes",
                column: "parent_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chunk_ownerships_chunks_chunk_hash",
                table: "chunk_ownerships");

            migrationBuilder.DropForeignKey(
                name: "FK_download_tokens_file_manifests_file_manifest_id",
                table: "download_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_download_tokens_users_created_by_user_id",
                table: "download_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_file_manifest_chunks_chunks_chunk_hash",
                table: "file_manifest_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_file_manifest_chunks_file_manifests_file_manifest_id",
                table: "file_manifest_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_node_files_file_manifests_file_manifest_id",
                table: "node_files");

            migrationBuilder.DropForeignKey(
                name: "FK_node_files_nodes_node_id",
                table: "node_files");

            migrationBuilder.DropForeignKey(
                name: "FK_nodes_layouts_layout_id",
                table: "nodes");

            migrationBuilder.DropForeignKey(
                name: "FK_nodes_nodes_parent_id",
                table: "nodes");

            migrationBuilder.AddForeignKey(
                name: "FK_chunk_ownerships_chunks_chunk_hash",
                table: "chunk_ownerships",
                column: "chunk_hash",
                principalTable: "chunks",
                principalColumn: "hash",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_download_tokens_file_manifests_file_manifest_id",
                table: "download_tokens",
                column: "file_manifest_id",
                principalTable: "file_manifests",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_download_tokens_users_created_by_user_id",
                table: "download_tokens",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_file_manifest_chunks_chunks_chunk_hash",
                table: "file_manifest_chunks",
                column: "chunk_hash",
                principalTable: "chunks",
                principalColumn: "hash",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_file_manifest_chunks_file_manifests_file_manifest_id",
                table: "file_manifest_chunks",
                column: "file_manifest_id",
                principalTable: "file_manifests",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_node_files_file_manifests_file_manifest_id",
                table: "node_files",
                column: "file_manifest_id",
                principalTable: "file_manifests",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_node_files_nodes_node_id",
                table: "node_files",
                column: "node_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_nodes_layouts_layout_id",
                table: "nodes",
                column: "layout_id",
                principalTable: "layouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_nodes_nodes_parent_id",
                table: "nodes",
                column: "parent_id",
                principalTable: "nodes",
                principalColumn: "id");
        }
    }
}
