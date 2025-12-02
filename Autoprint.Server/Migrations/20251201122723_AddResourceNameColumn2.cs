using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceNameColumn2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // *** CORRECTION MANUELLE : Ajout de la colonne manquante ResourceName ***
            migrationBuilder.AddColumn<string>(
                name: "ResourceName",
                table: "AuditLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                defaultValue: "");
            // *************************************************************************

            // (Vos instructions UpdateData générées automatiquement restent ci-dessous)
            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 1, 12, 22, 30, 570, DateTimeKind.Utc).AddTicks(5524));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 1, 12, 22, 30, 570, DateTimeKind.Utc).AddTicks(4948));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 1, 12, 22, 30, 570, DateTimeKind.Utc).AddTicks(6132));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // *** CORRECTION MANUELLE : Suppression de la colonne ResourceName si besoin de Down ***
            migrationBuilder.DropColumn(
                name: "ResourceName",
                table: "AuditLogs");
            // *************************************************************************

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 1, 12, 16, 34, 173, DateTimeKind.Utc).AddTicks(5226));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 1, 12, 16, 34, 173, DateTimeKind.Utc).AddTicks(4636));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 1, 12, 16, 34, 173, DateTimeKind.Utc).AddTicks(5867));
        }
    }
}