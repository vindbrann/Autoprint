using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DateAction = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Utilisateur = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Niveau = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Emplacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CidrIpv4 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emplacements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Marques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marques", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pilotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CheminFichier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NomFichierInf = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EstInstalle = table.Column<bool>(type: "bit", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pilotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAdUser = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Modeles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MarqueId = table.Column<int>(type: "int", nullable: false),
                    PiloteId = table.Column<int>(type: "int", nullable: true),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modeles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Modeles_Marques_MarqueId",
                        column: x => x.MarqueId,
                        principalTable: "Marques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Modeles_Pilotes_PiloteId",
                        column: x => x.PiloteId,
                        principalTable: "Pilotes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AdRoleMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdGroupName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdRoleMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdRoleMappings_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Imprimantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NomAffiche = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdresseIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EstPartagee = table.Column<bool>(type: "bit", nullable: false),
                    NomPartage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmplacementId = table.Column<int>(type: "int", nullable: false),
                    Localisation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModeleId = table.Column<int>(type: "int", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Imprimantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Imprimantes_Emplacements_EmplacementId",
                        column: x => x.EmplacementId,
                        principalTable: "Emplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Imprimantes_Modeles_ModeleId",
                        column: x => x.ModeleId,
                        principalTable: "Modeles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { 1, "ADMIN_ACCESS", "Accès complet (SuperAdmin)" },
                    { 2, "PRINTER_READ", "Voir les imprimantes" },
                    { 3, "PRINTER_WRITE", "Ajouter/Modifier des imprimantes" },
                    { 4, "PRINTER_DELETE", "Supprimer des imprimantes" },
                    { 5, "LOCATION_READ", "Voir les lieux" },
                    { 6, "LOCATION_WRITE", "Ajouter/Modifier des lieux" },
                    { 7, "LOCATION_DELETE", "Supprimer des lieux" },
                    { 8, "DRIVER_READ", "Voir les pilotes et modèles" },
                    { 9, "DRIVER_WRITE", "Uploader/Modifier métadonnées pilotes" },
                    { 10, "DRIVER_DELETE", "Supprimer pilotes de la BDD" },
                    { 11, "USER_READ", "Voir les utilisateurs" },
                    { 12, "USER_WRITE", "Créer/Modifier utilisateurs" },
                    { 13, "USER_DELETE", "Supprimer utilisateurs" },
                    { 14, "SETTINGS_MANAGE", "Gérer la configuration serveur (SMTP, Chemins...)" },
                    { 15, "DRIVER_INSTALL", "Installer dans Windows (PnPUtil)" },
                    { 16, "DRIVER_UNINSTALL", "Désinstaller de Windows" },
                    { 17, "ROLE_READ", "Voir les rôles et permissions" },
                    { 18, "ROLE_WRITE", "Créer/Modifier des rôles" },
                    { 19, "ROLE_DELETE", "Supprimer des rôles" },
                    { 20, "BRAND_READ", "Voir les marques" },
                    { 21, "BRAND_WRITE", "Ajouter/Modifier des marques" },
                    { 22, "BRAND_DELETE", "Supprimer des marques" },
                    { 23, "MODEL_READ", "Voir les modèles" },
                    { 24, "MODEL_WRITE", "Ajouter/Modifier des modèles" },
                    { 25, "MODEL_DELETE", "Supprimer des modèles" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[] { 1, "Administrateur Global", "SuperAdmin" });

            migrationBuilder.InsertData(
                table: "ServerSettings",
                columns: new[] { "Key", "Description", "Type", "Value" },
                values: new object[,]
                {
                    { "DriverPath", "Dossier Pilotes", "STRING", "drivers" },
                    { "NamingEnabled", "Activer le nommage automatique", "BOOL", "false" },
                    { "NamingSameShare", "Forcer le nom de partage", "BOOL", "false" },
                    { "NamingTemplate", "Gabarit", "STRING", "IMP_{LIEU}_{MODELE}" },
                    { "SmtpAuthRequired", "Auth requise ?", "BOOL", "false" },
                    { "SmtpDisplayName", "Nom Affiché", "STRING", "Autoprint Server" },
                    { "SmtpEnableSsl", "SSL/TLS", "BOOL", "false" },
                    { "SmtpFromAddress", "Email Expéditeur", "STRING", "noreply@autoprint.local" },
                    { "SmtpHost", "Adresse SMTP", "STRING", "localhost" },
                    { "SmtpIgnoreCertError", "Ignorer Certificat", "BOOL", "false" },
                    { "SmtpPass", "Password SMTP", "PASSWORD", "" },
                    { "SmtpPort", "Port SMTP", "INT", "25" },
                    { "SmtpUser", "User SMTP", "STRING", "" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "DisplayName", "IsActive", "IsAdUser", "LastLogin", "PasswordHash", "Username" },
                values: new object[] { 1, "Administrateur Local", true, false, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=", "admin" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 4, 1 },
                    { 5, 1 },
                    { 6, 1 },
                    { 7, 1 },
                    { 8, 1 },
                    { 9, 1 },
                    { 10, 1 },
                    { 11, 1 },
                    { 12, 1 },
                    { 13, 1 },
                    { 14, 1 },
                    { 15, 1 },
                    { 16, 1 },
                    { 17, 1 },
                    { 18, 1 },
                    { 19, 1 },
                    { 20, 1 },
                    { 21, 1 },
                    { 22, 1 },
                    { 23, 1 },
                    { 24, 1 },
                    { 25, 1 }
                });

            migrationBuilder.InsertData(
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[] { 1, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_AdRoleMappings_RoleId",
                table: "AdRoleMappings",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Imprimantes_EmplacementId",
                table: "Imprimantes",
                column: "EmplacementId");

            migrationBuilder.CreateIndex(
                name: "IX_Imprimantes_ModeleId",
                table: "Imprimantes",
                column: "ModeleId");

            migrationBuilder.CreateIndex(
                name: "IX_Modeles_MarqueId",
                table: "Modeles",
                column: "MarqueId");

            migrationBuilder.CreateIndex(
                name: "IX_Modeles_PiloteId",
                table: "Modeles",
                column: "PiloteId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdRoleMappings");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Imprimantes");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "ServerSettings");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Emplacements");

            migrationBuilder.DropTable(
                name: "Modeles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Marques");

            migrationBuilder.DropTable(
                name: "Pilotes");
        }
    }
}
