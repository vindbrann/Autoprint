using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class ProfilsSnmpAutomatiques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration d'alignement (#50), comme ConnexionSnmpProfil : aucune opération ici.
            // Les colonnes SnmpProfiles.Mode / LowThresholdPercent et la table SnmpProfileOverrides sont créées par
            // DbInitializer avec des scripts idempotents (SQL Server) et par SqliteSchemaReconciler (SQLite) ; le mode des
            // profils existants est choisi ensuite par SnmpProfileConverter. Cette migration remet seulement l'instantané
            // du modèle en phase.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rien à annuler (voir Up).
        }
    }
}
