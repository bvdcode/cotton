// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class DropNodeFilesNameKeyUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_node_files_node_id_name_key",
                table: "node_files");

            migrationBuilder.CreateIndex(
                name: "IX_node_files_node_id_name_key",
                table: "node_files",
                columns: new[] { "node_id", "name_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_node_files_node_id_name_key",
                table: "node_files");

            migrationBuilder.CreateIndex(
                name: "IX_node_files_node_id_name_key",
                table: "node_files",
                columns: new[] { "node_id", "name_key" },
                unique: true);
        }
    }
}
