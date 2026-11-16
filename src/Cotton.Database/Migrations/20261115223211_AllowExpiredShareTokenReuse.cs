using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AllowExpiredShareTokenReuse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_node_share_tokens_token",
                table: "node_share_tokens");

            migrationBuilder.DropIndex(
                name: "IX_download_tokens_token",
                table: "download_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_node_share_tokens_token",
                table: "node_share_tokens",
                column: "token");

            migrationBuilder.CreateIndex(
                name: "IX_download_tokens_token",
                table: "download_tokens",
                column: "token");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_node_share_tokens_token",
                table: "node_share_tokens");

            migrationBuilder.DropIndex(
                name: "IX_download_tokens_token",
                table: "download_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_node_share_tokens_token",
                table: "node_share_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_download_tokens_token",
                table: "download_tokens",
                column: "token",
                unique: true);
        }
    }
}
