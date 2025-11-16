using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCodesAndNamingOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Modeles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Imprimantes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.InsertData(
                table: "ServerSettings",
                columns: new[] { "Key", "Description", "Type", "Value" },
                values: new object[,]
                {
                    { "NamingEnabled", "Activer le nommage automatique", "BOOL", "false" },
                    { "NamingSameShare", "Forcer le nom de partage identique au nom d'imprimante", "BOOL", "false" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ServerSettings",
                keyColumn: "Key",
                keyValue: "NamingEnabled");

            migrationBuilder.DeleteData(
                table: "ServerSettings",
                keyColumn: "Key",
                keyValue: "NamingSameShare");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Modeles");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Imprimantes");
        }
    }
}
