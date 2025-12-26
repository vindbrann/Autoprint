using Autoprint.Server.Data;
using Autoprint.Server.Services;
using Autoprint.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscoveryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly DiscoveryService _discoveryService;

        public DiscoveryController(ApplicationDbContext context, DiscoveryService discoveryService)
        {
            _context = context;
            _discoveryService = discoveryService;
        }

        [HttpGet]
        public async Task<ActionResult<List<DiscoveryProfile>>> GetProfiles()
        {
            return await _context.DiscoveryProfiles.OrderBy(p => p.Name).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DiscoveryProfile>> GetProfile(int id)
        {
            var profile = await _context.DiscoveryProfiles.FindAsync(id);
            if (profile == null) return NotFound();
            return profile;
        }

        [HttpPost]
        public async Task<ActionResult<DiscoveryProfile>> SaveProfile(DiscoveryProfile profile)
        {
            if (profile.Id == 0)
            {
                profile.DateModification = DateTime.UtcNow;
                _context.DiscoveryProfiles.Add(profile);
            }
            else
            {
                var existing = await _context.DiscoveryProfiles.FindAsync(profile.Id);
                if (existing == null) return NotFound();

                existing.Name = profile.Name;
                existing.TargetRanges = profile.TargetRanges;
                existing.ExcludedRanges = profile.ExcludedRanges;
                existing.ProbeTargets = profile.ProbeTargets;
                existing.ScheduleHour = profile.ScheduleHour;
                existing.ScheduleDays = profile.ScheduleDays;
                existing.IsEnabled = profile.IsEnabled;
                existing.SkipKnownSubnets = profile.SkipKnownSubnets;
                existing.SendEmailReport = profile.SendEmailReport;
                existing.EmailRecipients = profile.EmailRecipients;
                existing.DateModification = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(profile);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProfile(int id)
        {
            var profile = await _context.DiscoveryProfiles.FindAsync(id);
            if (profile == null) return NotFound();

            _context.DiscoveryProfiles.Remove(profile);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/run")]
        public async Task<IActionResult> RunScanNow(int id)
        {
            await _discoveryService.ExecuteScanAsync(id);

            var updatedProfile = await _context.DiscoveryProfiles.FindAsync(id);
            return Ok(updatedProfile);
        }
    }
}