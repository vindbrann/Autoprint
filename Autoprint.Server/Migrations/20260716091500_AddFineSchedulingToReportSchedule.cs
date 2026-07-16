using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddFineSchedulingToReportSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RunHour",
                table: "ReportSchedules",
                type: "int",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "RunMinute",
                table: "ReportSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RunDayOfWeek",
                table: "ReportSchedules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RunDayOfMonth",
                table: "ReportSchedules",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RunHour",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "RunMinute",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "RunDayOfWeek",
                table: "ReportSchedules");

            migrationBuilder.DropColumn(
                name: "RunDayOfMonth",
                table: "ReportSchedules");
        }
    }
}
