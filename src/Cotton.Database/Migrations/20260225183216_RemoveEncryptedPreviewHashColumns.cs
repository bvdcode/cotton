using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEncryptedPreviewHashColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "encrypted_file_large_preview_hash",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "encrypted_file_preview_hash",
                table: "file_manifests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "encrypted_file_large_preview_hash",
                table: "file_manifests",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "encrypted_file_preview_hash",
                table: "file_manifests",
                type: "bytea",
                nullable: true);
        }
    }
}
