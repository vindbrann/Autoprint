using Autoprint.Server.Helpers;
using Autoprint.Server.Data;
using Autoprint.Server.Hubs;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Autoprint.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "LOCATION_READ")]
    public class EmplacementsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;
        private readonly IHubContext<EventsHub> _hubContext;

        public EmplacementsController(
            ApplicationDbContext context,
            AuditService auditService,
            IHubContext<EventsHub> hubContext)
        {
            _context = context;
            _auditService = auditService;
            _hubContext = hubContext;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Emplacement>>> GetEmplacements()
        {
            var query = _context.Emplacements.Select(e => new Emplacement
            {
                Id = e.Id,
                Nom = e.Nom,
                Code = e.Code,
                Networks = e.Networks,
                Status = e.Status,
                PrinterCount = _context.Imprimantes.Count(p => p.EmplacementId == e.Id)
            });

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return await query.ToListAsync();
            }

            if (await AgentAuth.IsValidAgentRequestAsync(Request, _context))
            {
                return await query.ToListAsync();
            }

            return Unauthorized(new { message = "Accès refusé. Authentification ou Clé API requise." });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Emplacement>> GetEmplacement(int id)
        {
            var emplacement = await _context.Emplacements
                .Include(e => e.Networks)
                .FirstOrDefaultAsync(e => e.Id == id);
            return emplacement == null ? NotFound() : emplacement;
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "LOCATION_WRITE")]
        public async Task<IActionResult> PutEmplacement(int id, Emplacement emplacement)
        {
            if (id != emplacement.Id) return BadRequest();
            var doublon = await DoublonAsync(emplacement.Nom, id);
            if (doublon != null) return doublon;

            var existingLieu = await _context.Emplacements
                .Include(e => e.Networks)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (existingLieu == null) return NotFound();

            existingLieu.Nom = emplacement.Nom;
            existingLieu.Code = emplacement.Code;
            existingLieu.Status = emplacement.Status;
            existingLieu.DateModification = DateTime.UtcNow;
            // existingLieu.ModifiePar = ... (Si tu gères l'utilisateur)

            var networkIdsToSend = emplacement.Networks.Select(n => n.Id).Where(i => i != 0).ToList();
            var networksToDelete = existingLieu.Networks.Where(n => !networkIdsToSend.Contains(n.Id)).ToList();

            foreach (var net in networksToDelete)
            {
                _context.EmplacementNetworks.Remove(net);
            }

            foreach (var netDto in emplacement.Networks)
            {
                if (netDto.Id == 0)
                {
                    existingLieu.Networks.Add(new EmplacementNetwork
                    {
                        CidrIpv4 = netDto.CidrIpv4,
                        Description = netDto.Description
                    });
                }
                else
                {
                    var existingNet = existingLieu.Networks.FirstOrDefault(n => n.Id == netDto.Id);
                    if (existingNet != null)
                    {
                        existingNet.CidrIpv4 = netDto.CidrIpv4;
                        existingNet.Description = netDto.Description;
                    }
                }
            }

            try
            {
                await _auditService.LogUpdateAsync(
                    id,
                    emplacement,
                    "LOCATION_UPDATE",
                    User.Identity?.Name);

                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("RefreshPrinters");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmplacementExists(id)) return NotFound();
                else throw;
            }
            return NoContent();
        }

        // Deux lieux du même nom refusés (recette du 12/11 : doublons acceptés pour les marques ; même contrôle ici)
        private async Task<ActionResult?> DoublonAsync(string? nom, int idExclu)
        {
            var n = (nom ?? "").Trim();
            if (n.Length == 0) return BadRequest(new { message = "Nom du lieu obligatoire." });
            var upper = n.ToUpper();
            if (await _context.Emplacements.AnyAsync(e => e.Id != idExclu && e.Nom.ToUpper() == upper))
                return Conflict(new { message = $"Le lieu « {n} » existe déjà." });
            return null;
        }

        [HttpPost]
        [Authorize(Policy = "LOCATION_WRITE")]
        public async Task<ActionResult<Emplacement>> PostEmplacement(Emplacement emplacement)
        {
            var doublon = await DoublonAsync(emplacement.Nom, 0);
            if (doublon != null) return doublon;

            _context.Emplacements.Add(emplacement);

            _auditService.LogAction(
                "LOCATION_CREATE",
                $"Création lieu : {emplacement.Nom} ({emplacement.Code})",
                User.Identity?.Name,
                resourceName: emplacement.Nom
            );

            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("RefreshPrinters");

            return CreatedAtAction("GetEmplacement", new { id = emplacement.Id }, emplacement);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "LOCATION_DELETE")]
        public async Task<IActionResult> DeleteEmplacement(int id)
        {
            var emplacement = await _context.Emplacements.FindAsync(id);
            if (emplacement == null) return NotFound();

            var imprimantes = await _context.Imprimantes.Where(i => i.EmplacementId == id).ToListAsync();
            bool requiresSync = false;

            foreach (var imp in imprimantes)
            {
                if (imp.Status == PrinterStatus.PendingCreation || imp.Status == PrinterStatus.ImportedNeedsFix)
                {
                    _context.Imprimantes.Remove(imp);
                }
                else
                {
                    imp.Status = PrinterStatus.PendingDelete;
                    imp.DateModification = DateTime.UtcNow;
                    imp.ModifiePar = User.Identity?.Name ?? "Système";
                    requiresSync = true;
                }
            }

            if (requiresSync)
            {
                emplacement.Status = LieuStatus.Inactive;
                emplacement.DateModification = DateTime.UtcNow;

                _auditService.LogAction("LOCATION_UPDATE", $"Lieu Inactif (attente nettoyage Spouleur) : {emplacement.Nom}", User.Identity?.Name, "WARNING", resourceName: emplacement.Nom);

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("RefreshPrinters");

                return BadRequest("Ce lieu contient des imprimantes actives sur Windows. Elles ont été marquées 'À supprimer'. Le lieu a été passé en INACTIF. Veuillez lancer une Synchronisation pour nettoyer Windows, puis vous pourrez supprimer ce lieu définitivement.");
            }
            else
            {
                _auditService.LogAction("LOCATION_DELETE", $"Suppression lieu : {emplacement.Nom}", User.Identity?.Name, "WARNING", resourceName: emplacement.Nom);

                _context.Emplacements.Remove(emplacement);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("RefreshPrinters");

                return NoContent();
            }
        }

        private bool EmplacementExists(int id) => _context.Emplacements.Any(e => e.Id == id);

        [HttpGet("CheckCidr")]
        public async Task<ActionResult<bool>> CheckCidrAvailability(string cidr, int excludeLieuId = 0)
        {
            var exists = await _context.EmplacementNetworks
                .AnyAsync(n => n.CidrIpv4 == cidr && n.EmplacementId != excludeLieuId);

            return !exists;
        }
    }
}