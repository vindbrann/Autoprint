using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Client.Data
{
    public class ClientDbContext : DbContext
    {
        private readonly string _databasePath;

        // On passe le chemin du fichier DB à la construction
        public ClientDbContext(string databasePath)
        {
            _databasePath = databasePath;
        }

        // On définit les tables locales (Miroir du serveur)
        public DbSet<Emplacement> Emplacements { get; set; }
        public DbSet<Imprimante> Imprimantes { get; set; }
        public DbSet<Pilote> Pilotes { get; set; }
        public DbSet<Marque> Marques { get; set; }
        public DbSet<Modele> Modeles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Configuration SQLite pointant vers le fichier défini
            optionsBuilder.UseSqlite($"Data Source={_databasePath}");
        }
    }
}