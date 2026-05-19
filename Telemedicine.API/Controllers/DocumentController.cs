using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using Telemedicine.Infrastructure.Data;

namespace TelemedicineSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DocumentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{documentId}/download")]
        [Authorize]
        public async Task<IActionResult> Download(Guid documentId)
        {
            var doc = await _context.Documents
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);

            if (doc == null)
                return NotFound("Документ не найден");

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), doc.FilePath);

            if (!System.IO.File.Exists(fullPath))
                return NotFound("Файл не найден на сервере");

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(bytes, "application/pdf", doc.FileName);
        }
    }
}