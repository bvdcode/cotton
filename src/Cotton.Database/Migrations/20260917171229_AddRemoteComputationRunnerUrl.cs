// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteComputationRunnerUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "remote_computation_runner_url",
                table: "server_settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "remote_computation_runner_url",
                table: "server_settings");
        }
    }
}
