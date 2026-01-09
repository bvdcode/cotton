using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPreviewOwnershipValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preview_image_hash",
                table: "file_manifests");

            migrationBuilder.AddColumn<Guid>(
                name: "file_preview_id",
                table: "file_manifests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "file_previews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_previews", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_file_preview_id",
                table: "file_manifests",
                column: "file_preview_id");

            migrationBuilder.CreateIndex(
                name: "IX_file_previews_hash",
                table: "file_previews",
                column: "hash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_file_manifests_file_previews_file_preview_id",
                table: "file_manifests",
                column: "file_preview_id",
                principalTable: "file_previews",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_manifests_file_previews_file_preview_id",
                table: "file_manifests");

            migrationBuilder.DropTable(
                name: "file_previews");

            migrationBuilder.DropIndex(
                name: "IX_file_manifests_file_preview_id",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "file_preview_id",
                table: "file_manifests");

            migrationBuilder.AddColumn<byte[]>(
                name: "preview_image_hash",
                table: "file_manifests",
                type: "bytea",
                nullable: true);
        }
    }
}
