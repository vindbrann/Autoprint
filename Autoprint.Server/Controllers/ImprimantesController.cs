using Autoprint.Server.Helpers;
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
        private readonly ISnmpService _snmpService;
        private readonly IPredictiveService _predictiveService;
        private readonly SecretProtector _secrets;

        public ImprimantesController(ApplicationDbContext context, AuditService auditService, IPrintSpoolerService spooler, ISnmpService snmpService, IPredictiveService predictiveService, SecretProtector secrets)
        {
            _context = context;
            _auditService = auditService;
            _spooler = spooler;
            _snmpService = snmpService;
            _predictiveService = predictiveService;
            _secrets = secrets;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Imprimante>>> GetImprimantes([FromQuery] bool includeArchived = false)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return HideSnmpCommunity(await GetImprimantesListWithIncludes(includeArchived));

            if (await AgentAuth.IsValidAgentRequestAsync(Request, _context))
                return await GetImprimantesForAgent();

            return Unauthorized(new { message = "Accès refusé." });
        }

        // Vue réduite pour les postes clients (audit #11) : la clé agent est lisible par tout utilisateur d'un poste,
        // on ne renvoie donc que ce dont le client a besoin (ni IP, ni communauté SNMP, ni commentaires, ni n° de série).
        private async Task<List<Imprimante>> GetImprimantesForAgent()
        {
            return await _context.Imprimantes
                .AsNoTracking()
                .Where(i => !i.IsArchived)
                .Select(i => new Imprimante
                {
                    Id = i.Id,
                    NomAffiche = i.NomAffiche,
                    Code = i.Code,
                    AdresseIp = string.Empty,
                    SnmpCommunity = string.Empty,
                    EstPartagee = i.EstPartagee,
                    NomPartage = i.NomPartage,
                    IsDirectPrintingEnabled = i.IsDirectPrintingEnabled,
                    EmplacementId = i.EmplacementId,
                    Localisation = i.Localisation,
                    ModeleId = i.ModeleId,
                    Status = i.Status,
                    Modele = i.Modele == null ? null : new Modele
                    {
                        Id = i.Modele.Id,
                        Nom = i.Modele.Nom,
                        MarqueId = i.Modele.MarqueId,
                        Marque = i.Modele.Marque == null ? null : new Marque { Id = i.Modele.Marque.Id, Nom = i.Modele.Marque.Nom },
                        PiloteId = i.Modele.PiloteId,
                        Pilote = i.Modele.Pilote == null ? null : new Pilote { Id = i.Modele.Pilote.Id, Nom = i.Modele.Pilote.Nom, Version = i.Modele.Pilote.Version }
                    }
                })
                .ToListAsync();
        }

        private async Task<List<Imprimante>> GetImprimantesListWithIncludes(bool includeArchived)
        {
            var query = _context.Imprimantes.AsQueryable();
            if (!includeArchived)
            {
                query = query.Where(i => !i.IsArchived);
            }
            return await query
                .AsNoTracking()
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m!.Marque)
                .Include(i => i.Modele).ThenInclude(m => m!.Pilote)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "PRINTER_READ")]
        public async Task<ActionResult<Imprimante>> GetImprimante(int id)
        {
            var imprimante = await _context.Imprimantes
                .AsNoTracking()
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m!.Marque)
                .Include(i => i.Modele).ThenInclude(m => m!.Pilote)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (imprimante == null) return NotFound();
            return HideSnmpCommunity(new List<Imprimante> { imprimante })[0];
        }

        // Communauté SNMP « ancien mode » : chiffrée en base et jamais renvoyée, comme les autres secrets (B32) ;
        // une valeur vide à l'enregistrement conserve la communauté actuelle.
        private static List<Imprimante> HideSnmpCommunity(List<Imprimante> printers)
        {
            foreach (var p in printers) p.SnmpCommunity = string.Empty;
            return printers;
        }

        [HttpPost]
        [Authorize(Policy = "PRINTER_WRITE")]
        public async Task<ActionResult<Imprimante>> PostImprimante(Imprimante imprimante)
        {
            var nameError = ValidatePrinterNames(imprimante.NomAffiche, imprimante.NomPartage);
            if (nameError != null) return BadRequest(nameError);
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
            imprimante.ModifiePar = User.Identity?.Name ?? "Système";
            imprimante.SnmpCommunity = _secrets.ProtectCommunity(imprimante.SnmpCommunity); // chiffrée en base (B32)
            _context.Imprimantes.Add(imprimante);
            _auditService.LogAction("PRINTER_CREATE", $"Ajout : {imprimante.NomAffiche}", User.Identity?.Name, resourceName: imprimante.NomAffiche);
            await _context.SaveChangesAsync();

            var newPrinter = await _context.Imprimantes
                .Include(i => i.Emplacement)
                .Include(i => i.Modele).ThenInclude(m => m!.Marque)
                .FirstOrDefaultAsync(i => i.Id == imprimante.Id);

            return CreatedAtAction("GetImprimante", new { id = imprimante.Id }, newPrinter);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutImprimante(int id, Imprimante inputImprimante)
        {
            if (id != inputImprimante.Id) return BadRequest("ID incohérent.");

            var dbImprimante = await _context.Imprimantes.FindAsync(id);
            if (dbImprimante == null) return NotFound();

            bool isArchiving = dbImprimante.IsArchived != inputImprimante.IsArchived;

            if (isArchiving)
            {
                if (!User.HasClaim(c => c.Type == "Permission" && c.Value == "PRINTER_ARCHIVE"))
                {
                    return Forbid();
                }
                dbImprimante.IsArchived = inputImprimante.IsArchived;
                if (!dbImprimante.IsArchived)
                {
                    dbImprimante.MonitoringStatus = "Inconnu";
                }
            }
            else
            {
                if (!User.HasClaim(c => c.Type == "Permission" && c.Value == "PRINTER_WRITE"))
                {
                    return Forbid();
                }
            }

            if (dbImprimante.NomAffiche != inputImprimante.NomAffiche || dbImprimante.NomPartage != inputImprimante.NomPartage)
            {
                var nameError = ValidatePrinterNames(inputImprimante.NomAffiche, inputImprimante.NomPartage);
                if (nameError != null) return BadRequest(nameError);
            }

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
            dbImprimante.SerialNumber = inputImprimante.SerialNumber;
            // Profil d'identifiants SNMP propre à l'imprimante (vide = celui du modèle)
            if (inputImprimante.SnmpCredentialId != null && !await _context.SnmpCredentials.AnyAsync(c => c.Id == inputImprimante.SnmpCredentialId))
                return BadRequest("Profil d'identifiants SNMP introuvable.");
            dbImprimante.SnmpCredentialId = inputImprimante.SnmpCredentialId;
            // Communauté jamais renvoyée par l'API : vide = valeur actuelle conservée ; sinon chiffrée avant enregistrement (B32)
            if (!string.IsNullOrWhiteSpace(inputImprimante.SnmpCommunity))
                dbImprimante.SnmpCommunity = _secrets.ProtectCommunity(inputImprimante.SnmpCommunity);
            dbImprimante.SnmpVersion = inputImprimante.SnmpVersion;
            dbImprimante.SnmpPort = inputImprimante.SnmpPort;
            dbImprimante.DateModification = DateTime.UtcNow;
            dbImprimante.ModifiePar = User.Identity?.Name ?? "Système";

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
                imprimante.ModifiePar = User.Identity?.Name ?? "Système";
                imprimante.DateModification = DateTime.UtcNow;

                _auditService.LogAction("PRINTER_UPDATE", $"Marquage suppression: {imprimante.NomAffiche}", User.Identity?.Name, "INFO", imprimante.NomAffiche);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}/diagnostic")]
        [Authorize(Policy = "PRINTER_READ")]
        public async Task<ActionResult<Autoprint.Shared.DTOs.PrinterDiagnosticResult>> GetImprimanteDiagnostic(int id)
        {
            var printer = await _context.Imprimantes
                .Include(i => i.Modele)
                .ThenInclude(m => m!.SnmpProfile)
                .ThenInclude(p => p!.Items)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (printer == null) return NotFound();

            var profiles = await SnmpCredentials.LoadProfilesAsync(_context);
            var credentials = SnmpCredentials.Resolve(printer, profiles, HttpContext.RequestServices.GetRequiredService<SecretProtector>());

            var result = await _snmpService.GetPrinterDiagnosticAsync(printer.AdresseIp, printer.SnmpPort, credentials, printer.Modele?.SnmpProfile);

            // Update database status based on diagnostic results
            if (!result.PingSuccess)
            {
                printer.MonitoringStatus = "Offline";
            }
            else
            {
                printer.LastSeen = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(result.SerialNumber))
                {
                    printer.SerialNumber = result.SerialNumber;
                }

                bool tonerLow = false;
                if (result.Toners != null)
                {
                    var todayUtc = DateTime.UtcNow.Date;
                    foreach (var toner in result.Toners)
                    {
                        if (toner.CurrentLevel >= 0)
                        {
                            if (toner.CurrentLevel <= 10)
                            {
                                tonerLow = true;
                            }

                            bool alreadyLoggedToday = await _context.TonerHistories
                                .AnyAsync(h => h.ImprimanteId == printer.Id 
                                               && h.ComponentColor == toner.Color 
                                               && h.RecordedAt >= todayUtc);

                            if (!alreadyLoggedToday)
                            {
                                _context.TonerHistories.Add(new TonerHistory
                                {
                                    ImprimanteId = printer.Id,
                                    ComponentColor = toner.Color,
                                    LevelPercent = toner.CurrentLevel,
                                    RecordedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }

                if (result.Status != null && (result.Status.Contains("Alerte") || result.Status.Contains("Inconnu")))
                {
                    printer.MonitoringStatus = "Warning";
                }
                else if (tonerLow)
                {
                    printer.MonitoringStatus = "Warning";
                }
                else if (result.Alerts != null && result.Alerts.Any(a => a.ToLower().Contains("erreur") || a.ToLower().Contains("bourrage") || a.ToLower().Contains("ouvert")))
                {
                    printer.MonitoringStatus = "Critical";
                }
                else
                {
                    printer.MonitoringStatus = "Ok";
                }
            }

            await _context.SaveChangesAsync();

            var history = await _context.TonerHistories
                .Where(h => h.ImprimanteId == id)
                .AsNoTracking()
                .ToListAsync();

            if (result.PingSuccess && result.Toners != null && result.Toners.Any())
            {
                foreach (var toner in result.Toners)
                {
                    var tonerHistory = history
                        .Where(h => h.ComponentColor.Equals(toner.Color, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    toner.EstimatedDaysRemaining = _predictiveService.PredictDaysRemaining(tonerHistory);
                }
            }

            return Ok(result);
        }

        private bool ImprimanteExists(int id) => _context.Imprimantes.Any(e => e.Id == id);

        // Règles du spouleur Windows : nom d'imprimante (220 car., sans \ ni ,) et nom de partage (80 car., jeu restreint)
        private static string? ValidatePrinterNames(string? nomAffiche, string? nomPartage)
        {
            if (string.IsNullOrWhiteSpace(nomAffiche)) return "Nom d'imprimante obligatoire.";
            if (nomAffiche.Length > 220 || nomAffiche.Any(char.IsControl) || nomAffiche.IndexOfAny(new[] { '\\', ',' }) >= 0)
                return "Nom d'imprimante invalide : 220 caractères maximum, sans \\ ni virgule.";

            if (!string.IsNullOrEmpty(nomPartage) &&
                (nomPartage.Length > 80 || nomPartage.Any(char.IsControl) || nomPartage.IndexOfAny("\\/[]:|<>+=;,?*\"".ToCharArray()) >= 0))
                return "Nom de partage invalide : 80 caractères maximum, sans \\ / [ ] : | < > + = ; , ? * \"";

            return null;
        }
    }
}