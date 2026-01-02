using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "benchmarks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false),
                    units = table.Column<string>(type: "text", nullable: false),
                    elapsed = table.Column<TimeSpan>(type: "interval", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benchmarks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chunks",
                columns: table => new
                {
                    hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    compression_algorithm = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunks", x => x.hash);
                });

            migrationBuilder.CreateTable(
                name: "file_manifests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    computed_content_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    proposed_content_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_manifests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "server_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encryption_threads = table.Column<int>(type: "integer", nullable: false),
                    cipher_chunk_size_bytes = table.Column<int>(type: "integer", nullable: false),
                    max_chunk_size_bytes = table.Column<int>(type: "integer", nullable: false),
                    session_timeout_hours = table.Column<int>(type: "integer", nullable: false),
                    allow_cross_user_deduplication = table.Column<bool>(type: "boolean", nullable: false),
                    allow_global_indexing = table.Column<bool>(type: "boolean", nullable: false),
                    telemetry_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    smtp_server_address = table.Column<string>(type: "text", nullable: true),
                    smtp_server_port = table.Column<int>(type: "integer", nullable: true),
                    smtp_username = table.Column<string>(type: "text", nullable: true),
                    smtp_password_encrypted = table.Column<string>(type: "text", nullable: true),
                    smtp_sender_email = table.Column<string>(type: "text", nullable: true),
                    smtp_use_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    s3_access_key_id = table.Column<string>(type: "text", nullable: true),
                    s3_secret_access_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    s3_bucket_name = table.Column<string>(type: "text", nullable: true),
                    s3_region = table.Column<string>(type: "text", nullable: true),
                    s3_endpoint_url = table.Column<string>(type: "text", nullable: true),
                    email_mode = table.Column<int>(type: "integer", nullable: false),
                    compution_mode = table.Column<int>(type: "integer", nullable: false),
                    storage_type = table.Column<int>(type: "integer", nullable: false),
                    import_source = table.Column<int>(type: "integer", nullable: false),
                    server_usage = table.Column<int[]>(type: "integer[]", nullable: false),
                    storage_space_mode = table.Column<int>(type: "integer", nullable: false),
                    webdav_password_encrypted = table.Column<string>(type: "text", nullable: true),
                    webdav_username = table.Column<string>(type: "text", nullable: true),
                    webdav_host = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "citext", nullable: false),
                    password_phc = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "file_manifest_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_manifest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_order = table.Column<int>(type: "integer", nullable: false),
                    chunk_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_manifest_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_file_manifest_chunks_chunks_chunk_hash",
                        column: x => x.chunk_hash,
                        principalTable: "chunks",
                        principalColumn: "hash",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_file_manifest_chunks_file_manifests_file_manifest_id",
                        column: x => x.file_manifest_id,
                        principalTable: "file_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chunk_ownerships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk_ownerships", x => x.id);
                    table.ForeignKey(
                        name: "FK_chunk_ownerships_chunks_chunk_hash",
                        column: x => x.chunk_hash,
                        principalTable: "chunks",
                        principalColumn: "hash",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chunk_ownerships_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "layouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_layouts", x => x.id);
                    table.ForeignKey(
                        name: "FK_layouts_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    layout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_nodes_layouts_layout_id",
                        column: x => x.layout_id,
                        principalTable: "layouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nodes_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "nodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_nodes_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "node_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_manifest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_node_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_node_files_file_manifests_file_manifest_id",
                        column: x => x.file_manifest_id,
                        principalTable: "file_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_node_files_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_node_files_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chunk_ownerships_chunk_hash",
                table: "chunk_ownerships",
                column: "chunk_hash");

            migrationBuilder.CreateIndex(
                name: "IX_chunk_ownerships_owner_id_chunk_hash",
                table: "chunk_ownerships",
                columns: ["owner_id", "chunk_hash"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_manifest_chunks_chunk_hash",
                table: "file_manifest_chunks",
                column: "chunk_hash");

            migrationBuilder.CreateIndex(
                name: "IX_file_manifest_chunks_file_manifest_id_chunk_order",
                table: "file_manifest_chunks",
                columns: ["file_manifest_id", "chunk_order"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_computed_content_hash",
                table: "file_manifests",
                column: "computed_content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_proposed_content_hash",
                table: "file_manifests",
                column: "proposed_content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_layouts_owner_id",
                table: "layouts",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_node_files_file_manifest_id_node_id",
                table: "node_files",
                columns: ["file_manifest_id", "node_id"]);

            migrationBuilder.CreateIndex(
                name: "IX_node_files_node_id_name_key",
                table: "node_files",
                columns: ["node_id", "name_key"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_node_files_owner_id",
                table: "node_files",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodes_layout_id_parent_id_type_name_key",
                table: "nodes",
                columns: ["layout_id", "parent_id", "type", "name_key"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nodes_owner_id",
                table: "nodes",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodes_parent_id",
                table: "nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "benchmarks");

            migrationBuilder.DropTable(
                name: "chunk_ownerships");

            migrationBuilder.DropTable(
                name: "file_manifest_chunks");

            migrationBuilder.DropTable(
                name: "node_files");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "server_settings");

            migrationBuilder.DropTable(
                name: "chunks");

            migrationBuilder.DropTable(
                name: "file_manifests");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropTable(
                name: "layouts");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
