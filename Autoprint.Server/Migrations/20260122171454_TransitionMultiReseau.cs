using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    public partial class TransitionMultiReseau : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmplacementNetworks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("Sqlite:Autoincrement", true),

                    EmplacementId = table.Column<int>(nullable: false),
                    CidrIpv4 = table.Column<string>(maxLength: 50, nullable: false),
                    Description = table.Column<string>(maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(nullable: false, defaultValue: false),
                    DateModification = table.Column<DateTime>(nullable: false),
                    ModifiePar = table.Column<string>(maxLength: 100, nullable: true),
                    EstSupprime = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmplacementNetworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmplacementNetworks_Emplacements_EmplacementId",
                        column: x => x.EmplacementId,
                        principalTable: "Emplacements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    @"INSERT INTO EmplacementNetworks (EmplacementId, CidrIpv4, Description, IsPrimary, DateModification, EstSupprime)
                      SELECT Id, CidrIpv4, 'Réseau Principal', 1, datetime('now'), 0
                      FROM Emplacements
                      WHERE CidrIpv4 IS NOT NULL AND CidrIpv4 <> '' AND CidrIpv4 <> '0.0.0.0/0'"
                );
            }
            else
            {
                migrationBuilder.Sql(
                    @"INSERT INTO EmplacementNetworks (EmplacementId, CidrIpv4, Description, IsPrimary, DateModification, EstSupprime)
                      SELECT Id, CidrIpv4, 'Réseau Principal', 1, GETUTCDATE(), 0
                      FROM Emplacements
                      WHERE CidrIpv4 IS NOT NULL AND CidrIpv4 <> '' AND CidrIpv4 <> '0.0.0.0/0'"
                );
            }

            migrationBuilder.CreateIndex(
                name: "IX_EmplacementNetworks_EmplacementId",
                table: "EmplacementNetworks",
                column: "EmplacementId");

            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql("ALTER TABLE Emplacements DROP COLUMN CidrIpv4;");
            }
            else
            {
                migrationBuilder.DropColumn(
                    name: "CidrIpv4",
                    table: "Emplacements");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CidrIpv4",
                table: "Emplacements",
                maxLength: 50,
                nullable: false,
                defaultValue: "0.0.0.0/0");

            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                   @"UPDATE Emplacements
                      SET CidrIpv4 = (SELECT CidrIpv4 FROM EmplacementNetworks WHERE EmplacementNetworks.EmplacementId = Emplacements.Id AND EmplacementNetworks.IsPrimary = 1 LIMIT 1)"
               );
            }
            else
            {
                migrationBuilder.Sql(
                    @"UPDATE e
                      SET e.CidrIpv4 = (SELECT TOP 1 n.CidrIpv4 FROM EmplacementNetworks n WHERE n.EmplacementId = e.Id AND n.IsPrimary = 1)
                      FROM Emplacements e"
                );
            }

            migrationBuilder.DropTable(
                name: "EmplacementNetworks");
        }
    }
}