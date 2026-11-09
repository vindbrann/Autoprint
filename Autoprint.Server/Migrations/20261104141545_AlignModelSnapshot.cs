using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable


namespace Autoprint.Server.Migrations
{
    /// <inheritdoc />
    public partial class AlignModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration d'alignement (backlog B2) : aucune opération.
            // Les colonnes et tables ajoutées depuis juillet 2026 (SecurityStamp, IsFromAd, IsColor, SerialNumber,
            // SnmpProfileItems, SnmpCredentials, liens SnmpCredentialId, paramètres « Monitoring_* »…) sont créées par
            // DbInitializer avec des scripts idempotents, sur les bases neuves comme existantes. Cette migration ne sert
            // qu'à remettre l'instantané du modèle (ApplicationDbContextModelSnapshot) en phase, pour que les prochaines
            // migrations générées par « dotnet ef migrations add » ne contiennent que leurs propres changements.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rien à annuler (voir Up).
        }
    }
}
