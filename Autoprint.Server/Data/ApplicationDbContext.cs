using Autoprint.Server.Helpers;
using Autoprint.Server.Models.Security;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;

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

            modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
            modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.PermissionId });

            // --- SEEDING (Données de démarrage pour la sécurité) ---

            modelBuilder.Entity<Permission>().HasData(
                // --- ADMINISTRATION GLOBALE ---
                new Permission { Id = 1, Code = "ADMIN_ACCESS", Description = "Accès complet (SuperAdmin)" },

                // --- IMPRIMANTES ---
                new Permission { Id = 2, Code = "PRINTER_READ", Description = "Voir les imprimantes" },
                new Permission { Id = 3, Code = "PRINTER_WRITE", Description = "Ajouter/Modifier des imprimantes" },
                new Permission { Id = 4, Code = "PRINTER_DELETE", Description = "Supprimer des imprimantes" },

                // --- LIEUX (Locations) ---
                new Permission { Id = 5, Code = "LOCATION_READ", Description = "Voir les lieux" },
                new Permission { Id = 6, Code = "LOCATION_WRITE", Description = "Ajouter/Modifier des lieux" },
                new Permission { Id = 7, Code = "LOCATION_DELETE", Description = "Supprimer des lieux" },

                // --- PILOTES & MODÈLES (Drivers) ---
                new Permission { Id = 8, Code = "DRIVER_READ", Description = "Voir les pilotes et modèles" },
                new Permission { Id = 9, Code = "DRIVER_WRITE", Description = "Uploader/Modifier métadonnées pilotes" },
                new Permission { Id = 10, Code = "DRIVER_DELETE", Description = "Supprimer pilotes de la BDD" },
                // NOUVEAU : Actions Système Windows
                new Permission { Id = 15, Code = "DRIVER_INSTALL", Description = "Installer dans Windows (PnPUtil)" },
                new Permission { Id = 16, Code = "DRIVER_UNINSTALL", Description = "Désinstaller de Windows" },

                // --- UTILISATEURS (Users) ---
                new Permission { Id = 11, Code = "USER_READ", Description = "Voir les utilisateurs" },
                new Permission { Id = 12, Code = "USER_WRITE", Description = "Créer/Modifier utilisateurs" },
                new Permission { Id = 13, Code = "USER_DELETE", Description = "Supprimer utilisateurs" },

                // --- RÔLES & PERMISSIONS (Roles) ---
                new Permission { Id = 17, Code = "ROLE_READ", Description = "Voir les rôles et permissions" },
                new Permission { Id = 18, Code = "ROLE_WRITE", Description = "Créer/Modifier des rôles" },
                new Permission { Id = 19, Code = "ROLE_DELETE", Description = "Supprimer des rôles" },

                // --- MARQUES (Brands) ---
                new Permission { Id = 20, Code = "BRAND_READ", Description = "Voir les marques" },
                new Permission { Id = 21, Code = "BRAND_WRITE", Description = "Ajouter/Modifier des marques" },
                new Permission { Id = 22, Code = "BRAND_DELETE", Description = "Supprimer des marques" },

                // --- MODÈLES (Models) ---
                new Permission { Id = 23, Code = "MODEL_READ", Description = "Voir les modèles" },
                new Permission { Id = 24, Code = "MODEL_WRITE", Description = "Ajouter/Modifier des modèles" },
                new Permission { Id = 25, Code = "MODEL_DELETE", Description = "Supprimer des modèles" },

                // --- PARAMÈTRES SERVEUR (Settings) ---
                new Permission { Id = 14, Code = "SETTINGS_MANAGE", Description = "Gérer la configuration serveur (SMTP, Chemins...)" }
            );

            // 2. Créer un Rôle "Super Admin"
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "SuperAdmin", Description = "Administrateur Global" }
            );

            // 3. Assigner TOUTES les permissions
            var adminPermissions = new List<RolePermission>();
            for (int i = 1; i <= 25; i++)
            {
                adminPermissions.Add(new RolePermission { RoleId = 1, PermissionId = i });
            }
            modelBuilder.Entity<RolePermission>().HasData(adminPermissions);

            // 4. Créer un Utilisateur Local par défaut
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    DisplayName = "Administrateur Local",
                    PasswordHash = SecurityHelper.ComputeSha256Hash("admin123"),
                    IsAdUser = false,
                    IsActive = true
                }
            );

            // 5. Mettre l'admin dans le groupe SuperAdmin
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