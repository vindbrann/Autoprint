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
                    ResourceName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                name: "SystemErrors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOccured = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemErrors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastPasswordChangeDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAdUser = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ForceChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    PasswordResetToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResetTokenExpires = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    AdIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MappingType = table.Column<int>(type: "int", nullable: false),
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
                    IsDirectPrintingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EmplacementId = table.Column<int>(type: "int", nullable: false),
                    Localisation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModeleId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                table: "Emplacements",
                columns: new[] { "Id", "CidrIpv4", "Code", "DateModification", "EstSupprime", "Nom" },
                values: new object[] { 1, "0.0.0.0/0", "ND", new DateTime(2025, 12, 11, 10, 21, 7, 77, DateTimeKind.Utc).AddTicks(653), false, "NON DÉFINI" });

            migrationBuilder.InsertData(
                table: "Marques",
                columns: new[] { "Id", "DateModification", "EstSupprime", "Nom" },
                values: new object[] { 1, new DateTime(2025, 12, 11, 10, 21, 7, 76, DateTimeKind.Utc).AddTicks(9776), false, "NON DÉFINI" });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { 1, "ADMIN_ACCESS", "Accès complet (SuperAdmin)" },
                    { 2, "PRINTER_READ", "Voir les imprimantes" },
                    { 3, "PRINTER_WRITE", "Ajouter/Modifier des imprimantes" },
                    { 4, "PRINTER_DELETE", "Supprimer des imprimantes" },
                    { 5, "PRINTER_SYNC", "Synchroniser vers Windows (Spouleur)" },
                    { 6, "LOCATION_READ", "Voir les lieux" },
                    { 7, "LOCATION_WRITE", "Ajouter/Modifier des lieux" },
                    { 8, "LOCATION_DELETE", "Supprimer des lieux" },
                    { 9, "DRIVER_READ", "Voir les pilotes" },
                    { 10, "DRIVER_SCAN", "Scanner/Mettre à jour les pilotes" },
                    { 11, "USER_READ", "Voir les utilisateurs" },
                    { 12, "USER_WRITE", "Créer/Modifier utilisateurs" },
                    { 13, "USER_DELETE", "Supprimer utilisateurs" },
                    { 14, "SETTINGS_MANAGE", "Gérer la configuration serveur" },
                    { 15, "ROLE_READ", "Voir les rôles et permissions" },
                    { 16, "ROLE_WRITE", "Créer/Modifier des rôles" },
                    { 17, "ROLE_DELETE", "Supprimer des rôles" },
                    { 18, "BRAND_READ", "Voir les marques" },
                    { 19, "BRAND_WRITE", "Ajouter/Modifier des marques" },
                    { 20, "BRAND_DELETE", "Supprimer des marques" },
                    { 21, "MODEL_READ", "Voir les modèles" },
                    { 22, "MODEL_WRITE", "Ajouter/Modifier des modèles" },
                    { 23, "MODEL_DELETE", "Supprimer des modèles" },
                    { 24, "AUDIT_READ", "Voir les logs d'audit" }
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
                    { "AdAdminEmails", "Mails alerte panne AD (séparés par ;)", "STRING", "" },
                    { "AdDomain", "Domaine Active Directory", "STRING", "domain.local" },
                    { "AdServicePassword", "Mot de passe AD", "PASSWORD", "" },
                    { "AdServiceUser", "Compte lecture AD", "STRING", "" },
                    { "AdUseServiceAccount", "Utiliser un compte spécifique ?", "BOOL", "false" },
                    { "NamingEnabled", "Activer le nommage automatique", "BOOL", "false" },
                    { "NamingSameShare", "Forcer le nom de partage", "BOOL", "false" },
                    { "NamingTemplate", "Gabarit", "STRING", "IMP_{LIEU}_{MODELE}" },
                    { "PasswordExpirationDays", "Expiration MDP (jours)", "INT", "90" },
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
                table: "Modeles",
                columns: new[] { "Id", "Code", "DateModification", "EstSupprime", "MarqueId", "Nom", "PiloteId" },
                values: new object[] { 1, null, new DateTime(2025, 12, 11, 10, 21, 7, 77, DateTimeKind.Utc).AddTicks(1495), false, 1, "GÉNÉRIQUE", null });

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
                    { 24, 1 }
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
                name: "SystemErrors");

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
