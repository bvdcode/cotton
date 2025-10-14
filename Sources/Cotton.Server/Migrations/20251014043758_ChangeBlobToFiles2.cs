using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Server.Migrations
{
    /// <inheritdoc />
    public partial class ChangeBlobToFiles2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blob_chunks");

            migrationBuilder.DropTable(
                name: "blobs");

            migrationBuilder.CreateTable(
                name: "file_manifests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    folder = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    FileManifestId = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_manifests", x => x.id);
                    table.ForeignKey(
                        name: "FK_file_manifests_file_manifests_FileManifestId",
                        column: x => x.FileManifestId,
                        principalTable: "file_manifests",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_file_manifests_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "file_manifest_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_order = table.Column<int>(type: "integer", nullable: false),
                    blob_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_manifest_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_file_manifest_chunks_chunks_chunk_sha256",
                        column: x => x.chunk_sha256,
                        principalTable: "chunks",
                        principalColumn: "sha256",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_file_manifest_chunks_file_manifests_blob_id",
                        column: x => x.blob_id,
                        principalTable: "file_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_file_manifest_chunks_blob_id_chunk_order",
                table: "file_manifest_chunks",
                columns: new[] { "blob_id", "chunk_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_manifest_chunks_chunk_sha256",
                table: "file_manifest_chunks",
                column: "chunk_sha256");

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_FileManifestId",
                table: "file_manifests",
                column: "FileManifestId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_manifest_chunks");

            migrationBuilder.DropTable(
                name: "file_manifests");

            migrationBuilder.CreateTable(
                name: "blobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    folder = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_blobs_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "blob_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blob_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    chunk_order = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blob_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_blob_chunks_blobs_blob_id",
                        column: x => x.blob_id,
                        principalTable: "blobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_blob_chunks_chunks_chunk_sha256",
                        column: x => x.chunk_sha256,
                        principalTable: "chunks",
                        principalColumn: "sha256",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blob_chunks_blob_id_chunk_order",
                table: "blob_chunks",
                columns: new[] { "blob_id", "chunk_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_blob_chunks_chunk_sha256",
                table: "blob_chunks",
                column: "chunk_sha256");

            migrationBuilder.CreateIndex(
                name: "IX_blobs_owner_id_folder",
                table: "blobs",
                columns: new[] { "owner_id", "folder" });

            migrationBuilder.CreateIndex(
                name: "IX_blobs_owner_id_folder_name",
                table: "blobs",
                columns: new[] { "owner_id", "folder", "name" },
                unique: true);
        }
    }
}
