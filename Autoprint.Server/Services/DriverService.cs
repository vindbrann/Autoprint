using Autoprint.Server.Data;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    public class DriverService : IDriverService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPrintSpoolerService _spoolerService;
        private readonly ILogger<DriverService> _logger;

        public DriverService(ApplicationDbContext context, IPrintSpoolerService spoolerService, ILogger<DriverService> logger)
        {
            _context = context;
            _spoolerService = spoolerService;
            _logger = logger;
        }

        public async Task<BatchResult> SynchroniserPilotesAsync()
        {
            var result = new BatchResult();

            // 1. Récupération Source (Windows)
            var driversSysteme = await _spoolerService.GetInstalledDriversAsync();

            // DEBUG : On affiche tout ce que WMI a trouvé
            _logger.LogWarning("--- DEBUG WMI : LISTE DES PILOTES TROUVÉS ---");
            foreach (var d in driversSysteme)
            {
                _logger.LogWarning($"[WMI] Nom: '{d.Nom}' | Version: '{d.Version}'");
            }
            _logger.LogWarning("---------------------------------------------");

            if (driversSysteme.Count == 0)
            {
                _logger.LogWarning("⚠️ Scan WMI vide. Vérifiez le Spouleur.");
            }

            // 2. Récupération BDD (On inclut rien car on vérifie les modèles via une requête séparée)
            var driversBdd = await _context.Pilotes.ToListAsync();

            // 3. Ajouts & Mises à jour (Windows -> BDD)
            foreach (var driverSys in driversSysteme)
            {
                var driverBdd = driversBdd.FirstOrDefault(p => p.Nom.Equals(driverSys.Nom, StringComparison.OrdinalIgnoreCase));

                if (driverBdd == null)
                {
                    // NOUVEAU
                    _context.Pilotes.Add(new Pilote
                    {
                        Nom = driverSys.Nom,
                        Version = driverSys.Version,
                        EstInstalle = true
                    });
                    result.Added++;
                }
                else
                {
                    // EXISTANT : Mise à jour Version ou Réactivation
                    if (driverBdd.Version != driverSys.Version || !driverBdd.EstInstalle)
                    {
                        driverBdd.Version = driverSys.Version;
                        driverBdd.EstInstalle = true; // Il est revenu !
                        result.Updated++;
                    }
                }
            }

            // 4. Nettoyage Intelligent (BDD -> Poubelle ou Alerte)
            foreach (var driverBdd in driversBdd)
            {
                // Le pilote est-il encore présent sur Windows ?
                bool existeSurWindows = driversSysteme.Any(s => s.Nom.Equals(driverBdd.Nom, StringComparison.OrdinalIgnoreCase));

                if (!existeSurWindows)
                {
                    // ⚠️ IL A DISPARU DU SERVEUR
                    // DEBUG : Pourquoi ça ne matche pas ?
                    if (driverBdd.Nom.Contains("Microsoft"))
                    {
                        _logger.LogError($"ECHEC MATCHING : Le pilote BDD '{driverBdd.Nom}' n'a pas été trouvé dans la liste WMI ci-dessus.");
                    }
                    // On récupère la liste des modèles qui utilisent ce pilote
                    var modelesLies = await _context.Modeles
                                            .Where(m => m.PiloteId == driverBdd.Id)
                                            .Select(m => m.Nom)
                                            .ToListAsync();

                    if (modelesLies.Count == 0)
                    {
                        // CAS A : DISPARU et INUTILE -> POUBELLE
                        _context.Pilotes.Remove(driverBdd);
                        result.Deleted++;
                        _logger.LogInformation($"🗑️ Pilote nettoyé (inutilisé) : {driverBdd.Nom}");
                    }
                    else
                    {
                        // CAS B : DISPARU mais UTILISÉ -> ALERTE ROUGE
                        if (driverBdd.EstInstalle)
                        {
                            driverBdd.EstInstalle = false; // Passe en rouge
                            result.Updated++;

                            // ICI : On loggue les noms des modèles coupables
                            string coupables = string.Join(", ", modelesLies);
                            _logger.LogWarning($"🛑 Pilote '{driverBdd.Nom}' manquant mais conservé car utilisé par {modelesLies.Count} modèle(s) : [{coupables}]");
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }
    }
}