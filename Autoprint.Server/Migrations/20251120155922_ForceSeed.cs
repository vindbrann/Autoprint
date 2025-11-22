using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class ForceSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 20, 15, 59, 21, 630, DateTimeKind.Utc).AddTicks(7889));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 20, 15, 59, 21, 630, DateTimeKind.Utc).AddTicks(7277));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 20, 15, 59, 21, 630, DateTimeKind.Utc).AddTicks(8694));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 20, 15, 56, 28, 495, DateTimeKind.Utc).AddTicks(916));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 20, 15, 56, 28, 495, DateTimeKind.Utc).AddTicks(287));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 20, 15, 56, 28, 495, DateTimeKind.Utc).AddTicks(1550));
        }
    }
}
