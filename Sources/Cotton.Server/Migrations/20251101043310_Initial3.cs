using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Server.Migrations
{
    /// <inheritdoc />
    public partial class Initial3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_manifest_chunks_file_manifests_file_manifest_id",
                table: "file_manifest_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_node_files_file_manifests_file_manifest_id",
                table: "node_files");

            migrationBuilder.DropPrimaryKey(
                name: "PK_file_manifests",
                table: "file_manifests");

            migrationBuilder.DropIndex(
                name: "IX_file_manifests_sha256",
                table: "file_manifests");

            migrationBuilder.DropIndex(
                name: "IX_file_manifest_chunks_file_manifest_id_chunk_order",
                table: "file_manifest_chunks");

            migrationBuilder.DropColumn(
                name: "id",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "file_manifest_id",
                table: "file_manifest_chunks");

            migrationBuilder.AddColumn<byte[]>(
                name: "FileManifestSha256",
                table: "node_files",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "file_manifest_sha256",
                table: "file_manifest_chunks",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddPrimaryKey(
                name: "PK_file_manifests",
                table: "file_manifests",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "IX_node_files_FileManifestSha256",
                table: "node_files",
                column: "FileManifestSha256");

            migrationBuilder.CreateIndex(
                name: "IX_file_manifest_chunks_file_manifest_sha256_chunk_order",
                table: "file_manifest_chunks",
                columns: new[] { "file_manifest_sha256", "chunk_order" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_file_manifest_chunks_file_manifests_file_manifest_sha256",
                table: "file_manifest_chunks",
                column: "file_manifest_sha256",
                principalTable: "file_manifests",
                principalColumn: "sha256",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_node_files_file_manifests_FileManifestSha256",
                table: "node_files",
                column: "FileManifestSha256",
                principalTable: "file_manifests",
                principalColumn: "sha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_manifest_chunks_file_manifests_file_manifest_sha256",
                table: "file_manifest_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_node_files_file_manifests_FileManifestSha256",
                table: "node_files");

            migrationBuilder.DropIndex(
                name: "IX_node_files_FileManifestSha256",
                table: "node_files");

            migrationBuilder.DropPrimaryKey(
                name: "PK_file_manifests",
                table: "file_manifests");

            migrationBuilder.DropIndex(
                name: "IX_file_manifest_chunks_file_manifest_sha256_chunk_order",
                table: "file_manifest_chunks");

            migrationBuilder.DropColumn(
                name: "FileManifestSha256",
                table: "node_files");

            migrationBuilder.DropColumn(
                name: "file_manifest_sha256",
                table: "file_manifest_chunks");

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "file_manifests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "file_manifests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "file_manifests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "file_manifest_id",
                table: "file_manifest_chunks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_file_manifests",
                table: "file_manifests",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_sha256",
                table: "file_manifests",
                column: "sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_manifest_chunks_file_manifest_id_chunk_order",
                table: "file_manifest_chunks",
                columns: new[] { "file_manifest_id", "chunk_order" },
                unique: true);

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
        }
    }
}
