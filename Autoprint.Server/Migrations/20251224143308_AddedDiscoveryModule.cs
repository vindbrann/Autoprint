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
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetRanges = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExcludedRanges = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProbeTargets = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SkipKnownSubnets = table.Column<bool>(type: "bit", nullable: false),
                    ScheduleHour = table.Column<int>(type: "int", nullable: false),
                    ScheduleDays = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastRunDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SendEmailReport = table.Column<bool>(type: "bit", nullable: false),
                    EmailRecipients = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateModification = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
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
