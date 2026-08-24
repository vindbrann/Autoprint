using System;
using Autoprint.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260821121000_AddSnmpProfileItemsAndSerialNumber")]
    public partial class AddSnmpProfileItemsAndSerialNumber : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Ajouter la colonne SerialNumber sur la table Imprimantes
            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "Imprimantes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // 2. Créer la table SnmpProfileItems
            migrationBuilder.CreateTable(
                name: "SnmpProfileItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("Sqlite:Autoincrement", true),
                    SnmpProfileId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Oid = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OidMaxCapacity = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ValueType = table.Column<int>(type: "int", nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnmpProfileItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SnmpProfileItems_SnmpProfiles_SnmpProfileId",
                        column: x => x.SnmpProfileId,
                        principalTable: "SnmpProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SnmpProfileItems_SnmpProfileId",
                table: "SnmpProfileItems",
                column: "SnmpProfileId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SnmpProfileItems");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "Imprimantes");
        }
    }
}
