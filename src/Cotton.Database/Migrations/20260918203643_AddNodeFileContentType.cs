using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNodeFileContentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_type",
                table: "node_files",
                type: "citext",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_type",
                table: "node_files");
        }
    }
}
