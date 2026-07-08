// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFileManifestMetadataExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "metadata",
                table: "file_manifests",
                type: "hstore",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "metadata_extraction_error",
                table: "file_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "metadata_extractor_version",
                table: "file_manifests",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "metadata",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "metadata_extraction_error",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "metadata_extractor_version",
                table: "file_manifests");
        }
    }
}
