// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOidcProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "oidc_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    client_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    client_secret_encrypted = table.Column<string>(type: "text", nullable: true),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    allow_account_creation = table.Column<bool>(type: "boolean", nullable: false),
                    require_verified_email = table.Column<bool>(type: "boolean", nullable: false),
                    default_role = table.Column<int>(type: "integer", nullable: false),
                    allowed_email_domains = table.Column<string[]>(type: "text[]", nullable: false),
                    sync_profile = table.Column<bool>(type: "boolean", nullable: false),
                    sync_avatar = table.Column<bool>(type: "boolean", nullable: false),
                    integrity_mac = table.Column<byte[]>(type: "bytea", nullable: true),
                    integrity_version = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oidc_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "oidc_login_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code_verifier_encrypted = table.Column<string>(type: "text", nullable: false),
                    nonce_encrypted = table.Column<string>(type: "text", nullable: false),
                    return_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    link_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trust_device = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    integrity_mac = table.Column<byte[]>(type: "bytea", nullable: true),
                    integrity_version = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oidc_login_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_oidc_login_states_oidc_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "oidc_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_external_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    picture_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    integrity_mac = table.Column<byte[]>(type: "bytea", nullable: true),
                    integrity_version = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_external_identities", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_external_identities_oidc_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "oidc_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_external_identities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_oidc_login_states_expires_at",
                table: "oidc_login_states",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_oidc_login_states_provider_id",
                table: "oidc_login_states",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_oidc_login_states_state_hash",
                table: "oidc_login_states",
                column: "state_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_oidc_providers_slug",
                table: "oidc_providers",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_external_identities_provider_id_subject",
                table: "user_external_identities",
                columns: new[] { "provider_id", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_external_identities_user_id_provider_id",
                table: "user_external_identities",
                columns: new[] { "user_id", "provider_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "oidc_login_states");

            migrationBuilder.DropTable(
                name: "user_external_identities");

            migrationBuilder.DropTable(
                name: "oidc_providers");
        }
    }
}
