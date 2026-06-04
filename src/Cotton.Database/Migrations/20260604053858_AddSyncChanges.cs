using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chunk_ownerships_users_owner_id",
                table: "chunk_ownerships");

            migrationBuilder.DropForeignKey(
                name: "FK_layouts_users_owner_id",
                table: "layouts");

            migrationBuilder.DropForeignKey(
                name: "FK_node_files_users_owner_id",
                table: "node_files");

            migrationBuilder.DropForeignKey(
                name: "FK_nodes_users_owner_id",
                table: "nodes");

            migrationBuilder.CreateTable(
                name: "sync_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    layout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_parent_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_manifest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
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
                name: "IX_sync_changes_owner_id_revision",
                table: "sync_changes",
                columns: new[] { "owner_id", "revision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_chunk_ownerships_users_owner_id",
                table: "chunk_ownerships",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_layouts_users_owner_id",
                table: "layouts",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_node_files_users_owner_id",
                table: "node_files",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_nodes_users_owner_id",
                table: "nodes",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chunk_ownerships_users_owner_id",
                table: "chunk_ownerships");

            migrationBuilder.DropForeignKey(
                name: "FK_layouts_users_owner_id",
                table: "layouts");

            migrationBuilder.DropForeignKey(
                name: "FK_node_files_users_owner_id",
                table: "node_files");

            migrationBuilder.DropForeignKey(
                name: "FK_nodes_users_owner_id",
                table: "nodes");

            migrationBuilder.DropTable(
                name: "sync_changes");

            migrationBuilder.AddForeignKey(
                name: "FK_chunk_ownerships_users_owner_id",
                table: "chunk_ownerships",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_layouts_users_owner_id",
                table: "layouts",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_node_files_users_owner_id",
                table: "node_files",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_nodes_users_owner_id",
                table: "nodes",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
