using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.Core.Entities;

namespace TelemedicineSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConsultationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ConsultationController(AppDbContext context)
        {
            _context = context;
        }

        // Мои консультации (для пациента и консультанта)
        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> GetMyConsultations()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            var consultations = await _context.Consultations
                .Include(c => c.Application)
                .Include(c => c.Patient)
                .Include(c => c.Consultant)
                .Where(c => (role == "Patient" && c.Patient.UserId == userId) ||
                            (role == "Consultant" && c.Consultant.UserId == userId))
                .OrderByDescending(c => c.Date)
                .Select(c => new
                {
                    c.ConsultationId,
                    c.ApplicationId,
                    c.Date,
                    c.Status,
                    PatientFullName = c.Patient.Surname + " " + c.Patient.Name + " " + c.Patient.MiddleName,
                    ConsultantFullName = c.Consultant.Surname + " " + c.Consultant.Name + " " + c.Consultant.MiddleName,
                    ConsultantSpecialty = c.Consultant.Specialty,
                    Subject = c.Application != null ? c.Application.Subject : ""
                })
                .ToListAsync();

            return Ok(consultations);
        }

        // Получить сообщения консультации
        [HttpGet("{consultationId}/messages")]
        [Authorize]
        public async Task<IActionResult> GetMessages(Guid consultationId)
        {
            var messages = await _context.ConsultationMessages
                .Where(m => m.ConsultationId == consultationId)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.Text,
                    m.SenderId,
                    m.SentAt
                })
                .ToListAsync();

            return Ok(messages);
        }

        // Начать консультацию (консультант)
        [HttpPost("{consultationId}/start")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> StartConsultation(Guid consultationId)
        {
            var consultation = await _context.Consultations
                .FirstOrDefaultAsync(c => c.ConsultationId == consultationId);

            if (consultation == null)
                return NotFound("Консультация не найдена");

            if (consultation.Status != "scheduled")
                return BadRequest("Консультацию можно начать только из статуса 'scheduled'");

            consultation.Status = "in_progress";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Консультация началась" });
        }

        // Загрузка документа в консультацию
        [HttpPost("{consultationId}/upload")]
        [Authorize]
        public async Task<IActionResult> UploadDocument(Guid consultationId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Файл не выбран");

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Проверяем, что пользователь — участник консультации
            var consultation = await _context.Consultations
                .FirstOrDefaultAsync(c => c.ConsultationId == consultationId);

            if (consultation == null)
                return NotFound("Консультация не найдена");

            var isPatient = await _context.Patients.AnyAsync(p => p.UserId == userId && p.PatientId == consultation.PatientId);
            var isConsultant = await _context.Consultants.AnyAsync(c => c.UserId == userId && c.ConsultantId == consultation.ConsultantId);

            if (!isPatient && !isConsultant)
                return Forbid("Вы не участник этой консультации");

            // Сохраняем файл
            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine("Uploads", fileName);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var document = new Document
            {
                DocumentId = Guid.NewGuid(),
                ConsultationId = consultationId,
                FileName = file.FileName,
                FilePath = filePath,
                DocType = "document",
                UploadDate = DateTime.UtcNow,
                UploadedBy = userId
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Документ загружен", documentId = document.DocumentId, fileName = file.FileName });
        }

        // Список документов консультации
        [HttpGet("{consultationId}/documents")]
        [Authorize]
        public async Task<IActionResult> GetDocuments(Guid consultationId)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultation = await _context.Consultations
                .FirstOrDefaultAsync(c => c.ConsultationId == consultationId);

            if (consultation == null)
                return NotFound("Консультация не найдена");

            var docs = await _context.Documents
                .Where(d => d.ConsultationId == consultationId)
                .OrderByDescending(d => d.UploadDate)
                .Select(d => new
                {
                    d.DocumentId,
                    d.FileName,
                    d.DocType,
                    d.UploadDate
                })
                .ToListAsync();

            return Ok(docs);
        }

        // Завершить консультацию
        [HttpPost("{consultationId}/complete")]
        [Authorize]
        public async Task<IActionResult> CompleteConsultation(Guid consultationId)
        {
            var consultation = await _context.Consultations
                .FirstOrDefaultAsync(c => c.ConsultationId == consultationId);

            if (consultation == null)
                return NotFound("Консультация не найдена");

            consultation.Status = "completed";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Консультация завершена" });
        }
    }
}