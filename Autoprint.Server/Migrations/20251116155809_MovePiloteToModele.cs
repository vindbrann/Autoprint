using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class MovePiloteToModele : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Imprimantes_Pilotes_PiloteId",
                table: "Imprimantes");

            migrationBuilder.DropIndex(
                name: "IX_Imprimantes_PiloteId",
                table: "Imprimantes");

            migrationBuilder.DropColumn(
                name: "PiloteId",
                table: "Imprimantes");

            migrationBuilder.AddColumn<int>(
                name: "PiloteId",
                table: "Modeles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modeles_PiloteId",
                table: "Modeles",
                column: "PiloteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Modeles_Pilotes_PiloteId",
                table: "Modeles",
                column: "PiloteId",
                principalTable: "Pilotes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Modeles_Pilotes_PiloteId",
                table: "Modeles");

            migrationBuilder.DropIndex(
                name: "IX_Modeles_PiloteId",
                table: "Modeles");

            migrationBuilder.DropColumn(
                name: "PiloteId",
                table: "Modeles");

            migrationBuilder.AddColumn<int>(
                name: "PiloteId",
                table: "Imprimantes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Imprimantes_PiloteId",
                table: "Imprimantes",
                column: "PiloteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Imprimantes_Pilotes_PiloteId",
                table: "Imprimantes",
                column: "PiloteId",
                principalTable: "Pilotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
