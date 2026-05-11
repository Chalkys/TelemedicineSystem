using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telemedicine.Infrastructure.Data;


namespace Telemedicine.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConsultantController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ConsultantController(AppDbContext context)
        {
            _context = context;
        }

        // Список всех специальностей
        [HttpGet("specializations")]
        [Authorize]
        public async Task<IActionResult> GetSpecializations()
        {
            var specs = await _context.Consultants
                .Where(c => c.Specialty != null)
                .Select(c => c.Specialty)
                .Distinct()
                .ToListAsync();

            return Ok(specs);
        }

        // Список консультантов (все или по специальности)
        [HttpGet("list")]
        [Authorize]
        public async Task<IActionResult> GetConsultants([FromQuery] string specialty)
        {
            var query = _context.Consultants.AsQueryable();

            if (!string.IsNullOrEmpty(specialty))
            {
                query = query.Where(c => c.Specialty == specialty);
            }

            var consultants = await query
                .Select(c => new
                {
                    c.ConsultantId,
                    FullName = c.Surname + " " + c.Name + " " + c.MiddleName,
                    c.Specialty,
                    c.Post,
                    c.EducationInfo,
                    c.Seniority
                })
                .ToListAsync();

            return Ok(consultants);
        }

        // Свободные слоты консультанта на дату
        [HttpGet("{consultantId}/slots")]
        [Authorize]
        public async Task<IActionResult> GetSlots(Guid consultantId, [FromQuery] DateTime date)
        {
            var dateString = date.ToString("yyyy-MM-dd");
            var busySlots = new HashSet<string>();

            // Из заявок
            var appSlots = await _context.Applications
                .Where(a => a.ConsultantId == consultantId
                            && a.ConsultationDate.HasValue
                            && a.Status != "rejected")
                .ToListAsync();

            foreach (var a in appSlots)
            {
                if (a.ConsultationDate.HasValue && a.ConsultationDate.Value.ToString("yyyy-MM-dd") == dateString)
                    busySlots.Add(a.ConsultationDate.Value.ToString("HH:mm"));
            }

            // Из консультаций
            var consSlots = await _context.Consultations
                .Where(c => c.ConsultantId == consultantId
                            && c.Status != "cancelled")
                .ToListAsync();

            foreach (var c in consSlots)
            {
                if (c.Date.ToString("yyyy-MM-dd") == dateString)
                    busySlots.Add(c.Date.ToString("HH:mm"));
            }

            // Генерируем слоты
            var slots = new List<object>();
            var start = date.Date.AddHours(9);
            var end = date.Date.AddHours(17);

            while (start < end)
            {
                var timeStr = start.ToString("HH:mm");
                slots.Add(new
                {
                    time = timeStr,
                    available = !busySlots.Contains(timeStr)
                });
                start = start.AddMinutes(30);
            }

            return Ok(slots);
        }
    }
}
