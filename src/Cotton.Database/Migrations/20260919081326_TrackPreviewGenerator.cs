using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class TrackPreviewGenerator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preview_generator_id",
                table: "file_manifests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_preview_generator_id_preview_generator_versi~",
                table: "file_manifests",
                columns: new[] { "preview_generator_id", "preview_generator_version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_file_manifests_preview_generator_id_preview_generator_versi~",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "preview_generator_id",
                table: "file_manifests");
        }
    }
}
