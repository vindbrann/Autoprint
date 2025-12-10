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
            using (var context = new ClientDbContext(_dbPath))
            {
                using (var transaction = await context.Database.BeginTransactionAsync())
                {
                    try
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

                        System.Diagnostics.Debug.WriteLine($"[CACHE] Mise à jour réussie : {lieux.Count} lieux, {imprimantes.Count} imprimantes.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CACHE] ERREUR CRITIQUE : {ex.Message}");
                        throw;
                    }
                }
            }
        }

        public async Task InitializeDatabaseAsync()
        {
            bool dbEstCorrompue = false;
            using (var context = new ClientDbContext(_dbPath))
            {
                try
                {
                    await context.Database.EnsureCreatedAsync();
                    var test = await context.Imprimantes.FirstOrDefaultAsync();
                }
                catch (Exception)
                {
                    dbEstCorrompue = true;
                }
            }

            if (dbEstCorrompue)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ BDD Corrompue détectée. Tentative de réparation...");
                SqliteConnection.ClearAllPools();
                await Task.Delay(100);

                try
                {
                    using (var context = new ClientDbContext(_dbPath))
                    {
                        await context.Database.EnsureDeletedAsync();
                        await context.Database.EnsureCreatedAsync();
                    }
                    System.Diagnostics.Debug.WriteLine("✅ BDD Réparée avec succès.");
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (File.Exists(_dbPath)) File.Delete(_dbPath);
                        using (var context = new ClientDbContext(_dbPath))
                        {
                            await context.Database.EnsureCreatedAsync();
                        }
                    }
                    catch (Exception exFatal)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ ÉCHEC FATAL RÉPARATION : {exFatal.Message}");
                    }
                }
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