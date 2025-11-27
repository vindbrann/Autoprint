using Autoprint.Client.Data;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;
using System;
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
            _dbPath = Path.Combine(pathService.LocalCachePath, "client_cache.db");
        }

        public async Task InitializeAsync()
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                // En développement, si le schéma change, on peut décommenter ça pour recréer la base
                // await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
            }
        }

        // =========================================================
        // MÉTHODE ATOMIQUE (Update Global)
        // =========================================================
        public async Task UpdateCacheAsync(List<Emplacement> lieux, List<Imprimante> imprimantes)
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                // 1. VIDER DANS L'ORDRE (Pour respecter les Clés Étrangères)
                // D'abord les enfants (qui dépendent des autres)
                context.Imprimantes.RemoveRange(context.Imprimantes);
                context.Modeles.RemoveRange(context.Modeles);
                context.Marques.RemoveRange(context.Marques);
                context.Pilotes.RemoveRange(context.Pilotes);

                // Ensuite les parents (maintenant qu'ils sont libres)
                context.Emplacements.RemoveRange(context.Emplacements);

                // On valide le nettoyage avant de réinsérer
                await context.SaveChangesAsync();

                // 2. RÉINSÉRER LES PARENTS (Lieux)
                await context.Emplacements.AddRangeAsync(lieux);

                // 3. PRÉPARER ET INSÉRER LES IMPRIMANTES
                // Dictionnaires pour éviter de créer des doublons d'objets en mémoire
                var marquesVues = new Dictionary<int, Marque>();
                var modelesVus = new Dictionary<int, Modele>();
                var pilotesVus = new Dictionary<int, Pilote>();

                foreach (var imp in imprimantes)
                {
                    // CRITIQUE : On détache le Lieu pour qu'EF ne tente pas de le recréer
                    // (L'ID EmplacementId suffit à faire le lien en base)
                    imp.Emplacement = null;

                    if (imp.Modele != null)
                    {
                        // Gestion Marque (Unification)
                        if (imp.Modele.Marque != null)
                        {
                            if (marquesVues.TryGetValue(imp.Modele.Marque.Id, out var m)) imp.Modele.Marque = m;
                            else marquesVues[imp.Modele.Marque.Id] = imp.Modele.Marque;
                        }
                        // Gestion Pilote (Unification)
                        if (imp.Modele.Pilote != null)
                        {
                            if (pilotesVus.TryGetValue(imp.Modele.Pilote.Id, out var p)) imp.Modele.Pilote = p;
                            else pilotesVus[imp.Modele.Pilote.Id] = imp.Modele.Pilote;
                        }
                        // Gestion Modèle (Unification)
                        if (modelesVus.TryGetValue(imp.Modele.Id, out var mod)) imp.Modele = mod;
                        else modelesVus[imp.Modele.Id] = imp.Modele;
                    }
                }

                await context.Imprimantes.AddRangeAsync(imprimantes);

                // 4. SAUVEGARDE FINALE
                await context.SaveChangesAsync();
            }
        }

        // --- LECTURE ---

        public async Task<List<Emplacement>> GetEmplacementsAsync()
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                try { return await context.Emplacements.ToListAsync(); }
                catch { return new List<Emplacement>(); }
            }
        }

        public async Task<List<Imprimante>> GetImprimantesAsync()
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                try
                {
                    return await context.Imprimantes
                        .Include(i => i.Emplacement) // Important pour avoir le nom du lieu
                        .Include(i => i.Modele).ThenInclude(m => m.Marque)
                        .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                        .ToListAsync();
                }
                catch { return new List<Imprimante>(); }
            }
        }
    }
}