using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLayoutNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_layout_node_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_manifest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_layout_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_layout_node_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_layout_node_files_file_manifests_file_manifest_id",
                        column: x => x.file_manifest_id,
                        principalTable: "file_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_layout_node_files_user_layout_nodes_user_layout_node_id",
                        column: x => x.user_layout_node_id,
                        principalTable: "user_layout_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_layout_node_files_file_manifest_id",
                table: "user_layout_node_files",
                column: "file_manifest_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_layout_node_files_user_layout_node_id",
                table: "user_layout_node_files",
                column: "user_layout_node_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_layout_node_files");
        }
    }
}
