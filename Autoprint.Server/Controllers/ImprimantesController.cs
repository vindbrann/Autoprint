using Autoprint.Server.Data;
using Autoprint.Shared;
using Autoprint.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Autoprint.Server.Services;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImprimantesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;
        private readonly IPrintSpoolerService _spooler;

        public ImprimantesController(ApplicationDbContext context, AuditService auditService, IPrintSpoolerService spooler)
        {
            _context = context;
            _auditService = auditService;
            _spooler = spooler;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Imprimante>>> GetImprimantes()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return await GetImprimantesListWithIncludes();

            if (Request.Headers.TryGetValue("X-Agent-Secret", out var receivedSecret))
            {
                var setting = await _context.ServerSettings.FirstOrDefaultAsync(s => s.Key == "AgentApiKey");
                if (setting != null && receivedSecret == setting.Value)
                    return await GetImprimantesListWithIncludes();
            }
            return Unauthorized(new { message = "Accès refusé." });
        }

        private async Task<List<Imprimante>> GetImprimantesListWithIncludes()
        {
            return await _context.Imprimantes
                .AsNoTracking()
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m.Marque)
                .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "PRINTER_READ")]
        public async Task<ActionResult<Imprimante>> GetImprimante(int id)
        {
            var imprimante = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m.Marque)
                .Include(i => i.Modele).ThenInclude(m => m.Pilote)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (imprimante == null) return NotFound();
            return imprimante;
        }

        [HttpPost]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<ActionResult<Imprimante>> PostImprimante(Imprimante imprimante)
        {
            if (imprimante.ModeleId == 0) return BadRequest("Modèle obligatoire.");
            if (imprimante.EmplacementId == 0) return BadRequest("Emplacement obligatoire.");

            if (!await _context.Modeles.AnyAsync(m => m.Id == imprimante.ModeleId)) return BadRequest("Modèle introuvable.");
            if (!await _context.Emplacements.AnyAsync(e => e.Id == imprimante.EmplacementId)) return BadRequest("Emplacement introuvable.");

            imprimante.Status = PrinterStatus.PendingCreation;
            if (string.IsNullOrWhiteSpace(imprimante.NomPartage))
                imprimante.NomPartage = imprimante.NomAffiche;

            if (!imprimante.IsDirectPrintingEnabled)
            {
                var defaultSetting = await _context.ServerSettings.FirstOrDefaultAsync(s => s.Key == "Printer_DefaultDirectModeEnabled");
                if (defaultSetting != null && bool.TryParse(defaultSetting.Value, out bool defVal) && defVal)
                {
                    imprimante.IsDirectPrintingEnabled = true;
                }
            }

            imprimante.Modele = null!;
            imprimante.Emplacement = null!;

            _context.Imprimantes.Add(imprimante);
            _auditService.LogAction("PRINTER_CREATE", $"Ajout : {imprimante.NomAffiche}", User.Identity?.Name, resourceName: imprimante.NomAffiche);
            await _context.SaveChangesAsync();

            var newPrinter = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m.Marque)
                .FirstOrDefaultAsync(i => i.Id == imprimante.Id);

            return CreatedAtAction("GetImprimante", new { id = imprimante.Id }, newPrinter);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<IActionResult> PutImprimante(int id, Imprimante inputImprimante)
        {
            if (id != inputImprimante.Id) return BadRequest("ID incohérent.");

            var dbImprimante = await _context.Imprimantes.FindAsync(id);
            if (dbImprimante == null) return NotFound();

            if (!await _context.Modeles.AnyAsync(m => m.Id == inputImprimante.ModeleId)) return BadRequest("Modèle introuvable.");
            if (!await _context.Emplacements.AnyAsync(e => e.Id == inputImprimante.EmplacementId)) return BadRequest("Emplacement introuvable.");

            if (dbImprimante.Status == PrinterStatus.Synchronized || dbImprimante.Status == PrinterStatus.SyncError)
                dbImprimante.Status = PrinterStatus.PendingUpdate;

            dbImprimante.NomAffiche = inputImprimante.NomAffiche;
            dbImprimante.NomPartage = inputImprimante.NomPartage;
            dbImprimante.AdresseIp = inputImprimante.AdresseIp;
            dbImprimante.Commentaire = inputImprimante.Commentaire;
            dbImprimante.Localisation = inputImprimante.Localisation;
            dbImprimante.EstPartagee = inputImprimante.EstPartagee;
            dbImprimante.Code = inputImprimante.Code;
            dbImprimante.IsDirectPrintingEnabled = inputImprimante.IsDirectPrintingEnabled;
            dbImprimante.ModeleId = inputImprimante.ModeleId;
            dbImprimante.EmplacementId = inputImprimante.EmplacementId;

            var snapModele = await _context.Modeles.AsNoTracking().Include(m => m.Marque).Include(m => m.Pilote).FirstOrDefaultAsync(m => m.Id == inputImprimante.ModeleId);
            var snapEmplacement = await _context.Emplacements.AsNoTracking().FirstOrDefaultAsync(e => e.Id == inputImprimante.EmplacementId);

            var auditSnapshot = new Imprimante
            {
                Id = id,
                NomAffiche = dbImprimante.NomAffiche,
                NomPartage = dbImprimante.NomPartage,
                AdresseIp = dbImprimante.AdresseIp,
                Commentaire = dbImprimante.Commentaire,
                Localisation = dbImprimante.Localisation,
                EstPartagee = dbImprimante.EstPartagee,
                Code = dbImprimante.Code,
                IsDirectPrintingEnabled = dbImprimante.IsDirectPrintingEnabled,
                Status = dbImprimante.Status,
                ModeleId = dbImprimante.ModeleId,
                EmplacementId = dbImprimante.EmplacementId,
                Modele = snapModele!,
                Emplacement = snapEmplacement!
            };

            try
            {
                await _auditService.LogUpdateAsync(id, auditSnapshot, "PRINTER_UPDATE", User.Identity?.Name, "INFO", "Modele.Marque", "Modele.Pilote", "Emplacement");
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ImprimanteExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [HttpPost("Audit")]
        [Authorize(Policy = "PRINTER_SYNC")]
        public async Task<IActionResult> AuditConfiguration()
        {
            var snapshot = await _spooler.GetServerSnapshotAsync();

            var printers = await _context.Imprimantes.ToListAsync();
            int errorsFound = 0;
            int corrections = 0;

            foreach (var imp in printers)
            {
                if (imp.Status == PrinterStatus.PendingDelete) continue;

                string? windowsName = null;
                WinPrinterInfo? winInfo = null;

                if (!string.IsNullOrEmpty(imp.AdresseIp) && snapshot.PortsByIp.TryGetValue(imp.AdresseIp, out string? portName))
                {
                    if (snapshot.PrintersByPort.TryGetValue(portName, out winInfo))
                    {
                        windowsName = winInfo.Name;
                    }
                }
                if (windowsName == null && snapshot.PrintersByPort.TryGetValue(imp.AdresseIp, out winInfo))
                {
                    windowsName = winInfo.Name;
                }

                if (imp.Status == PrinterStatus.PendingCreation)
                {
                    if (windowsName != null)
                    {
                        imp.Status = PrinterStatus.Synchronized;
                        imp.Commentaire = windowsName != imp.NomAffiche
                            ? $"[AUTO-MERGE] Nom Windows '{windowsName}' détecté."
                            : null;
                        corrections++;
                    }
                    continue;
                }

                if (windowsName == null)
                {
                    if (imp.Status != PrinterStatus.SyncError)
                    {
                        imp.Status = PrinterStatus.SyncError;
                        imp.Commentaire = "[AUDIT] Introuvable sur Windows (IP non mappée).";
                        errorsFound++;
                    }
                    continue;
                }

                string portAttendu = $"IP_{imp.AdresseIp}";
                string portReel = winInfo!.PortName ?? "";

                if (!portReel.Equals(portAttendu, StringComparison.OrdinalIgnoreCase))
                {
                    imp.Status = PrinterStatus.SyncError;
                    imp.Commentaire = $"[AUDIT] Nom de port non standard : '{portReel}' (Attendu : '{portAttendu}').";
                    errorsFound++;
                }

                else if (winInfo.IsDirect != imp.IsDirectPrintingEnabled)
                {
                    imp.Status = PrinterStatus.SyncError;
                    imp.Commentaire = $"[AUDIT] Mode Direct incorrect (Win: {winInfo.IsDirect}).";
                    errorsFound++;
                }
                else if (windowsName != imp.NomAffiche)
                {
                    imp.Status = PrinterStatus.SyncError;
                    imp.Commentaire = $"[AUDIT] Nom incorrect: '{windowsName}'.";
                    errorsFound++;
                }
                else
                {
                    if (imp.Status == PrinterStatus.SyncError)
                    {
                        imp.Status = PrinterStatus.Synchronized;
                        imp.Commentaire = null;
                        corrections++;
                    }
                }
            }

            if (errorsFound > 0 || corrections > 0) await _context.SaveChangesAsync();

            return Ok(new { message = $"Audit terminé. {errorsFound} erreurs, {corrections} corrections." });
        }

        [HttpPost("ForceDirectMode")]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<IActionResult> ForceDirectMode()
        {
            var printersToUpdate = await _context.Imprimantes.Where(p => p.IsDirectPrintingEnabled == false).ToListAsync();
            if (!printersToUpdate.Any()) return Ok(new { message = "Aucune imprimante à mettre à jour." });

            foreach (var printer in printersToUpdate)
            {
                printer.IsDirectPrintingEnabled = true;
                if (printer.Status == PrinterStatus.Synchronized || printer.Status == PrinterStatus.SyncError)
                    printer.Status = PrinterStatus.PendingUpdate;
            }

            _auditService.LogAction("BULK_UPDATE", $"Forçage Mode Direct sur {printersToUpdate.Count} imprimantes", User.Identity?.Name);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"{printersToUpdate.Count} imprimantes mises à jour." });
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "PRINTER_DELETE")]
        public async Task<IActionResult> DeleteImprimante(int id)
        {
            var imprimante = await _context.Imprimantes.FindAsync(id);
            if (imprimante == null) return NotFound();

            if (imprimante.Status == PrinterStatus.PendingCreation || imprimante.Status == PrinterStatus.ImportedNeedsFix)
            {
                _context.Imprimantes.Remove(imprimante);
                _auditService.LogAction("PRINTER_DELETE", $"Suppression BDD: {imprimante.NomAffiche}", User.Identity?.Name, "WARNING", imprimante.NomAffiche);
            }
            else
            {
                imprimante.Status = PrinterStatus.PendingDelete;
                _auditService.LogAction("PRINTER_UPDATE", $"Marquage suppression: {imprimante.NomAffiche}", User.Identity?.Name, "INFO", imprimante.NomAffiche);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool ImprimanteExists(int id) => _context.Imprimantes.Any(e => e.Id == id);
    }
}