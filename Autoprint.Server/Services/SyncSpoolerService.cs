using Autoprint.Server.Data;
using Autoprint.Shared.DTOs;
using Autoprint.Shared.Enums;
using Microsoft.EntityFrameworkCore;

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

        public SyncSpoolerService(IServiceScopeFactory scopeFactory, ILogger<SyncSpoolerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<List<SyncPreviewDto>> GetPendingChangesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pending = await context.Imprimantes
                .Where(i => i.Status == PrinterStatus.PendingCreation
                         || i.Status == PrinterStatus.PendingUpdate
                         || i.Status == PrinterStatus.PendingDelete)
                .Select(i => new SyncPreviewDto
                {
                    Id = i.Id,
                    NomImprimante = i.NomAffiche,
                    Status = i.Status,
                    DateModification = i.DateModification,
                    ModifiePar = "Admin", // À connecter à User ID plus tard
                    Action = i.Status == PrinterStatus.PendingCreation ? "Création" :
                             i.Status == PrinterStatus.PendingDelete ? "Suppression" : "Mise à jour",
                    Details = i.Status == PrinterStatus.PendingCreation ? $"IP: {i.AdresseIp}" :
                              i.Status == PrinterStatus.PendingUpdate ? "Mise à jour config/nom" : "Suppression du serveur"
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

            // On charge tout ce qu'il faut, y compris l'emplacement pour le mapping
            var tasks = await context.Imprimantes
                .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                .Include(i => i.Emplacement)
                .Where(i => idsToProcess.Contains(i.Id))
                .ToListAsync();

            foreach (var imp in tasks)
            {
                try
                {
                    // --- MAPPING DES CHAMPS ---
                    // BDD "Localisation" (Détails) -> Windows "Commentaire"
                    string winComment = imp.Localisation ?? "";

                    // BDD "Emplacement.Nom" (Bâtiment) -> Windows "Location"
                    string winLocation = imp.Emplacement?.Nom ?? "";

                    // --- 1. CAS SUPPRESSION ---
                    if (imp.Status == PrinterStatus.PendingDelete)
                    {
                        await spooler.SupprimerImprimante(imp.NomAffiche);
                        context.Imprimantes.Remove(imp);
                        result.Messages.Add($"[DELETE] {imp.NomAffiche} supprimée.");
                    }

                    // --- 2. CAS CRÉATION ---
                    else if (imp.Status == PrinterStatus.PendingCreation)
                    {
                        if (string.IsNullOrEmpty(imp.AdresseIp)) throw new Exception("IP manquante");
                        if (imp.Modele?.Pilote == null) throw new Exception("Pilote manquant");

                        await spooler.CreerPortTcp(imp.AdresseIp);
                        await spooler.CreerImprimante(imp.NomAffiche, imp.Modele.Pilote.Nom, imp.AdresseIp);

                        // On applique tout de suite les infos de localisation
                        await spooler.ModifierImprimante(imp.NomAffiche, winComment, winLocation);

                        imp.Status = PrinterStatus.Synchronized;
                        result.Messages.Add($"[CREATE] {imp.NomAffiche} créée.");
                    }

                    // --- 3. CAS MISE À JOUR (INTELLIGENT) ---
                    else if (imp.Status == PrinterStatus.PendingUpdate)
                    {
                        // Stratégie : On cherche l'imprimante physique via son IP (Port)
                        // car le Nom a pu changer en BDD.
                        string? nomActuelSurWindows = await spooler.RecupererNomImprimanteParIp(imp.AdresseIp);

                        if (!string.IsNullOrEmpty(nomActuelSurWindows))
                        {
                            // TROUVÉE !

                            // A. Renommage si nécessaire
                            if (nomActuelSurWindows != imp.NomAffiche)
                            {
                                await spooler.RenommerImprimante(nomActuelSurWindows, imp.NomAffiche);
                                result.Messages.Add($"[RENAME] '{nomActuelSurWindows}' -> '{imp.NomAffiche}'.");
                            }

                            // B. Mise à jour des métadonnées (Lieu, Commentaire)
                            await spooler.ModifierImprimante(imp.NomAffiche, winComment, winLocation);
                            result.Messages.Add($"[UPDATE] {imp.NomAffiche} mise à jour.");
                        }
                        else
                        {
                            // PAS TROUVÉE (Auto-Repair)
                            // Elle a peut-être été supprimée manuellement ou l'IP a changé ?
                            // Dans le doute, on recrée propre.
                            _logger.LogWarning($"Imprimante {imp.NomAffiche} (IP: {imp.AdresseIp}) introuvable. Tentative de réparation...");

                            if (imp.Modele?.Pilote == null) throw new Exception("Pilote manquant pour réparation.");

                            // On s'assure que le port et l'imprimante existent
                            await spooler.CreerPortTcp(imp.AdresseIp);
                            await spooler.CreerImprimante(imp.NomAffiche, imp.Modele.Pilote.Nom, imp.AdresseIp);
                            await spooler.ModifierImprimante(imp.NomAffiche, winComment, winLocation);

                            result.Messages.Add($"[REPAIR] {imp.NomAffiche} recréée (Introuvable avant update).");
                        }

                        imp.Status = PrinterStatus.Synchronized;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erreur synchro {imp.NomAffiche}");
                    imp.Status = PrinterStatus.SyncError;
                    imp.Commentaire = $"[ERREUR SYNC] {ex.Message}";
                    result.Success = false;
                    result.Messages.Add($"[ERREUR] {imp.NomAffiche} : {ex.Message}");
                }
            }

            await context.SaveChangesAsync();
            return result;
        }
    }
}