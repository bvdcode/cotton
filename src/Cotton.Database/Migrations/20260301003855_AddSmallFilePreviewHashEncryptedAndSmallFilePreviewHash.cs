using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSmallFilePreviewHashEncryptedAndSmallFilePreviewHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "small_file_preview_hash",
                table: "file_manifests",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "small_file_preview_hash_encrypted",
                table: "file_manifests",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "small_file_preview_hash",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "small_file_preview_hash_encrypted",
                table: "file_manifests");
        }
    }
}
