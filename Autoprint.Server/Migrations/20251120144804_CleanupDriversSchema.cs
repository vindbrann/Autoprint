using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class CleanupDriversSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Checksum",
                table: "Pilotes");

            migrationBuilder.DropColumn(
                name: "CheminFichier",
                table: "Pilotes");

            migrationBuilder.DropColumn(
                name: "NomFichierInf",
                table: "Pilotes");

            migrationBuilder.RenameColumn(
                name: "EstInstalle",
                table: "Pilotes",
                newName: "EstValide");

            migrationBuilder.AddColumn<string>(
                name: "Environnement",
                table: "Pilotes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Environnement",
                table: "Pilotes");

            migrationBuilder.RenameColumn(
                name: "EstValide",
                table: "Pilotes",
                newName: "EstInstalle");

            migrationBuilder.AddColumn<string>(
                name: "Checksum",
                table: "Pilotes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CheminFichier",
                table: "Pilotes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NomFichierInf",
                table: "Pilotes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
