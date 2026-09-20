using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFileTextIndexState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "text_index_error",
                table: "file_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "text_index_version",
                table: "file_manifests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_text_index_version_created_at",
                table: "file_manifests",
                columns: new[] { "text_index_version", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_file_manifests_text_index_version_created_at",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "text_index_error",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "text_index_version",
                table: "file_manifests");
        }
    }
}
