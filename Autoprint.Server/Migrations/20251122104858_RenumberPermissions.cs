using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class RenumberPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 24, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 25, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 26, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 22, 10, 48, 57, 444, DateTimeKind.Utc).AddTicks(9803));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 22, 10, 48, 57, 444, DateTimeKind.Utc).AddTicks(8799));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 22, 10, 48, 57, 445, DateTimeKind.Utc).AddTicks(936));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Code", "Description" },
                values: new object[] { "PRINTER_SYNC", "Synchroniser vers Windows (Spouleur)" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Code", "Description" },
                values: new object[] { "LOCATION_READ", "Voir les lieux" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Code", "Description" },
                values: new object[] { "LOCATION_WRITE", "Ajouter/Modifier des lieux" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Code", "Description" },
                values: new object[] { "LOCATION_DELETE", "Supprimer des lieux" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Code", "Description" },
                values: new object[] { "DRIVER_READ", "Voir les pilotes" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "Code", "Description" },
                values: new object[] { "DRIVER_WRITE", "Scanner/Mettre à jour les pilotes" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 14,
                column: "Description",
                value: "Gérer la configuration serveur");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "Code", "Description" },
                values: new object[] { "ROLE_READ", "Voir les rôles et permissions" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "Code", "Description" },
                values: new object[] { "ROLE_WRITE", "Créer/Modifier des rôles" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "Code", "Description" },
                values: new object[] { "ROLE_DELETE", "Supprimer des rôles" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "Code", "Description" },
                values: new object[] { "BRAND_READ", "Voir les marques" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "Code", "Description" },
                values: new object[] { "BRAND_WRITE", "Ajouter/Modifier des marques" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Code", "Description" },
                values: new object[] { "BRAND_DELETE", "Supprimer des marques" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "Code", "Description" },
                values: new object[] { "MODEL_READ", "Voir les modèles" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "Code", "Description" },
                values: new object[] { "MODEL_WRITE", "Ajouter/Modifier des modèles" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "Code", "Description" },
                values: new object[] { "MODEL_DELETE", "Supprimer des modèles" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 22, 8, 41, 27, 386, DateTimeKind.Utc).AddTicks(4490));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 22, 8, 41, 27, 386, DateTimeKind.Utc).AddTicks(3114));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 11, 22, 8, 41, 27, 386, DateTimeKind.Utc).AddTicks(6098));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Code", "Description" },
                values: new object[] { "LOCATION_READ", "Voir les lieux" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Code", "Description" },
                values: new object[] { "LOCATION_WRITE", "Ajouter/Modifier des lieux" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Code", "Description" },
                values: new object[] { "LOCATION_DELETE", "Supprimer des lieux" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Code", "Description" },
                values: new object[] { "DRIVER_READ", "Voir les pilotes et modèles" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Code", "Description" },
                values: new object[] { "DRIVER_WRITE", "Uploader/Modifier métadonnées pilotes" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "Code", "Description" },
                values: new object[] { "DRIVER_DELETE", "Supprimer pilotes de la BDD" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 14,
                column: "Description",
                value: "Gérer la configuration serveur (SMTP, Chemins...)");

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "Code", "Description" },
                values: new object[] { "DRIVER_INSTALL", "Installer dans Windows (PnPUtil)" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "Code", "Description" },
                values: new object[] { "DRIVER_UNINSTALL", "Désinstaller de Windows" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "Code", "Description" },
                values: new object[] { "ROLE_READ", "Voir les rôles et permissions" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "Code", "Description" },
                values: new object[] { "ROLE_WRITE", "Créer/Modifier des rôles" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "Code", "Description" },
                values: new object[] { "ROLE_DELETE", "Supprimer des rôles" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Code", "Description" },
                values: new object[] { "BRAND_READ", "Voir les marques" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "Code", "Description" },
                values: new object[] { "BRAND_WRITE", "Ajouter/Modifier des marques" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "Code", "Description" },
                values: new object[] { "BRAND_DELETE", "Supprimer des marques" });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "Code", "Description" },
                values: new object[] { "MODEL_READ", "Voir les modèles" });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { 24, "MODEL_WRITE", "Ajouter/Modifier des modèles" },
                    { 25, "MODEL_DELETE", "Supprimer des modèles" },
                    { 26, "PRINTER_SYNC", "Synchroniser vers Windows (Spouleur)" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 24, 1 },
                    { 25, 1 },
                    { 26, 1 }
                });
        }
    }
}
