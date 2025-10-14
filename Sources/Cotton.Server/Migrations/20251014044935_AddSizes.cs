using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "size_bytes",
                table: "chunks",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "size_bytes",
                table: "chunks");
        }
    }
}
