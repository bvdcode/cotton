using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFileManifestDurationAndCodecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
    }
}
