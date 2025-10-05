using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Server.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chunks",
                columns: table => new
                {
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunks", x => x.sha256);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    folder = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "chunk_ownerships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk_ownerships", x => x.id);
                    table.ForeignKey(
                        name: "FK_chunk_ownerships_users_owner_id",
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
                    chunk_order = table.Column<int>(type: "integer", nullable: false),
                    blob_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                columns: ["blob_id", "chunk_order"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_blob_chunks_chunk_sha256",
                table: "blob_chunks",
                column: "chunk_sha256");

            migrationBuilder.CreateIndex(
                name: "IX_blobs_owner_id_folder",
                table: "blobs",
                columns: ["owner_id", "folder"]);

            migrationBuilder.CreateIndex(
                name: "IX_blobs_owner_id_folder_name",
                table: "blobs",
                columns: ["owner_id", "folder", "name"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chunk_ownerships_owner_id",
                table: "chunk_ownerships",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blob_chunks");

            migrationBuilder.DropTable(
                name: "chunk_ownerships");

            migrationBuilder.DropTable(
                name: "blobs");

            migrationBuilder.DropTable(
                name: "chunks");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
