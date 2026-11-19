using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class ConnexionSnmpProfil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration d'alignement (#40), comme AlignModelSnapshot : aucune opération ici.
            // Les colonnes SnmpProfiles.SnmpVersion / SnmpCommunity / SnmpCredentialId et Imprimantes.SnmpConnexionPropre,
            // ainsi que le retrait de Modeles.SnmpCredentialId, sont faits par DbInitializer avec des scripts idempotents :
            // sur une base 1.1.2, les migrations passent avant DbInitializer et les tables SNMP n'existent pas encore.
            // Cette migration remet seulement l'instantané du modèle en phase.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rien à annuler (voir Up).
        }
    }
}
