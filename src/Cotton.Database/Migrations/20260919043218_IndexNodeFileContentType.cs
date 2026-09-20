using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class IndexNodeFileContentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_node_files_content_type_file_manifest_id",
                table: "node_files",
                columns: new[] { "content_type", "file_manifest_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_node_files_content_type_file_manifest_id",
                table: "node_files");
        }
    }
}
