using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Models;

namespace Autoprint.Server.Data 
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tes tables
        public DbSet<Marque> Marques { get; set; }
        public DbSet<Modele> Modeles { get; set; }
        public DbSet<Emplacement> Emplacements { get; set; }
        public DbSet<Pilote> Pilotes { get; set; }
        public DbSet<Imprimante> Imprimantes { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ServerSetting> ServerSettings { get; set; } // La nouveauté 1.7

        // Mise à jour automatique des dates
        public override int SaveChanges()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entityEntry in entries)
            {
                ((BaseEntity)entityEntry.Entity).DateModification = DateTime.UtcNow;
            }

            return base.SaveChanges();
        }

        // Configuration des données par défaut
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- CONFIGURATION SMTP & SYSTEME PAR DEFAUT ---
            modelBuilder.Entity<ServerSetting>().HasData(
                // 1. Connexion
                new ServerSetting { Key = "SmtpHost", Value = "localhost", Description = "Adresse SMTP", Type = "STRING" },
                new ServerSetting { Key = "SmtpPort", Value = "25", Description = "Port SMTP", Type = "INT" },

                // 2. Sécurité
                new ServerSetting { Key = "SmtpEnableSsl", Value = "false", Description = "SSL/TLS", Type = "BOOL" },
                new ServerSetting { Key = "SmtpIgnoreCertError", Value = "false", Description = "Ignorer Certificat", Type = "BOOL" },

                // 3. Auth
                new ServerSetting { Key = "SmtpAuthRequired", Value = "false", Description = "Auth requise ?", Type = "BOOL" },
                new ServerSetting { Key = "SmtpUser", Value = "", Description = "User SMTP", Type = "STRING" },
                new ServerSetting { Key = "SmtpPass", Value = "", Description = "Password SMTP", Type = "PASSWORD" },

                // 4. Expéditeur
                new ServerSetting { Key = "SmtpFromAddress", Value = "noreply@autoprint.local", Description = "Email Expéditeur", Type = "STRING" },
                new ServerSetting { Key = "SmtpDisplayName", Value = "Autoprint Server", Description = "Nom Affiché", Type = "STRING" },

                // 5. Système
                new ServerSetting { Key = "DriverPath", Value = "drivers", Description = "Dossier Pilotes", Type = "STRING" }
            );
        }
    }
}