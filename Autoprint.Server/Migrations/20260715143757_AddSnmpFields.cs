using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSnmpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnmpCommunity",
                table: "Imprimantes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SnmpPort",
                table: "Imprimantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SnmpVersion",
                table: "Imprimantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 7, 15, 14, 37, 55, 534, DateTimeKind.Utc).AddTicks(4925));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 7, 15, 14, 37, 55, 534, DateTimeKind.Utc).AddTicks(6713));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnmpCommunity",
                table: "Imprimantes");

            migrationBuilder.DropColumn(
                name: "SnmpPort",
                table: "Imprimantes");

            migrationBuilder.DropColumn(
                name: "SnmpVersion",
                table: "Imprimantes");

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 1, 22, 17, 14, 53, 761, DateTimeKind.Utc).AddTicks(1993));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 1, 22, 17, 14, 53, 761, DateTimeKind.Utc).AddTicks(2560));
        }
    }
}
