using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeAdMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdGroupName",
                table: "AdRoleMappings");

            migrationBuilder.AddColumn<string>(
                name: "AdIdentifier",
                table: "AdRoleMappings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MappingType",
                table: "AdRoleMappings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 24, 9, 7, 24, 899, DateTimeKind.Utc).AddTicks(2759));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 24, 9, 7, 24, 899, DateTimeKind.Utc).AddTicks(2182));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 24, 9, 7, 24, 899, DateTimeKind.Utc).AddTicks(3358));

            migrationBuilder.InsertData(
                table: "ServerSettings",
                columns: new[] { "Key", "Description", "Type", "Value" },
                values: new object[,]
                {
                    { "AdAdminEmails", "Mails alerte panne AD (séparés par ;)", "STRING", "" },
                    { "AdDomain", "Domaine Active Directory", "STRING", "mondomaine.lan" },
                    { "AdServicePassword", "Mot de passe AD", "PASSWORD", "" },
                    { "AdServiceUser", "Compte lecture AD", "STRING", "" },
                    { "AdUseServiceAccount", "Utiliser un compte spécifique ?", "BOOL", "false" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdRoleMappings_AdIdentifier",
                table: "AdRoleMappings",
                column: "AdIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_AdRoleMappings_AdIdentifier_RoleId",
                table: "AdRoleMappings",
                columns: new[] { "AdIdentifier", "RoleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdRoleMappings_AdIdentifier",
                table: "AdRoleMappings");

            migrationBuilder.DropIndex(
                name: "IX_AdRoleMappings_AdIdentifier_RoleId",
                table: "AdRoleMappings");

            migrationBuilder.DeleteData(
                table: "ServerSettings",
                keyColumn: "Key",
                keyValue: "AdAdminEmails");

            migrationBuilder.DeleteData(
                table: "ServerSettings",
                keyColumn: "Key",
                keyValue: "AdDomain");

            migrationBuilder.DeleteData(
                table: "ServerSettings",
                keyColumn: "Key",
                keyValue: "AdServicePassword");

            migrationBuilder.DeleteData(
                table: "ServerSettings",
                keyColumn: "Key",
                keyValue: "AdServiceUser");

            migrationBuilder.DeleteData(
                table: "ServerSettings",
                keyColumn: "Key",
                keyValue: "AdUseServiceAccount");

            migrationBuilder.DropColumn(
                name: "AdIdentifier",
                table: "AdRoleMappings");

            migrationBuilder.DropColumn(
                name: "MappingType",
                table: "AdRoleMappings");

            migrationBuilder.AddColumn<string>(
                name: "AdGroupName",
                table: "AdRoleMappings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 22, 11, 24, 12, 658, DateTimeKind.Utc).AddTicks(297));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 22, 11, 24, 12, 657, DateTimeKind.Utc).AddTicks(9777));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 22, 11, 24, 12, 658, DateTimeKind.Utc).AddTicks(894));
        }
    }
}
