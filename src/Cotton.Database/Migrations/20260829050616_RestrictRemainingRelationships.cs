// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class RestrictRemainingRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_node_share_tokens_nodes_node_id",
                table: "node_share_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_oidc_login_states_oidc_providers_provider_id",
                table: "oidc_login_states");

            migrationBuilder.DropForeignKey(
                name: "FK_user_external_identities_oidc_providers_provider_id",
                table: "user_external_identities");

            migrationBuilder.DropForeignKey(
                name: "FK_user_external_identities_users_user_id",
                table: "user_external_identities");

            migrationBuilder.DropForeignKey(
                name: "FK_user_passkey_credentials_users_user_id",
                table: "user_passkey_credentials");

            migrationBuilder.AddForeignKey(
                name: "FK_node_share_tokens_nodes_node_id",
                table: "node_share_tokens",
                column: "node_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_oidc_login_states_oidc_providers_provider_id",
                table: "oidc_login_states",
                column: "provider_id",
                principalTable: "oidc_providers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_external_identities_oidc_providers_provider_id",
                table: "user_external_identities",
                column: "provider_id",
                principalTable: "oidc_providers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_external_identities_users_user_id",
                table: "user_external_identities",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_passkey_credentials_users_user_id",
                table: "user_passkey_credentials",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_node_share_tokens_nodes_node_id",
                table: "node_share_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_oidc_login_states_oidc_providers_provider_id",
                table: "oidc_login_states");

            migrationBuilder.DropForeignKey(
                name: "FK_user_external_identities_oidc_providers_provider_id",
                table: "user_external_identities");

            migrationBuilder.DropForeignKey(
                name: "FK_user_external_identities_users_user_id",
                table: "user_external_identities");

            migrationBuilder.DropForeignKey(
                name: "FK_user_passkey_credentials_users_user_id",
                table: "user_passkey_credentials");

            migrationBuilder.AddForeignKey(
                name: "FK_node_share_tokens_nodes_node_id",
                table: "node_share_tokens",
                column: "node_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_oidc_login_states_oidc_providers_provider_id",
                table: "oidc_login_states",
                column: "provider_id",
                principalTable: "oidc_providers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_external_identities_oidc_providers_provider_id",
                table: "user_external_identities",
                column: "provider_id",
                principalTable: "oidc_providers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_external_identities_users_user_id",
                table: "user_external_identities",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_passkey_credentials_users_user_id",
                table: "user_passkey_credentials",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
