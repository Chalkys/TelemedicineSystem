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
    public class MedicalBookController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MedicalBookController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Получить свою медкарту (пациент)
        [HttpGet("my")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetMyMedicalBook()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return NotFound("Пациент не найден");

            var book = await _context.MedicalBooks
                .Include(b => b.Entries)
                    .ThenInclude(e => e.Consultant)
                .FirstOrDefaultAsync(b => b.PatientId == patient.PatientId);

            if (book == null)
                return Ok(new { entries = new object[] { }, message = "Медкарта ещё не создана" });

            var result = new
            {
                book.MedicalBookId,
                book.CreationDate,
                book.Description,
                Entries = book.Entries.OrderByDescending(e => e.CreatedAt).Select(e => new
                {
                    e.EntryId,
                    e.Conclusion,
                    e.Meds,
                    e.Procedures,
                    e.Recommendations,
                    e.TreatmentStart,
                    e.TreatmentEnd,
                    e.CauseOfAnEnd,
                    e.CreatedAt,
                    ConsultantFullName = e.Consultant.Surname + " " + e.Consultant.Name + " " + e.Consultant.MiddleName,
                    e.Consultant.Specialty
                })
            };

            return Ok(result);
        }

        // 2. Добавить запись в медкарту пациента (консультант)
        [HttpPost("entry")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> AddEntry([FromBody] AddEntryDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultant = await _context.Consultants
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consultant == null)
                return NotFound("Консультант не найден");

            // Найти или создать медкарту пациента
            var book = await _context.MedicalBooks
                .FirstOrDefaultAsync(b => b.PatientId == dto.PatientId);

            if (book == null)
            {
                book = new MedicalBook
                {
                    MedicalBookId = Guid.NewGuid(),
                    PatientId = dto.PatientId,
                    CreationDate = DateTime.UtcNow,
                    Description = "Медицинская карта"
                };
                _context.MedicalBooks.Add(book);
                await _context.SaveChangesAsync();
            }

            var entry = new Entry
            {
                EntryId = Guid.NewGuid(),
                MedicalBookId = book.MedicalBookId,
                ConsultationId = dto.ApplicationId.HasValue
                    ? (await _context.Consultations
                        .Where(c => c.ApplicationId == dto.ApplicationId.Value)
                        .Select(c => (Guid?)c.ConsultationId)
                        .FirstOrDefaultAsync())
                    : null,
                ConsultantId = consultant.ConsultantId,
                Conclusion = dto.Conclusion,
                Meds = dto.Meds,
                Procedures = dto.Procedures,
                Recommendations = dto.Recommendations,
                TreatmentStart = dto.TreatmentStart,
                TreatmentEnd = dto.TreatmentEnd,
                CauseOfAnEnd = dto.CauseOfAnEnd,
                CreatedAt = DateTime.UtcNow
            };

            _context.Entries.Add(entry);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Запись добавлена", entryId = entry.EntryId });
        }

        // 3. Получить медкарту пациента (консультант)
        [HttpGet("patient/{patientId}")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetPatientMedicalBook(Guid patientId)
        {
            var book = await _context.MedicalBooks
                .Include(b => b.Entries)
                    .ThenInclude(e => e.Consultant)
                .FirstOrDefaultAsync(b => b.PatientId == patientId);

            if (book == null)
                return Ok(new { entries = new object[] { }, message = "Медкарта не найдена" });

            var patient = await _context.Patients.FindAsync(patientId);

            var result = new
            {
                book.MedicalBookId,
                book.CreationDate,
                book.Description,
                PatientFullName = patient?.Surname + " " + patient?.Name + " " + patient?.MiddleName,
                Entries = book.Entries.OrderByDescending(e => e.CreatedAt).Select(e => new
                {
                    e.EntryId,
                    e.Conclusion,
                    e.Meds,
                    e.Procedures,
                    e.Recommendations,
                    e.TreatmentStart,
                    e.TreatmentEnd,
                    e.CauseOfAnEnd,
                    e.CreatedAt,
                    ConsultantFullName = e.Consultant.Surname + " " + e.Consultant.Name + " " + e.Consultant.MiddleName
                })
            };

            return Ok(result);
        }

        // 4. Список пациентов консультанта
        [HttpGet("my-patients")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetMyPatients()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultant = await _context.Consultants
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consultant == null)
                return NotFound("Консультант не найден");

            // Пациенты из принятых заявок
            var patientIds = await _context.Applications
                .Where(a => a.ConsultantId == consultant.ConsultantId && a.Status == "accepted")
                .Select(a => a.PatientId)
                .Distinct()
                .ToListAsync();

            var patients = await _context.Patients
                .Where(p => patientIds.Contains(p.PatientId))
                .Select(p => new
                {
                    p.PatientId,
                    FullName = p.Surname + " " + p.Name + " " + p.MiddleName,
                    p.DateOfBirth,
                    p.Gender
                })
                .ToListAsync();

            return Ok(patients);
        }
    }

    // DTO
    public class AddEntryDto
    {
        public Guid PatientId { get; set; }
        public Guid? ApplicationId { get; set; }
        public string Conclusion { get; set; }
        public string Meds { get; set; }
        public string Procedures { get; set; }
        public string Recommendations { get; set; }
        public DateTime? TreatmentStart { get; set; }
        public DateTime? TreatmentEnd { get; set; }
        public string CauseOfAnEnd { get; set; }
    }
}