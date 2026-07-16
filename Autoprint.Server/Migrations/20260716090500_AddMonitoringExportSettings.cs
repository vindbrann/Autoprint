using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringExportSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ServerSettings",
                columns: new[] { "Key", "Description", "Type", "Value" },
                values: new object[,]
                {
                    { "Monitoring_IncludeOffline", "Inclure les imprimantes hors lignes dans l'export API", "BOOL", "true" },
                    { "Monitoring_IncludeWarning", "Inclure les alertes / avertissements dans l'export API", "BOOL", "true" },
                    { "Monitoring_IncludeCritical", "Inclure les pannes critiques dans l'export API", "BOOL", "true" },
                    { "Monitoring_IncludeArchived", "Inclure les imprimantes archivées dans l'export API", "BOOL", "false" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "ServerSettings", keyColumn: "Key", keyValue: "Monitoring_IncludeOffline");
            migrationBuilder.DeleteData(table: "ServerSettings", keyColumn: "Key", keyValue: "Monitoring_IncludeWarning");
            migrationBuilder.DeleteData(table: "ServerSettings", keyColumn: "Key", keyValue: "Monitoring_IncludeCritical");
            migrationBuilder.DeleteData(table: "ServerSettings", keyColumn: "Key", keyValue: "Monitoring_IncludeArchived");
        }
    }
}
