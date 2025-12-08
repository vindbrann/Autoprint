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
                 await context.Database.EnsureCreatedAsync();
                try
                {
                    string sqlPatch = "ALTER TABLE Imprimantes ADD COLUMN IsBranchOfficeEnabled INTEGER NOT NULL DEFAULT 0;";

                    await context.Database.ExecuteSqlRawAsync(sqlPatch);

                    System.Diagnostics.Debug.WriteLine("✅ Patch BDD appliqué : Colonne IsBranchOfficeEnabled ajoutée.");
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ Patch BDD ignoré (Colonne déjà présente ou autre erreur).");
                }
            }
        }
        public async Task UpdateCacheAsync(List<Emplacement> lieux, List<Imprimante> imprimantes)
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                context.Imprimantes.RemoveRange(context.Imprimantes);
                context.Modeles.RemoveRange(context.Modeles);
                context.Marques.RemoveRange(context.Marques);
                context.Pilotes.RemoveRange(context.Pilotes);

                context.Emplacements.RemoveRange(context.Emplacements);

                await context.SaveChangesAsync();
                await context.Emplacements.AddRangeAsync(lieux);
                var marquesVues = new Dictionary<int, Marque>();
                var modelesVus = new Dictionary<int, Modele>();
                var pilotesVus = new Dictionary<int, Pilote>();

                foreach (var imp in imprimantes)
                {
                    imp.Emplacement = null;

                    if (imp.Modele != null)
                    {
                        if (imp.Modele.Marque != null)
                        {
                            if (marquesVues.TryGetValue(imp.Modele.Marque.Id, out var m)) imp.Modele.Marque = m;
                            else marquesVues[imp.Modele.Marque.Id] = imp.Modele.Marque;
                        }
                        if (imp.Modele.Pilote != null)
                        {
                            if (pilotesVus.TryGetValue(imp.Modele.Pilote.Id, out var p)) imp.Modele.Pilote = p;
                            else pilotesVus[imp.Modele.Pilote.Id] = imp.Modele.Pilote;
                        }
                        if (modelesVus.TryGetValue(imp.Modele.Id, out var mod)) imp.Modele = mod;
                        else modelesVus[imp.Modele.Id] = imp.Modele;
                    }
                }

                await context.Imprimantes.AddRangeAsync(imprimantes);

                await context.SaveChangesAsync();
            }
        }

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
                        .Include(i => i.Emplacement)
                        .Include(i => i.Modele).ThenInclude(m => m.Marque)
                        .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                        .ToListAsync();
                }
                catch { return new List<Imprimante>(); }
            }
        }
    }
}