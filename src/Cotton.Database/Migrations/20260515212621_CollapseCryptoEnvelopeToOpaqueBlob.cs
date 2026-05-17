using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cotton.Database.Migrations
{
    /// <inheritdoc />
    public partial class CollapseCryptoEnvelopeToOpaqueBlob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "envelope_version",
                table: "user_crypto_envelopes");

            migrationBuilder.DropColumn(
                name: "kdf_alg",
                table: "user_crypto_envelopes");

            migrationBuilder.DropColumn(
                name: "kdf_params",
                table: "user_crypto_envelopes");

            migrationBuilder.DropColumn(
                name: "kdf_salt",
                table: "user_crypto_envelopes");

            migrationBuilder.DropColumn(
                name: "recovery_kdf_params",
                table: "user_crypto_envelopes");

            migrationBuilder.DropColumn(
                name: "recovery_kdf_salt",
                table: "user_crypto_envelopes");

            migrationBuilder.DropColumn(
                name: "wrapped_master_key",
                table: "user_crypto_envelopes");

            migrationBuilder.RenameColumn(
                name: "wrapped_master_key_recovery",
                table: "user_crypto_envelopes",
                newName: "envelope");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "envelope",
                table: "user_crypto_envelopes",
                newName: "wrapped_master_key_recovery");

            migrationBuilder.AddColumn<int>(
                name: "envelope_version",
                table: "user_crypto_envelopes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "kdf_alg",
                table: "user_crypto_envelopes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "kdf_params",
                table: "user_crypto_envelopes",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "kdf_salt",
                table: "user_crypto_envelopes",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "recovery_kdf_params",
                table: "user_crypto_envelopes",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "recovery_kdf_salt",
                table: "user_crypto_envelopes",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "wrapped_master_key",
                table: "user_crypto_envelopes",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
