using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkGCScheduledAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "gc_scheduled_after",
                table: "chunks",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gc_scheduled_after",
                table: "chunks");
        }
    }
}
