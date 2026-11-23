using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class EtatConsommables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration d'alignement (#52), comme PrevisionConsommables : aucune opération ici.
            // La table SupplySnapshots est créée par DbInitializer avec un script idempotent (SQL Server) et par
            // SqliteSchemaReconciler (SQLite). Cette migration remet seulement l'instantané du modèle en phase.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rien à annuler (voir Up).
        }
    }
}
