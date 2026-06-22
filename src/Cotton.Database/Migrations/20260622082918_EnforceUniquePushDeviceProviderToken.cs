using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniquePushDeviceProviderToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_push_device_tokens_user_id_provider_token_hash",
                table: "push_device_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_push_device_tokens_provider_token_hash",
                table: "push_device_tokens",
                columns: new[] { "provider", "token_hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_push_device_tokens_provider_token_hash",
                table: "push_device_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_push_device_tokens_user_id_provider_token_hash",
                table: "push_device_tokens",
                columns: new[] { "user_id", "provider", "token_hash" },
                unique: true);
        }
    }
}
