using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    public partial class AddChunkStoredSizeBytes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "size_bytes",
                table: "chunks",
                newName: "plain_size_bytes");

            migrationBuilder.AddColumn<long>(
                name: "stored_size_bytes",
                table: "chunks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "stored_size_bytes",
                table: "chunks");

            migrationBuilder.RenameColumn(
                name: "plain_size_bytes",
                table: "chunks",
                newName: "size_bytes");
        }
    }
}
