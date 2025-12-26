using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNewServerSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "instance_id",
                table: "server_settings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "s3_access_key_id",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "s3_bucket_name",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "s3_endpoint_url",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "s3_region",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "s3_secret_access_key_encrypted",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "smtp_password_encrypted",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "smtp_sender_email",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "smtp_server_address",
                table: "server_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "smtp_server_port",
                table: "server_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "smtp_use_ssl",
                table: "server_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "smtp_username",
                table: "server_settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "instance_id",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "s3_access_key_id",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "s3_bucket_name",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "s3_endpoint_url",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "s3_region",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "s3_secret_access_key_encrypted",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "smtp_password_encrypted",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "smtp_sender_email",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "smtp_server_address",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "smtp_server_port",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "smtp_use_ssl",
                table: "server_settings");

            migrationBuilder.DropColumn(
                name: "smtp_username",
                table: "server_settings");
        }
    }
}
