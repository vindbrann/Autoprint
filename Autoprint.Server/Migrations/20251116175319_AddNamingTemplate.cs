using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddNamingTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ServerSettings",
                columns: new[] { "Key", "Description", "Type", "Value" },
                values: new object[] { "NamingTemplate", "Gabarit de nommage (Tokens: {LIEU}, {MODELE}, {MARQUE}, {IP}, {IP_LAST})", "STRING", "IMP_{LIEU}_{MODELE}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ServerSettings",
                keyColumn: "Key",
                keyValue: "NamingTemplate");
        }
    }
}
