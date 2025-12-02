using Autoprint.Server.Data;
using Autoprint.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AlertsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<SystemAlertsDto>> GetAlerts()
        {
            bool hasMissingDrivers = await _context.Pilotes
                .AnyAsync(p => !p.EstInstalle);

            bool hasBrokenModels = await _context.Modeles
                .AnyAsync(m => m.Pilote != null && !m.Pilote.EstInstalle);

            return Ok(new SystemAlertsDto
            {
                DriverAlert = hasMissingDrivers,
                ModelAlert = hasBrokenModels
            });
        }
    }
}