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

        [HttpPost("Scan/Printers")]
        [Authorize(Policy = "PRINTER_READ")]
        public async Task<ActionResult<List<DiscoveredPrinterDto>>> ScanPrinters([FromBody] RemoteScanRequestDto request)
        {
            try
            {
                var windowsPrinters = await _spoolerService.ScanPrintersAsync(request.TargetHost, request.Username, request.Password);
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
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("Import/Printers")]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<IActionResult> ImportPrinters([FromBody] List<ImportPrinterSelectionDto> selection)
        {
            int count = 0;
            var defaultLieuId = 1;
            var defaultModeleId = 1;

            if (!await _context.Emplacements.AnyAsync(e => e.Id == 1)) defaultLieuId = (await _context.Emplacements.FirstOrDefaultAsync())?.Id ?? 0;
            if (!await _context.Modeles.AnyAsync(m => m.Id == 1)) defaultModeleId = (await _context.Modeles.FirstOrDefaultAsync())?.Id ?? 0;

            foreach (var item in selection)
            {
                if (await _context.Imprimantes.AnyAsync(p => p.NomAffiche == item.Name)) continue;

                var nouvelleImp = new Imprimante
                {
                    NomAffiche = item.Name,
                    AdresseIp = item.PortName,
                    EstPartagee = item.IsShared,
                    NomPartage = item.ShareName,
                    ModeleId = item.SelectedModeleId > 0 ? item.SelectedModeleId : defaultModeleId,
                    EmplacementId = item.SelectedLieuId > 0 ? item.SelectedLieuId : defaultLieuId,
                    IsDirectPrintingEnabled = item.IsDirectPrintingEnabled,
                    Status = (item.SelectedModeleId > 0 && item.SelectedLieuId > 0)
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
            return Ok(new { Message = $"{count} imprimantes importées avec succès." });
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