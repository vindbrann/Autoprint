using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddedDiscoveryModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
               name: "Status",
               table: "Emplacements",
               type: "int",
               nullable: false,
               defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DiscoveryProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("Sqlite:Autoincrement", true),

                    Name = table.Column<string>(maxLength: 100, nullable: false),

                    TargetRanges = table.Column<string>(nullable: false),

                    ExcludedRanges = table.Column<string>(nullable: true),

                    ProbeTargets = table.Column<string>(nullable: false),

                    SkipKnownSubnets = table.Column<bool>(nullable: false),

                    ScheduleHour = table.Column<int>(nullable: false),

                    ScheduleDays = table.Column<int>(nullable: false),

                    IsEnabled = table.Column<bool>(nullable: false),

                    LastRunDate = table.Column<DateTime>(nullable: true),

                    LastRunResult = table.Column<string>(nullable: true),

                    SendEmailReport = table.Column<bool>(nullable: false),

                    EmailRecipients = table.Column<string>(nullable: true),

                    DateModification = table.Column<DateTime>(nullable: false),

                    EstSupprime = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryProfiles", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DateModification", "Status" },
                values: new object[] { new DateTime(2025, 12, 24, 14, 33, 8, 235, DateTimeKind.Utc).AddTicks(5150), 0 });

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 24, 14, 33, 8, 235, DateTimeKind.Utc).AddTicks(4337));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 24, 14, 33, 8, 235, DateTimeKind.Utc).AddTicks(5871));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveryProfiles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Emplacements");

            migrationBuilder.UpdateData(
                table: "Emplacements",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 11, 10, 21, 7, 77, DateTimeKind.Utc).AddTicks(653));

            migrationBuilder.UpdateData(
                table: "Marques",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 11, 10, 21, 7, 76, DateTimeKind.Utc).AddTicks(9776));

            migrationBuilder.UpdateData(
                table: "Modeles",
                keyColumn: "Id",
                keyValue: 1,
                column: "DateModification",
                value: new DateTime(2025, 12, 11, 10, 21, 7, 77, DateTimeKind.Utc).AddTicks(1495));
        }
    }
}