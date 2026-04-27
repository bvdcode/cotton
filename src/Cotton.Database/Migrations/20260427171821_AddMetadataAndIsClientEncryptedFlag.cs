using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataAndIsClientEncryptedFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_client_encrypted",
                table: "node_files",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "metadata",
                table: "node_files",
                type: "hstore",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_client_encrypted",
                table: "node_files");

            migrationBuilder.DropColumn(
                name: "metadata",
                table: "node_files");
        }
    }
}
