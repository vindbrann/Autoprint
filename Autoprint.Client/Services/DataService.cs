using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Autoprint.Client.Data;
using Autoprint.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Client.Services
{
    public class DataService
    {
        private readonly string _dbPath;

        public DataService(PathService pathService)
        {
            _dbPath = Path.Combine(pathService.LocalCachePath, "client_cache.db");
        }

        public async Task UpdateCacheAsync(List<Emplacement> lieux, List<Imprimante> imprimantes)
        {
            try
            {
                await SaveDataInternalAsync(lieux, imprimantes);
            }
            catch (Exception ex) when (ex is DbUpdateException || ex is SqliteException)
            {
                System.Diagnostics.Debug.WriteLine($"[CACHE] Schéma obsolète détecté ({ex.Message}). Régénération du cache...");

                await ResetDatabaseAsync();

                try
                {
                    await SaveDataInternalAsync(lieux, imprimantes);
                    System.Diagnostics.Debug.WriteLine("[CACHE] Cache régénéré et mis à jour avec succès.");
                }
                catch (Exception exRetry)
                {
                    System.Diagnostics.Debug.WriteLine($"[CACHE] Échec fatal après régénération : {exRetry.Message}");
                    throw; 
                }
            }
        }

        private async Task SaveDataInternalAsync(List<Emplacement> lieux, List<Imprimante> imprimantes)
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                await context.Database.EnsureCreatedAsync();

                using (var transaction = await context.Database.BeginTransactionAsync())
                {
                    await context.Imprimantes.ExecuteDeleteAsync();
                    await context.Modeles.ExecuteDeleteAsync();
                    await context.Marques.ExecuteDeleteAsync();
                    await context.Pilotes.ExecuteDeleteAsync();
                    await context.Emplacements.ExecuteDeleteAsync();

                    await context.Emplacements.AddRangeAsync(lieux);
                    await context.SaveChangesAsync();

                    var marquesVues = new Dictionary<int, Marque>();
                    var pilotesVus = new Dictionary<int, Pilote>();
                    var modelesVus = new Dictionary<int, Modele>();

                    foreach (var imp in imprimantes)
                    {
                        imp.Emplacement = null;

                        if (imp.Modele != null)
                        {
                            if (imp.Modele.Marque != null)
                            {
                                if (marquesVues.TryGetValue(imp.Modele.Marque.Id, out var m))
                                    imp.Modele.Marque = m;
                                else
                                    marquesVues[imp.Modele.Marque.Id] = imp.Modele.Marque;
                            }

                            if (imp.Modele.Pilote != null)
                            {
                                if (pilotesVus.TryGetValue(imp.Modele.Pilote.Id, out var p))
                                    imp.Modele.Pilote = p;
                                else
                                    pilotesVus[imp.Modele.Pilote.Id] = imp.Modele.Pilote;
                            }

                            if (modelesVus.TryGetValue(imp.Modele.Id, out var mod))
                                imp.Modele = mod;
                            else
                                modelesVus[imp.Modele.Id] = imp.Modele;
                        }
                    }

                    await context.Imprimantes.AddRangeAsync(imprimantes);
                    await context.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
            }
        }

        public async Task InitializeDatabaseAsync()
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                try
                {
                    await context.Database.EnsureCreatedAsync();
                    await context.Imprimantes.FirstOrDefaultAsync();
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ BDD Corrompue ou obsolète au démarrage. Réinitialisation...");
                    await ResetDatabaseAsync();
                }
            }
        }

        private async Task ResetDatabaseAsync()
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            await Task.Delay(200); 

            try
            {
                if (File.Exists(_dbPath))
                    File.Delete(_dbPath);

                using (var context = new ClientDbContext(_dbPath))
                {
                    await context.Database.EnsureCreatedAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RESET DB ERROR] Impossible de supprimer le fichier DB : {ex.Message}");
            }
        }

        public async Task<List<Emplacement>> GetEmplacementsAsync()
        {
            using (var context = new ClientDbContext(_dbPath))
            {
                try
                {
                    return await context.Emplacements.ToListAsync();
                }
                catch
                {
                    return new List<Emplacement>();
                }
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
                catch
                {
                    return new List<Imprimante>();
                }
            }
        }
    }
}