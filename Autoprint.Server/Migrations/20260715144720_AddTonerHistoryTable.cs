using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTonerHistoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TonerHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImprimanteId = table.Column<int>(type: "int", nullable: false),
                    ComponentColor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LevelPercent = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiePar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TonerHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TonerHistories_Imprimantes_ImprimanteId",
                        column: x => x.ImprimanteId,
                        principalTable: "Imprimantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 7, 15, 14, 47, 19, 423, DateTimeKind.Utc).AddTicks(4678));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 7, 15, 14, 47, 19, 423, DateTimeKind.Utc).AddTicks(6245));

            migrationBuilder.CreateIndex(
                name: "IX_TonerHistories_ImprimanteId",
                table: "TonerHistories",
                column: "ImprimanteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TonerHistories");

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 7, 15, 14, 43, 26, 927, DateTimeKind.Utc).AddTicks(7937));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2026, 7, 15, 14, 43, 26, 927, DateTimeKind.Utc).AddTicks(9135));
        }
    }
}
