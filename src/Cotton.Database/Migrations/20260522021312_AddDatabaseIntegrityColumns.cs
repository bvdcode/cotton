// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseIntegrityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "users",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "user_passkey_credentials",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "user_passkey_credentials",
                type: "integer",
                nullable: true);

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

            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "refresh_tokens",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "refresh_tokens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "node_share_tokens",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "node_share_tokens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "download_tokens",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "download_tokens",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "users");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "users");

            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "user_passkey_credentials");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "user_passkey_credentials");

            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "node_share_tokens");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "node_share_tokens");

            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "download_tokens");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "download_tokens");
        }
    }
}
