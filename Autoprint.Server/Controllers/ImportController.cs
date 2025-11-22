using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Autoprint.Shared.Enums; // Pour PrinterStatus
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "ADMIN_ACCESS")] // Sécurité Admin
    public class ImportController : ControllerBase
    {
        private readonly IPrintSpoolerService _spoolerService;
        private readonly ApplicationDbContext _context;

        public ImportController(IPrintSpoolerService spoolerService, ApplicationDbContext context)
        {
            _spoolerService = spoolerService;
            _context = context;
        }

        // ==================================================================================
        // 1. GESTION DES IMPRIMANTES (SCAN DISTANT POSSIBLE)
        // ==================================================================================

        [HttpPost("Scan/Printers")]
        public async Task<ActionResult<List<DiscoveredPrinterDto>>> ScanPrinters([FromBody] RemoteScanRequestDto request)
        {
            try
            {
                // A. Scan Technique (Windows)
                var windowsPrinters = await _spoolerService.ScanPrintersAsync(request.TargetHost, request.Username, request.Password);

                // B. Comparaison avec la BDD (Source of Truth)
                var dbPrinters = await _context.Imprimantes.ToListAsync();

                foreach (var wp in windowsPrinters)
                {
                    // On essaie de matcher par IP (Port) OU par Nom exact
                    // Note : Le PortName WMI est souvent l'IP ("192.168.1.50")
                    var match = dbPrinters.FirstOrDefault(p =>
                        (!string.IsNullOrEmpty(p.AdresseIp) && p.AdresseIp.Equals(wp.PortName, StringComparison.OrdinalIgnoreCase)) ||
                        p.NomAffiche.Equals(wp.Name, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        wp.ExistsInDb = true;
                        wp.ExistingId = match.Id;
                    }
                }

                return Ok(windowsPrinters.OrderBy(p => p.Name));
            }
            catch (Exception ex)
            {
                // On renvoie l'erreur WMI proprement au client pour qu'il l'affiche (ex: "Accès refusé")
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("Import/Printers")]
        public async Task<IActionResult> ImportPrinters([FromBody] List<ImportPrinterSelectionDto> selection)
        {
            int count = 0;

            // Récupération des IDs par défaut "NON DÉFINI" (On suppose qu'ils ont l'ID 1, sinon on prend le premier dispo)
            var defaultLieuId = 1;
            var defaultModeleId = 1;

            // (Sécurité : Vérifier si l'ID 1 existe vraiment, sinon chercher ou créer à la volée si besoin)
            if (!await _context.Emplacements.AnyAsync(e => e.Id == 1)) defaultLieuId = (await _context.Emplacements.FirstOrDefaultAsync())?.Id ?? 0;
            if (!await _context.Modeles.AnyAsync(m => m.Id == 1)) defaultModeleId = (await _context.Modeles.FirstOrDefaultAsync())?.Id ?? 0;

            foreach (var item in selection)
            {
                // On vérifie qu'elle n'existe pas déjà pour éviter les doublons de dernière minute
                if (await _context.Imprimantes.AnyAsync(p => p.NomAffiche == item.Name)) continue;

                var nouvelleImp = new Imprimante
                {
                    NomAffiche = item.Name,
                    AdresseIp = item.PortName, // WMI PortName -> IP
                    EstPartagee = item.IsShared,
                    NomPartage = item.ShareName,

                    // Mapping Intelligent ou Par défaut
                    ModeleId = item.SelectedModeleId > 0 ? item.SelectedModeleId : defaultModeleId,
                    EmplacementId = item.SelectedLieuId > 0 ? item.SelectedLieuId : defaultLieuId,

                    // Statut : Si on a mis des IDs par défaut, il faut corriger. Sinon c'est prêt à créer.
                    Status = (item.SelectedModeleId > 0 && item.SelectedLieuId > 0)
                             ? PrinterStatus.PendingCreation
                             : PrinterStatus.ImportedNeedsFix
                };

                _context.Imprimantes.Add(nouvelleImp);
                count++;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"{count} imprimantes importées avec succès." });
        }


        // ==================================================================================
        // 2. GESTION DES PILOTES (SCAN LOCAL UNIQUEMENT + DIFF)
        // ==================================================================================

        [HttpGet("Scan/Drivers")]
        public async Task<ActionResult<List<DiscoveredDriverDto>>> ScanDrivers()
        {
            // A. Réalité Terrain (Windows)
            var windowsDrivers = await _spoolerService.ScanLocalDriversAsync();

            // B. Connaissance BDD
            var dbDrivers = await _context.Pilotes.ToListAsync();

            // C. Construction de la liste consolidée
            var resultList = new List<DiscoveredDriverDto>();

            // 1. Traiter ceux présents sur Windows
            foreach (var wd in windowsDrivers)
            {
                var match = dbDrivers.FirstOrDefault(d => d.Nom.Equals(wd.Name, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    wd.SyncStatus = "Synced"; // Vert : Tout va bien

                    // Petite subtilité : Si on l'avait marqué "Disparu" avant, on le remet "Installé"
                    if (!match.EstInstalle)
                    {
                        match.EstInstalle = true; // Auto-repair de l'état
                        // On pourrait sauvegarder ici, mais on le fera peut-être au prochain SaveChanges
                    }
                }
                else
                {
                    wd.SyncStatus = "New"; // Bleu : À importer
                }

                resultList.Add(wd);
            }

            // 2. Traiter les "Orphelins" (En BDD mais pas sur Windows)
            var orphelins = dbDrivers.Where(db => !windowsDrivers.Any(w => w.Name.Equals(db.Nom, StringComparison.OrdinalIgnoreCase)));

            foreach (var orphelin in orphelins)
            {
                resultList.Add(new DiscoveredDriverDto
                {
                    Name = orphelin.Nom,
                    DriverVersion = orphelin.Version,
                    SyncStatus = "Missing" // Rouge : Alerte
                });

                // On met à jour l'état en base pour info
                if (orphelin.EstInstalle) orphelin.EstInstalle = false;
            }

            // Sauvegarde des mises à jour d'états (EstInstalle true/false)
            await _context.SaveChangesAsync();

            return Ok(resultList.OrderBy(d => d.Name));
        }

        [HttpPost("Import/Drivers")]
        public async Task<IActionResult> ImportDrivers([FromBody] List<string> driverNamesToImport)
        {
            // On re-scanne pour être sûr d'avoir les dernières infos (versions) de Windows
            var windowsDrivers = await _spoolerService.ScanLocalDriversAsync();
            int count = 0;

            foreach (var name in driverNamesToImport)
            {
                // On ne réimporte pas s'il existe déjà
                if (await _context.Pilotes.AnyAsync(p => p.Nom == name)) continue;

                var winDriver = windowsDrivers.FirstOrDefault(w => w.Name == name);
                if (winDriver != null)
                {
                    _context.Pilotes.Add(new Pilote
                    {
                        Nom = winDriver.Name,
                        Version = winDriver.DriverVersion,
                        EstInstalle = true
                    });
                    count++;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"{count} pilotes ajoutés à la bibliothèque." });
        }
    }

    // DTO pour l'envoi de la sélection d'imprimantes depuis le client
    public class ImportPrinterSelectionDto
    {
        public string Name { get; set; } = "";
        public string PortName { get; set; } = "";
        public string ShareName { get; set; } = "";
        public bool IsShared { get; set; }

        // Les choix de l'utilisateur
        public int SelectedModeleId { get; set; }
        public int SelectedLieuId { get; set; }
    }
}