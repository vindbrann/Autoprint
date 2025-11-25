using Autoprint.Client.Data;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Autoprint.Client.Services
{
    public class DataService
    {
        private readonly string _dbPath;

        public DataService(PathService pathService)
        {
            // On définit le chemin du fichier .db dans %LocalAppData%
            _dbPath = Path.Combine(pathService.LocalCachePath, "client_cache.db");
        }

        /// <summary>
        /// Initialise la base de données (Crée le fichier s'il n'existe pas)
        /// </summary>
        public async Task InitializeAsync()
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                await context.Database.EnsureCreatedAsync();
            }
        }

        // =========================================================
        // SECTION EMPLACEMENTS (LIEUX)
        // =========================================================

        public async Task SaveEmplacementsAsync(List<Emplacement> emplacementsServeur)
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                context.Emplacements.RemoveRange(context.Emplacements);
                await context.Emplacements.AddRangeAsync(emplacementsServeur);
                await context.SaveChangesAsync();
            }
        }

        public async Task<List<Emplacement>> GetEmplacementsAsync()
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                if (await context.Database.CanConnectAsync())
                {
                    return await context.Emplacements.ToListAsync();
                }
                return new List<Emplacement>();
            }
        }

        // =========================================================
        // SECTION IMPRIMANTES (AVEC DÉ-DOUBLONNAGE)
        // =========================================================

        public async Task SaveImprimantesAsync(List<Imprimante> imprimantes)
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                // 1. Nettoyage complet
                context.Imprimantes.RemoveRange(context.Imprimantes);
                context.Modeles.RemoveRange(context.Modeles);
                context.Marques.RemoveRange(context.Marques);
                context.Pilotes.RemoveRange(context.Pilotes);

                await context.SaveChangesAsync();

                // 2. Algorithme de dé-doublonnage
                // Ces dictionnaires servent à mémoriser les objets déjà vus pour ne pas créer de doublons mémoire
                var marquesVues = new Dictionary<int, Marque>();
                var modelesVus = new Dictionary<int, Modele>();
                var pilotesVus = new Dictionary<int, Pilote>();

                foreach (var imp in imprimantes)
                {
                    // A. On détache le Lieu (car géré séparément par SaveEmplacementsAsync)
                    imp.Emplacement = null;

                    // B. On unifie les références pour Marques/Modèles/Pilotes
                    if (imp.Modele != null)
                    {
                        // --- Gestion MARQUE ---
                        if (imp.Modele.Marque != null)
                        {
                            if (marquesVues.TryGetValue(imp.Modele.Marque.Id, out var marqueExistante))
                            {
                                imp.Modele.Marque = marqueExistante; // On réutilise l'objet existant
                            }
                            else
                            {
                                marquesVues[imp.Modele.Marque.Id] = imp.Modele.Marque; // On mémorise le nouveau
                            }
                        }

                        // --- Gestion PILOTE ---
                        if (imp.Modele.Pilote != null)
                        {
                            if (pilotesVus.TryGetValue(imp.Modele.Pilote.Id, out var piloteExistant))
                            {
                                imp.Modele.Pilote = piloteExistant;
                            }
                            else
                            {
                                pilotesVus[imp.Modele.Pilote.Id] = imp.Modele.Pilote;
                            }
                        }

                        // --- Gestion MODÈLE ---
                        if (modelesVus.TryGetValue(imp.Modele.Id, out var modeleExistant))
                        {
                            imp.Modele = modeleExistant;
                        }
                        else
                        {
                            modelesVus[imp.Modele.Id] = imp.Modele;
                        }
                    }
                }

                // 3. Sauvegarde finale
                // Maintenant que tout pointe vers les mêmes objets, EF Core est content
                await context.Imprimantes.AddRangeAsync(imprimantes);
                await context.SaveChangesAsync();
            }
        }

        public async Task<List<Imprimante>> GetImprimantesAsync()
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                if (await context.Database.CanConnectAsync())
                {
                    // On charge tout l'arbre relationnel
                    return await context.Imprimantes
                        .Include(i => i.Emplacement)
                        .Include(i => i.Modele).ThenInclude(m => m.Marque)
                        .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                        .ToListAsync();
                }
                return new List<Imprimante>();
            }
        }
    }
}