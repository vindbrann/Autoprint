using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddModifieParColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModifiePar",
                table: "SystemErrors",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiePar",
                table: "Pilotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiePar",
                table: "Modeles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiePar",
                table: "Marques",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiePar",
                table: "Imprimantes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiePar",
                table: "Emplacements",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiePar",
                table: "DiscoveryProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DateModification", "ModifiePar" },
                values: new object[] { new DateTime(2026, 1, 16, 15, 57, 30, 398, DateTimeKind.Utc).AddTicks(6673), null });

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DateModification", "ModifiePar" },
                values: new object[] { new DateTime(2026, 1, 16, 15, 57, 30, 398, DateTimeKind.Utc).AddTicks(6074), null });

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DateModification", "ModifiePar" },
                values: new object[] { new DateTime(2026, 1, 16, 15, 57, 30, 398, DateTimeKind.Utc).AddTicks(7272), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModifiePar",
                table: "SystemErrors");

            migrationBuilder.DropColumn(
                name: "ModifiePar",
                table: "Pilotes");

            migrationBuilder.DropColumn(
                name: "ModifiePar",
                table: "Modeles");

            migrationBuilder.DropColumn(
                name: "ModifiePar",
                table: "Marques");

            migrationBuilder.DropColumn(
                name: "ModifiePar",
                table: "Imprimantes");

            migrationBuilder.DropColumn(
                name: "ModifiePar",
                table: "Emplacements");

            migrationBuilder.DropColumn(
                name: "ModifiePar",
                table: "DiscoveryProfiles");

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 24, 14, 33, 8, 235, DateTimeKind.Utc).AddTicks(5150));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 24, 14, 33, 8, 235, DateTimeKind.Utc).AddTicks(4337));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 24, 14, 33, 8, 235, DateTimeKind.Utc).AddTicks(5871));
        }
    }
}
