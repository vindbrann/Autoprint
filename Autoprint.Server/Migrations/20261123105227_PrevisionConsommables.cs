using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class PrevisionConsommables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration d'alignement (#51), comme ProfilsSnmpAutomatiques : aucune opération ici.
            // La table SupplyHistories et la colonne SnmpProfiles.SupplyLeadTimeDays sont créées par DbInitializer avec des
            // scripts idempotents (SQL Server) et par SqliteSchemaReconciler (SQLite). Cette migration remet seulement
            // l'instantané du modèle en phase.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rien à annuler (voir Up).
        }
    }
}
