using Autoprint.Server.Helpers;
using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Autoprint.Shared.DTOs;
using Autoprint.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImportController : ControllerBase
    {
        private readonly IPrintSpoolerService _spoolerService;
        private readonly ApplicationDbContext _context;

        public ImportController(IPrintSpoolerService spoolerService, ApplicationDbContext context)
        {
            _spoolerService = spoolerService;
            _context = context;
        }

        // Limité au spouleur local : aucun hôte cible ni identifiant, pour empêcher toute coercition NTLM du compte machine
        [HttpPost("Scan/Printers")]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<ActionResult<List<DiscoveredPrinterDto>>> ScanPrinters()
        {
            try
            {
                var windowsPrinters = await _spoolerService.ScanLocalPrintersAsync();
                var dbPrinters = await _context.Imprimantes.ToListAsync();

                foreach (var wp in windowsPrinters)
                {
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
                return BadRequest(ApiErrors.LogAndDescribe(HttpContext, ex, "Impossible de lire le spouleur local."));
            }
        }

        [HttpPost("Import/Printers")]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<IActionResult> ImportPrinters([FromBody] List<ImportPrinterSelectionDto> selection)
        {
            int count = 0;
            int skipped = 0;

            // Lieu / modèle choisis vérifiés ; à défaut, le premier existant (l'imprimante est marquée « à corriger »)
            var lieuIds = (await _context.Emplacements.Select(e => e.Id).ToListAsync()).ToHashSet();
            var modeleIds = (await _context.Modeles.Select(m => m.Id).ToListAsync()).ToHashSet();
            if (lieuIds.Count == 0 || modeleIds.Count == 0)
                return BadRequest("Créez au moins un lieu et un modèle avant d'importer des imprimantes.");

            int defaultLieuId = lieuIds.Contains(1) ? 1 : lieuIds.Min();
            int defaultModeleId = modeleIds.Contains(1) ? 1 : modeleIds.Min();

            foreach (var item in selection)
            {
                if (string.IsNullOrWhiteSpace(item.Name) || await _context.Imprimantes.AnyAsync(p => p.NomAffiche == item.Name))
                {
                    skipped++;
                    continue;
                }

                bool modeleOk = modeleIds.Contains(item.SelectedModeleId);
                bool lieuOk = lieuIds.Contains(item.SelectedLieuId);

                var nouvelleImp = new Imprimante
                {
                    NomAffiche = item.Name,
                    EstPartagee = item.IsShared,
                    NomPartage = item.ShareName,
                    AdresseIp = item.PortName ?? string.Empty,
                    ModeleId = modeleOk ? item.SelectedModeleId : defaultModeleId,
                    EmplacementId = lieuOk ? item.SelectedLieuId : defaultLieuId,
                    IsDirectPrintingEnabled = item.IsDirectPrintingEnabled,
                    Status = (modeleOk && lieuOk)
                             ? PrinterStatus.PendingCreation
                             : PrinterStatus.ImportedNeedsFix
                };

                _context.Imprimantes.Add(nouvelleImp);
                count++;
            }

            if (count > 0)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "IMPORT_PRINTERS",
                    Details = $"Importation de {count} imprimantes depuis le Spouleur Windows.",
                    Utilisateur = User.Identity?.Name ?? "Admin",
                    Niveau = "WARNING",
                    DateAction = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = skipped > 0
                ? $"{count} imprimante(s) importée(s), {skipped} ignorée(s) (déjà présentes ou sans nom)."
                : $"{count} imprimante(s) importée(s) avec succès." });
        }



        [HttpGet("Scan/Drivers")]
        [Authorize(Policy = "DRIVER_SCAN")]
        public async Task<ActionResult<List<DiscoveredDriverDto>>> ScanDrivers()
        {
            var pilotesInstalles = await _spoolerService.GetInstalledDriversAsync();

            var windowsDrivers = pilotesInstalles.Select(p => new DiscoveredDriverDto
            {
                Name = p.Nom,
                DriverVersion = p.Version
            }).ToList();

            var dbDrivers = await _context.Pilotes.ToListAsync();
            var resultList = new List<DiscoveredDriverDto>();

            foreach (var wd in windowsDrivers)
            {
                var match = dbDrivers.FirstOrDefault(d => d.Nom.Equals(wd.Name, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    wd.SyncStatus = "Synced";
                    if (!match.EstInstalle) match.EstInstalle = true;
                }
                else
                {
                    wd.SyncStatus = "New";
                }
                resultList.Add(wd);
            }

            var orphelins = dbDrivers.Where(db => !windowsDrivers.Any(w => w.Name.Equals(db.Nom, StringComparison.OrdinalIgnoreCase)));
            foreach (var orphelin in orphelins)
            {
                resultList.Add(new DiscoveredDriverDto { Name = orphelin.Nom, DriverVersion = orphelin.Version, SyncStatus = "Missing" });
                if (orphelin.EstInstalle) orphelin.EstInstalle = false;
            }

            await _context.SaveChangesAsync();
            return Ok(resultList.OrderBy(d => d.Name));
        }

        [HttpPost("Import/Drivers")]
        [Authorize(Policy = "DRIVER_SCAN")] 
        public async Task<IActionResult> ImportDrivers([FromBody] List<string> driverNamesToImport)
        {
            var pilotesInstalles = await _spoolerService.GetInstalledDriversAsync();
            int count = 0;

            foreach (var name in driverNamesToImport)
            {
                if (await _context.Pilotes.AnyAsync(p => p.Nom == name)) continue;

                var winDriver = pilotesInstalles.FirstOrDefault(w => w.Nom == name);
                if (winDriver != null)
                {
                    _context.Pilotes.Add(new Pilote
                    {
                        Nom = winDriver.Nom,
                        Version = winDriver.Version,
                        EstInstalle = true
                    });
                    count++;
                }
            }

            if (count > 0)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    Action = "IMPORT_DRIVERS",
                    Details = $"Importation manuelle de {count} pilotes.",
                    Utilisateur = User.Identity?.Name ?? "Admin",
                    Niveau = "INFO",
                    DateAction = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = $"{count} pilotes ajoutés à la bibliothèque." });
        }
    }

    public class ImportPrinterSelectionDto
    {
        public string Name { get; set; } = "";
        public string PortName { get; set; } = "";
        public string ShareName { get; set; } = "";
        public bool IsShared { get; set; }
        public int SelectedModeleId { get; set; }
        public int SelectedLieuId { get; set; }
        public bool IsDirectPrintingEnabled { get; set; }
    }
}