using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDefaultFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstParDefaut",
                table: "Imprimantes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstParDefaut",
                table: "Imprimantes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
