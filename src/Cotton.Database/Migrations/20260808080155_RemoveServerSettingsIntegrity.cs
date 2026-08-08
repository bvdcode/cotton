// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveServerSettingsIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "server_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "server_settings",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "server_settings",
                type: "integer",
                nullable: true);
        }
    }
}
