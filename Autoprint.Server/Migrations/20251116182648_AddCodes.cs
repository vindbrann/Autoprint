using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Emplacements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "Emplacements");
        }
    }
}
