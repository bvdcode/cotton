// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFirebaseCloudMessagingAndServerSettingsIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "push_device_tokens");

            migrationBuilder.DropColumn(
                name: "fcm_project_id",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "fcm_service_account_json_encrypted",
                table: "server_settings");

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
            migrationBuilder.AddColumn<string>(
                name: "fcm_project_id",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fcm_service_account_json_encrypted",
                table: "server_settings",
                type: "text",
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

            migrationBuilder.CreateTable(
                name: "push_device_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    device_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    integrity_mac = table.Column<byte[]>(type: "bytea", nullable: true),
                    integrity_version = table.Column<int>(type: "integer", nullable: true),
                    last_registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    platform = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<int>(type: "integer", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_device_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_push_device_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_push_device_tokens_provider_token_hash",
                table: "push_device_tokens",
                columns: new[] { "provider", "token_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_push_device_tokens_user_id_session_id_provider_platform",
                table: "push_device_tokens",
                columns: new[] { "user_id", "session_id", "provider", "platform" });
        }
    }
}
