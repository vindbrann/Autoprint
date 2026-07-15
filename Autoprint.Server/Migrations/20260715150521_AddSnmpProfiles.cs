using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSnmpProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SnmpProfileId",
                table: "Modeles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SnmpProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OidTonerBlack = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OidTonerCyan = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OidTonerMagenta = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OidTonerYellow = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OidPageCounter = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiePar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnmpProfiles", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 7, 15, 15, 5, 21, 50, DateTimeKind.Utc).AddTicks(2826));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DateModification", "SnmpProfileId" },
                values: new object[] { new DateTime(2026, 7, 15, 15, 5, 21, 50, DateTimeKind.Utc).AddTicks(4499), null });

            migrationBuilder.CreateIndex(
                name: "IX_Modeles_SnmpProfileId",
                table: "Modeles",
                column: "SnmpProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Modeles_SnmpProfiles_SnmpProfileId",
                table: "Modeles",
                column: "SnmpProfileId",
                principalTable: "SnmpProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Modeles_SnmpProfiles_SnmpProfileId",
                table: "Modeles");

            migrationBuilder.DropTable(
                name: "SnmpProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Modeles_SnmpProfileId",
                table: "Modeles");

            migrationBuilder.DropColumn(
                name: "SnmpProfileId",
                table: "Modeles");

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 7, 15, 14, 47, 19, 423, DateTimeKind.Utc).AddTicks(4678));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 7, 15, 14, 47, 19, 423, DateTimeKind.Utc).AddTicks(6245));
        }
    }
}
