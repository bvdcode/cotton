// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFileEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_embeddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_manifest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fragment_index = table.Column<int>(type: "integer", nullable: false),
                    index_version = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<float[]>(type: "real[]", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_embeddings", x => x.id);
                    table.ForeignKey(
                        name: "FK_file_embeddings_file_manifests_file_manifest_id",
                        column: x => x.file_manifest_id,
                        principalTable: "file_manifests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_file_embeddings_file_manifest_id_index_version_fragment_ind~",
                table: "file_embeddings",
                columns: new[] { "file_manifest_id", "index_version", "fragment_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_embeddings");
        }
    }
}
