using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddServerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSettings", x => x.Key);
                });

            migrationBuilder.InsertData(
                table: "ServerSettings",
                columns: new[] { "Key", "Description", "Type", "Value" },
                values: new object[,]
                {
                    { "DriverPath", "Dossier Pilotes", "STRING", "drivers" },
                    { "SmtpAuthRequired", "Auth requise ?", "BOOL", "false" },
                    { "SmtpDisplayName", "Nom Affiché", "STRING", "Autoprint Server" },
                    { "SmtpEnableSsl", "SSL/TLS", "BOOL", "false" },
                    { "SmtpFromAddress", "Email Expéditeur", "STRING", "noreply@autoprint.local" },
                    { "SmtpHost", "Adresse SMTP", "STRING", "localhost" },
                    { "SmtpIgnoreCertError", "Ignorer Certificat", "BOOL", "false" },
                    { "SmtpPass", "Password SMTP", "PASSWORD", "" },
                    { "SmtpPort", "Port SMTP", "INT", "25" },
                    { "SmtpUser", "User SMTP", "STRING", "" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerSettings");
        }
    }
}
