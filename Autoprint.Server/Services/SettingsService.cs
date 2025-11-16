using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    // On définit le contrat (ce que le service sait faire)
    public interface ISettingsService
    {
        Task<string> GetDriversPathAsync();
        Task UpdateDriversPathAsync(string newPath, bool deplacerFichiers);
    }

    // Le code réel
    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _context;

        public SettingsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetDriversPathAsync()
        {
            // On lit ta clé spécifique "DriverPath"
            var setting = await _context.ServerSettings.FindAsync("DriverPath");

            // Si vide, on utilise "drivers" à côté de l'application
            string path = setting?.Value ?? "drivers";

            // Si c'est un chemin relatif (ex: "drivers"), on le transforme en absolu (C:\...\drivers)
            if (!Path.IsPathFullyQualified(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            }

            return path;
        }

        public async Task UpdateDriversPathAsync(string newPath, bool deplacerFichiers)
        {
            // Nettoyage du chemin (retirer les \ à la fin)
            newPath = newPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // On récupère l'ancien chemin pour comparer
            string oldPath = await GetDriversPathAsync();

            // Si c'est pareil, on ne fait rien
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return;

            // Création du nouveau dossier si besoin
            if (!Directory.Exists(newPath)) Directory.CreateDirectory(newPath);

            // --- LOGIQUE DE MIGRATION (Si demandée par l'admin) ---
            if (deplacerFichiers && Directory.Exists(oldPath))
            {
                var pilotes = await _context.Pilotes.ToListAsync();
                foreach (var pilote in pilotes)
                {
                    // Si le pilote est bien dans l'ancien dossier, on le déplace
                    if (!string.IsNullOrEmpty(pilote.CheminFichier) &&
                        pilote.CheminFichier.StartsWith(oldPath, StringComparison.OrdinalIgnoreCase))
                    {
                        string relative = pilote.CheminFichier.Substring(oldPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        string newFile = Path.Combine(newPath, relative);
                        string newDir = Path.GetDirectoryName(newFile) ?? newPath;

                        if (!Directory.Exists(newDir)) Directory.CreateDirectory(newDir);

                        if (File.Exists(pilote.CheminFichier))
                        {
                            if (File.Exists(newFile)) File.Delete(newFile); // On écrase si existe déjà
                            File.Move(pilote.CheminFichier, newFile);
                        }

                        // On met à jour le lien en base de données
                        pilote.CheminFichier = newFile;
                    }
                }
            }

            // --- MISE À JOUR DE LA CONFIG ---
            var setting = await _context.ServerSettings.FindAsync("DriverPath");
            if (setting == null)
            {
                // Si la ligne n'existait pas, on la crée
                setting = new ServerSetting { Key = "DriverPath", Value = newPath, Description = "Dossier Pilotes", Type = "STRING" };
                _context.ServerSettings.Add(setting);
            }
            else
            {
                setting.Value = newPath;
            }

            await _context.SaveChangesAsync();
        }
    }
}