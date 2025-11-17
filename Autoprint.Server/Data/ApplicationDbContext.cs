using Microsoft.EntityFrameworkCore;
using Autoprint.Shared; // Assure-toi que tes entités de base sont ici
using Autoprint.Server.Models.Security; // On ajoute le namespace des nouvelles classes

namespace Autoprint.Server.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // --- TES TABLES EXISTANTES ---
        public DbSet<Marque> Marques { get; set; }
        public DbSet<Modele> Modeles { get; set; }
        public DbSet<Emplacement> Emplacements { get; set; }
        public DbSet<Pilote> Pilotes { get; set; }
        public DbSet<Imprimante> Imprimantes { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ServerSetting> ServerSettings { get; set; }

        // --- NOUVELLES TABLES DE SÉCURITÉ ---
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<AdRoleMapping> AdRoleMappings { get; set; }

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- CONFIGURATION DES CLÉS COMPOSITES (Many-to-Many) ---
            modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
            modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.PermissionId });

            // --- SEEDING (Données de démarrage pour la sécurité) ---

            // 1. Créer les permissions de base (Liste non exhaustive, on en ajoutera)
            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = 1, Code = "ADMIN_ACCESS", Description = "Accès complet au système" },
                new Permission { Id = 2, Code = "PRINTER_READ", Description = "Voir les imprimantes" },
                new Permission { Id = 3, Code = "PRINTER_WRITE", Description = "Modifier les imprimantes" }
            );

            // 2. Créer un Rôle "Super Admin"
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "SuperAdmin", Description = "Administrateur Global" }
            );

            // 3. Donner la permission ADMIN au rôle SuperAdmin
            modelBuilder.Entity<RolePermission>().HasData(
                new RolePermission { RoleId = 1, PermissionId = 1 }
            );

            // 4. Créer un Utilisateur Local par défaut
            // Login: "admin" / Pass: "admin123" (Hashé en SHA256 pour l'exemple, on fera mieux plus tard)
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    DisplayName = "Administrateur Local",
                    PasswordHash = "jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=",
                    IsAdUser = false,
                    IsActive = true
                }
            );

            // 5. Mettre l'admin dans le groupe Admin
            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { UserId = 1, RoleId = 1 }
            );

            // --- TA CONFIG EXISTANTE ---
            modelBuilder.Entity<ServerSetting>().HasData(
                new ServerSetting { Key = "SmtpHost", Value = "localhost", Description = "Adresse SMTP", Type = "STRING" },
                new ServerSetting { Key = "SmtpPort", Value = "25", Description = "Port SMTP", Type = "INT" },
                new ServerSetting { Key = "SmtpEnableSsl", Value = "false", Description = "SSL/TLS", Type = "BOOL" },
                new ServerSetting { Key = "SmtpIgnoreCertError", Value = "false", Description = "Ignorer Certificat", Type = "BOOL" },
                new ServerSetting { Key = "SmtpAuthRequired", Value = "false", Description = "Auth requise ?", Type = "BOOL" },
                new ServerSetting { Key = "SmtpUser", Value = "", Description = "User SMTP", Type = "STRING" },
                new ServerSetting { Key = "SmtpPass", Value = "", Description = "Password SMTP", Type = "PASSWORD" },
                new ServerSetting { Key = "SmtpFromAddress", Value = "noreply@autoprint.local", Description = "Email Expéditeur", Type = "STRING" },
                new ServerSetting { Key = "SmtpDisplayName", Value = "Autoprint Server", Description = "Nom Affiché", Type = "STRING" },
                new ServerSetting { Key = "DriverPath", Value = "drivers", Description = "Dossier Pilotes", Type = "STRING" },
                new ServerSetting { Key = "NamingTemplate", Value = "IMP_{LIEU}_{MODELE}", Description = "Gabarit", Type = "STRING" },
                new ServerSetting { Key = "NamingEnabled", Value = "false", Description = "Activer le nommage automatique", Type = "BOOL" },
                new ServerSetting { Key = "NamingSameShare", Value = "false", Description = "Forcer le nom de partage", Type = "BOOL" }
            );
        }
    }
}