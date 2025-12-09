using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class RenameBranchToDirect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsBranchOfficeEnabled",
                table: "Imprimantes",
                newName: "IsDirectPrintingEnabled");

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 8, 11, 25, 9, 518, DateTimeKind.Utc).AddTicks(3754));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 8, 11, 25, 9, 518, DateTimeKind.Utc).AddTicks(3218));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 8, 11, 25, 9, 518, DateTimeKind.Utc).AddTicks(4376));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsDirectPrintingEnabled",
                table: "Imprimantes",
                newName: "IsBranchOfficeEnabled");

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 5, 12, 51, 55, 457, DateTimeKind.Utc).AddTicks(477));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 5, 12, 51, 55, 456, DateTimeKind.Utc).AddTicks(9869));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 5, 12, 51, 55, 457, DateTimeKind.Utc).AddTicks(1248));
        }
    }
}
