using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Sync.ClientState.Migrations
{
    /// <inheritdoc />
    public partial class InitialClientState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_state",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_state", x => x.key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_state");
        }
    }
}
