using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    layout_id = table.Column<Guid>(type: "uuid", nullable: true),
                    node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    node_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_parent_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_manifest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_node_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    content_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    e_tag = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_sync_changes_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_changes_owner_id_layout_id_revision",
                table: "sync_changes",
                columns: new[] { "owner_id", "layout_id", "revision" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_changes_owner_id_node_file_id",
                table: "sync_changes",
                columns: new[] { "owner_id", "node_file_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_changes_owner_id_node_id",
                table: "sync_changes",
                columns: new[] { "owner_id", "node_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_changes_owner_id_revision",
                table: "sync_changes",
                columns: new[] { "owner_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_changes");
        }
    }
}
