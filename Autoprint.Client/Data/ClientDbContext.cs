using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Client.Data
{
    public class ClientDbContext : DbContext
    {
        private readonly string _databasePath;

        public ClientDbContext(string databasePath)
        {
            _databasePath = databasePath;
        }

        public DbSet<Emplacement> Emplacements { get; set; }
        public DbSet<Imprimante> Imprimantes { get; set; }
        public DbSet<Pilote> Pilotes { get; set; }
        public DbSet<Marque> Marques { get; set; }
        public DbSet<Modele> Modeles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_databasePath}");
        }
    }
}