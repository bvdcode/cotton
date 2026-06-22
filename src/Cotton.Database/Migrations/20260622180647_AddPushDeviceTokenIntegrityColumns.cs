using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPushDeviceTokenIntegrityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "push_device_tokens",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "push_device_tokens",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "push_device_tokens");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "push_device_tokens");
        }
    }
}
