using Autoprint.Shared;
using System.Text.Json.Serialization;

namespace Autoprint.Shared.DTOs
{
    // DTO principal pour l'affichage du journal d'audit
    public class AuditLogDto
    {
        public int Id { get; set; }
        public DateTime DateAction { get; set; }
        public string Utilisateur { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Niveau { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;

        public string? OldValues { get; set; }
        public string? NewValues { get; set; }

        [JsonIgnore] // La colonne ActionLisible est calculée, pas besoin de la sérialiser
        public string ActionLisible => GetActionDescription(Action, ResourceName);

        // Dans la classe AuditLogDto :

        private static string GetActionDescription(string actionCode, string resourceName)
        {
            // Utiliser la chaîne directement (plus robuste pour les noms exacts trouvés dans le code)
            switch (actionCode)
            {
                // 1. Entités de Référence (CRUD)

                // Marques
                case "BRAND_CREATE": return $"Création de la marque : {resourceName}";
                case "BRAND_UPDATE": return $"Modification de la marque : {resourceName}";
                case "BRAND_DELETE": return $"Suppression de la marque : {resourceName}";

                // Modèles (basé sur MODEL_ trouvé)
                case "MODEL_CREATE": return $"Création du modèle : {resourceName}";
                case "MODEL_UPDATE": return $"Modification du modèle : {resourceName}";
                case "MODEL_DELETE": return $"Suppression du modèle : {resourceName}";

                // Lieux (basé sur LOCATION_ trouvé)
                case "LOCATION_CREATE": return $"Création du lieu : {resourceName}";
                case "LOCATION_UPDATE": return $"Modification du lieu : {resourceName}";
                case "LOCATION_DELETE": return $"Suppression du lieu : {resourceName}";

                // Pilotes (basé sur DRIVER_ trouvé)
                case "DRIVER_SYNC": return $"Synchronisation des pilotes";

                // 2. Imprimantes (CRUD & Sync)
                case "PRINTER_CREATE": return $"Création de l'imprimante : {resourceName}";
                case "PRINTER_UPDATE": return $"Modification de l'imprimante : {resourceName}";
                case "PRINTER_DELETE": return $"Suppression de l'imprimante : {resourceName}";

                // 3. Import/Scan
                case "IMPORT_PRINTERS": return $"Scan et Importation d'imprimantes";

                // 4. Utilisateurs / Rôles / Sécurité
                case "USER_CREATE": return $"Création de l'utilisateur : {resourceName}";
                case "USER_UPDATE": return $"Modification de l'utilisateur : {resourceName}";
                case "USER_DELETE": return $"Suppression de l'utilisateur : {resourceName}";
                case "USER_PASSWORD_RESET": return $"Réinitialisation mot de passe (Admin) : {resourceName}";
                case "USER_PWD_CHANGE": return $"Changement de mot de passe utilisateur";

                case "ROLE_CREATE": return $"Création du rôle : {resourceName}";
                case "ROLE_UPDATE": return $"Modification du rôle : {resourceName}";
                case "ROLE_DELETE": return $"Suppression du rôle : {resourceName}";

                case "ROLE_MAP_ADD": return $"Ajout d'un lien AD/Rôle : {resourceName}";
                case "ROLE_MAP_DEL": return $"Suppression d'un lien AD/Rôle : {resourceName}";

                // 5. Opérations Système
                case "PRINTER_SYNC": return $"Synchronisation des configurations Windows";
                case "LOG_PURGE": return $"Nettoyage automatique des logs";
                case "CONFIG_UPDATE": return $"Mise à jour de la Configuration Serveur";
                case "SYSTEM_RESTORE": return $"Restauration complète de la base de données";

                // 6. Actions Client/IPC (Affichées dans le log si le client écrit dans l'Audit)
                case "INSTALL": return $"Installation de l'imprimante (Client M2M) : {resourceName}";
                case "UNINSTALL": return $"Désinstallation de l'imprimante (Client M2M) : {resourceName}";

                // Défaut
                default:
                    // Si le code est introuvable, on affiche le code technique exact pour débogage futur
                    return $"CODE NON TRADUIT : {actionCode} ({resourceName})";
            }
        }
    }

    // DTO utilisé pour la requête de filtre/recherche
    public class AuditFilterDto
    {
        public string? SearchQuery { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    // DTO générique pour la pagination
    public class PaginatedList<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }
}