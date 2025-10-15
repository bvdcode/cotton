using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "user_layout_nodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_user_layout_nodes_owner_id",
                table: "user_layout_nodes",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_layout_nodes_users_owner_id",
                table: "user_layout_nodes",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_layout_nodes_users_owner_id",
                table: "user_layout_nodes");

            migrationBuilder.DropIndex(
                name: "IX_user_layout_nodes_owner_id",
                table: "user_layout_nodes");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "user_layout_nodes");
        }
    }
}
