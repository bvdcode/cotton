using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChangeRefreshTokenToExtendedRefreshToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "auth_type",
                table: "refresh_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "refresh_tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "refresh_tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "device",
                table: "refresh_tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<IPAddress>(
                name: "ip_address",
                table: "refresh_tokens",
                type: "inet",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "region",
                table: "refresh_tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_agent",
                table: "refresh_tokens",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "auth_type",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "city",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "country",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "device",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "region",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "user_agent",
                table: "refresh_tokens");
        }
    }
}
