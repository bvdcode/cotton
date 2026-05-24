// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFileManifestDurationAndCodecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "audio_codec",
                table: "file_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "duration_seconds",
                table: "file_manifests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "video_codec",
                table: "file_manifests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audio_codec",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "duration_seconds",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "video_codec",
                table: "file_manifests");
        }
    }
}
