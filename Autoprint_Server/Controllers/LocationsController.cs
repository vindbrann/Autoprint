using Microsoft.AspNetCore.Mvc;
using Autoprint.Server.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationsController : ControllerBase
    {

        private readonly ApplicationDbContext _context;

        public LocationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Location>>> GetLocations()
        {
            return await _context.Locations.ToListAsync();
        }
    }
}