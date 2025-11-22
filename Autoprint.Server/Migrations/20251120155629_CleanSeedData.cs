using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class CleanSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastPasswordChangeDate",
                value: new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 20, 15, 53, 50, 927, DateTimeKind.Utc).AddTicks(5269));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 20, 15, 53, 50, 927, DateTimeKind.Utc).AddTicks(2829));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 20, 15, 53, 50, 927, DateTimeKind.Utc).AddTicks(7423));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastPasswordChangeDate",
                value: new DateTime(2020, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
        }
    }
}
