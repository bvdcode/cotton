using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_manifests_users_owner_id",
                table: "file_manifests");

            migrationBuilder.DropIndex(
                name: "IX_chunk_ownerships_owner_id",
                table: "chunk_ownerships");

            migrationBuilder.AddColumn<string>(
                name: "password_phc",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "owner_id",
                table: "file_manifests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "version_stable_id",
                table: "file_manifests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
                    user_layout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chunk_ownerships_owner_id_chunk_sha256",
                table: "chunk_ownerships",
                columns: new[] { "owner_id", "chunk_sha256" },
                unique: true);

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
                name: "FK_file_manifests_users_owner_id",
                table: "file_manifests",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_manifests_users_owner_id",
                table: "file_manifests");

            migrationBuilder.DropTable(
                name: "user_layout_nodes");

            migrationBuilder.DropTable(
                name: "user_layouts");

            migrationBuilder.DropIndex(
                name: "IX_users_username",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_chunk_ownerships_owner_id_chunk_sha256",
                table: "chunk_ownerships");

            migrationBuilder.DropColumn(
                name: "password_phc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "username",
                table: "users");

            migrationBuilder.DropColumn(
                name: "version_stable_id",
                table: "file_manifests");

            migrationBuilder.AlterColumn<Guid>(
                name: "owner_id",
                table: "file_manifests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_chunk_ownerships_owner_id",
                table: "chunk_ownerships",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_file_manifests_users_owner_id",
                table: "file_manifests",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id");
        }
    }
}
