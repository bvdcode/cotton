using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_large_file_preview_hash",
                table: "file_manifests",
                column: "large_file_preview_hash");

            migrationBuilder.CreateIndex(
                name: "IX_file_manifests_small_file_preview_hash",
                table: "file_manifests",
                column: "small_file_preview_hash");

            migrationBuilder.CreateIndex(
                name: "IX_chunks_gc_scheduled_after",
                table: "chunks",
                column: "gc_scheduled_after");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_file_manifests_large_file_preview_hash",
                table: "file_manifests");

            migrationBuilder.DropIndex(
                name: "IX_file_manifests_small_file_preview_hash",
                table: "file_manifests");

            migrationBuilder.DropIndex(
                name: "IX_chunks_gc_scheduled_after",
                table: "chunks");
        }
    }
}
