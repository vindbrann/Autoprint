using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class FixAdminPasswordHash2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3TDIGw=");
        }
    }
}
