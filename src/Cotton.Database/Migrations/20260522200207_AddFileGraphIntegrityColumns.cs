using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFileGraphIntegrityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "nodes",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "nodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "node_files",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "node_files",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "file_manifests",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "file_manifests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "file_manifest_chunks",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "file_manifest_chunks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "integrity_mac",
                table: "chunks",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "integrity_version",
                table: "chunks",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "nodes");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "nodes");

            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "node_files");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "node_files");

            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "file_manifests");

            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "file_manifest_chunks");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "file_manifest_chunks");

            migrationBuilder.DropColumn(
                name: "integrity_mac",
                table: "chunks");

            migrationBuilder.DropColumn(
                name: "integrity_version",
                table: "chunks");
        }
    }
}
