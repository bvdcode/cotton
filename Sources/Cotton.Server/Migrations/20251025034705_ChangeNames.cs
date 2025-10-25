using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Server.Migrations
{
    /// <inheritdoc />
    public partial class ChangeNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_layout_node_files_file_manifests_file_manifest_id",
                table: "user_layout_node_files");

            migrationBuilder.DropForeignKey(
                name: "FK_user_layout_node_files_user_layout_nodes_user_layout_node_id",
                table: "user_layout_node_files");

            migrationBuilder.DropTable(
                name: "user_layout_nodes");

            migrationBuilder.DropTable(
                name: "user_layouts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_layout_node_files",
                table: "user_layout_node_files");

            migrationBuilder.RenameTable(
                name: "user_layout_node_files",
                newName: "layout_node_files");

            migrationBuilder.RenameColumn(
                name: "user_layout_node_id",
                table: "layout_node_files",
                newName: "node_id");

            migrationBuilder.RenameIndex(
                name: "IX_user_layout_node_files_user_layout_node_id",
                table: "layout_node_files",
                newName: "IX_layout_node_files_node_id");

            migrationBuilder.RenameIndex(
                name: "IX_user_layout_node_files_file_manifest_id",
                table: "layout_node_files",
                newName: "IX_layout_node_files_file_manifest_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_layout_node_files",
                table: "layout_node_files",
                column: "id");

            migrationBuilder.CreateTable(
                name: "layouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_layouts_owner_id",
                table: "layouts",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodes_layout_id_name_parent_id_type",
                table: "nodes",
                columns: new[] { "layout_id", "name", "parent_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nodes_owner_id",
                table: "nodes",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodes_parent_id",
                table: "nodes",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "FK_layout_node_files_file_manifests_file_manifest_id",
                table: "layout_node_files",
                column: "file_manifest_id",
                principalTable: "file_manifests",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_layout_node_files_nodes_node_id",
                table: "layout_node_files",
                column: "node_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_layout_node_files_file_manifests_file_manifest_id",
                table: "layout_node_files");

            migrationBuilder.DropForeignKey(
                name: "FK_layout_node_files_nodes_node_id",
                table: "layout_node_files");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropTable(
                name: "layouts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_layout_node_files",
                table: "layout_node_files");

            migrationBuilder.RenameTable(
                name: "layout_node_files",
                newName: "user_layout_node_files");

            migrationBuilder.RenameColumn(
                name: "node_id",
                table: "user_layout_node_files",
                newName: "user_layout_node_id");

            migrationBuilder.RenameIndex(
                name: "IX_layout_node_files_node_id",
                table: "user_layout_node_files",
                newName: "IX_user_layout_node_files_user_layout_node_id");

            migrationBuilder.RenameIndex(
                name: "IX_layout_node_files_file_manifest_id",
                table: "user_layout_node_files",
                newName: "IX_user_layout_node_files_file_manifest_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_layout_node_files",
                table: "user_layout_node_files",
                column: "id");

            migrationBuilder.CreateTable(
                name: "user_layouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_layouts", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_layouts_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_layout_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_layout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_layout_nodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_layout_nodes_user_layout_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "user_layout_nodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_layout_nodes_user_layouts_user_layout_id",
                        column: x => x.user_layout_id,
                        principalTable: "user_layouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_layout_nodes_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_layout_nodes_owner_id",
                table: "user_layout_nodes",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_layout_nodes_parent_id",
                table: "user_layout_nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_layout_nodes_user_layout_id_name_parent_id_type",
                table: "user_layout_nodes",
                columns: new[] { "user_layout_id", "name", "parent_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_layouts_owner_id",
                table: "user_layouts",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_layout_node_files_file_manifests_file_manifest_id",
                table: "user_layout_node_files",
                column: "file_manifest_id",
                principalTable: "file_manifests",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_layout_node_files_user_layout_nodes_user_layout_node_id",
                table: "user_layout_node_files",
                column: "user_layout_node_id",
                principalTable: "user_layout_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
