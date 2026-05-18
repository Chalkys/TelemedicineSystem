using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Telemedicine.Infrastructure.Data;

namespace TelemedicineSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MkbController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MkbController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("search")]
        [Authorize]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 2)
                return Ok(new object[] { });

            var results = await _context.Diseases
                .Where(d => d.Name.Contains(query) || d.MkbCode.StartsWith(query))
                .Take(20)
                .Select(d => new { d.DiseaseId, d.MkbCode, d.Name })
                .ToListAsync();

            return Ok(results);
        }
    }
}