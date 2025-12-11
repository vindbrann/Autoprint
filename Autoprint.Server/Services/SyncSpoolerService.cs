using Autoprint.Server.Data;
using Autoprint.Shared.DTOs;
using Autoprint.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Autoprint.Server.Hubs;

namespace Autoprint.Server.Services
{
    public interface ISyncSpoolerService
    {
        Task<List<SyncPreviewDto>> GetPendingChangesAsync();
        Task<BatchResult> ApplyChangesAsync(List<int> idsToProcess);
    }

    public class SyncSpoolerService : ISyncSpoolerService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SyncSpoolerService> _logger;
        private readonly IHubContext<EventsHub> _hubContext;

        public SyncSpoolerService(
            IServiceScopeFactory scopeFactory,
            ILogger<SyncSpoolerService> logger,
            IHubContext<EventsHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        public async Task<List<SyncPreviewDto>> GetPendingChangesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pending = await context.Imprimantes
                .Where(i => i.Status == PrinterStatus.PendingCreation
                         || i.Status == PrinterStatus.PendingUpdate
                         || i.Status == PrinterStatus.PendingDelete
                         || i.Status == PrinterStatus.SyncError)
                .Select(i => new SyncPreviewDto
                {
                    Id = i.Id,
                    NomImprimante = i.NomAffiche,
                    Status = i.Status,
                    DateModification = i.DateModification,
                    ModifiePar = "Admin",
                    Action = i.Status == PrinterStatus.PendingCreation ? "Création" :
                             i.Status == PrinterStatus.PendingDelete ? "Suppression" :
                             i.Status == PrinterStatus.SyncError ? "Correction (Retry)" : "Mise à jour",
                    Details = i.Status == PrinterStatus.PendingCreation ? $"IP: {i.AdresseIp}" :
                              i.Status == PrinterStatus.PendingUpdate ? "Mise à jour config/nom" :
                              i.Status == PrinterStatus.SyncError ? "Nouvelle tentative de synchro" : "Suppression du serveur"
                })
                .ToListAsync();

            return pending;
        }

        public async Task<BatchResult> ApplyChangesAsync(List<int> idsToProcess)
        {
            var result = new BatchResult { Success = true, Messages = new List<string>() };

            if (idsToProcess == null || !idsToProcess.Any())
            {
                result.Messages.Add("Aucune imprimante sélectionnée.");
                return result;
            }

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var spooler = scope.ServiceProvider.GetRequiredService<IPrintSpoolerService>();

            var tasks = await context.Imprimantes
                .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                .Include(i => i.Emplacement)
                .Where(i => idsToProcess.Contains(i.Id))
                .ToListAsync();

            foreach (var imp in tasks)
            {
                try
                {
                    string winComment = imp.Localisation ?? "";
                    string winLocation = imp.Emplacement?.Nom ?? "";

                    if (imp.Status == PrinterStatus.PendingDelete)
                    {
                        await spooler.SupprimerImprimante(imp.NomAffiche);
                        context.Imprimantes.Remove(imp);
                        result.Messages.Add($"[DELETE] {imp.NomAffiche} supprimée.");
                    }

                    else if (imp.Status == PrinterStatus.PendingCreation)
                    {
                        if (string.IsNullOrEmpty(imp.AdresseIp)) throw new Exception("IP manquante");
                        if (imp.Modele?.Pilote == null) throw new Exception("Pilote manquant");

                        await spooler.CreerPortTcp(imp.AdresseIp);
                        await spooler.CreerImprimante(imp.NomAffiche, imp.Modele.Pilote.Nom, imp.AdresseIp, imp.IsDirectPrintingEnabled);
                        await spooler.ModifierImprimante(imp.NomAffiche, winComment, winLocation, imp.IsDirectPrintingEnabled);

                        bool configOk = await spooler.VerifierModeDirect(imp.NomAffiche, imp.IsDirectPrintingEnabled);
                        if (configOk)
                        {
                            imp.Status = PrinterStatus.Synchronized;
                            result.Messages.Add($"[CREATE] {imp.NomAffiche} créée.");
                        }
                        else
                        {
                            imp.Status = PrinterStatus.SyncError;
                            string etatVoulu = imp.IsDirectPrintingEnabled ? "ACTIF" : "INACTIF";
                            imp.Commentaire = $"[WARN] Windows refuse d'appliquer le Mode Filiale ({etatVoulu}). Vérifiez le type de pilote (V3 vs V4).";
                            result.Messages.Add($"[WARN] {imp.NomAffiche} : Windows bloque le mode filiale.");
                        }
                    }

                    else if (imp.Status == PrinterStatus.PendingUpdate || imp.Status == PrinterStatus.SyncError)
                    {
                        string? nomActuelSurWindows = await spooler.RecupererNomImprimanteParIp(imp.AdresseIp);

                        if (!string.IsNullOrEmpty(nomActuelSurWindows))
                        {
                            if (nomActuelSurWindows != imp.NomAffiche)
                            {
                                await spooler.RenommerImprimante(nomActuelSurWindows, imp.NomAffiche);
                                result.Messages.Add($"[RENAME] '{nomActuelSurWindows}' -> '{imp.NomAffiche}'.");
                            }

                            await spooler.ModifierImprimante(imp.NomAffiche, winComment, winLocation, imp.IsDirectPrintingEnabled);
                            result.Messages.Add($"[UPDATE] {imp.NomAffiche} mise à jour.");
                        }
                        else
                        {
                            _logger.LogWarning($"Imprimante {imp.NomAffiche} (IP: {imp.AdresseIp}) introuvable. Réparation...");
                            if (imp.Modele?.Pilote == null) throw new Exception("Pilote manquant pour réparation.");

                            await spooler.CreerPortTcp(imp.AdresseIp);
                            await spooler.CreerImprimante(imp.NomAffiche, imp.Modele.Pilote.Nom, imp.AdresseIp, imp.IsDirectPrintingEnabled);
                            await spooler.ModifierImprimante(imp.NomAffiche, winComment, winLocation, imp.IsDirectPrintingEnabled);

                            result.Messages.Add($"[REPAIR] {imp.NomAffiche} recréée.");
                        }

                        bool configOk = await spooler.VerifierModeDirect(imp.NomAffiche, imp.IsDirectPrintingEnabled);

                        if (configOk)
                        {
                            imp.Status = PrinterStatus.Synchronized;
                            imp.Commentaire = null;
                        }
                        else
                        {
                            imp.Status = PrinterStatus.SyncError;
                            string etatVoulu = imp.IsDirectPrintingEnabled ? "ACTIF" : "INACTIF";
                            imp.Commentaire = $"[WARN] Mode Filiale {etatVoulu} refusé par Windows. Vérifiez le pilote.";
                            result.Messages.Add($"[WARN] {imp.NomAffiche} : Windows bloque le mode filiale.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erreur synchro {imp.NomAffiche}");
                    imp.Status = PrinterStatus.SyncError;
                    imp.Commentaire = $"[ERREUR SYNC] {ex.Message}";
                    result.Messages.Add($"[ERREUR] {imp.NomAffiche} : {ex.Message}");
                }
            }

            await context.SaveChangesAsync();

            if (tasks.Any()) await _hubContext.Clients.All.SendAsync("RefreshPrinters");

            if (result.Messages.Any(m => m.Contains("[WARN]") || m.Contains("[ERREUR]")))
            {
                result.Success = false;
            }

            return result;
        }
    }
}