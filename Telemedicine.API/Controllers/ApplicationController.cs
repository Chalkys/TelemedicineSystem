using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.Core.DTOs;
using TelemedicineSystem.Core.Entities;


namespace TelemedicineSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ApplicationController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Создать заявку (пациент)
        [HttpPost("create")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> CreateApplication([FromBody] CreateApplicationDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return NotFound("Пациент не найден");

            var application = new Application
            {
                ApplicationId = Guid.NewGuid(),
                PatientId = patient.PatientId,
                Type = dto.Type,
                Subject = dto.Subject,
                ConsultantId = dto.ConsultantId,
                ConsultationDate = dto.ConsultationDate.HasValue
                    ? DateTime.SpecifyKind(dto.ConsultationDate.Value.AddHours(-3), DateTimeKind.Utc)
                    : null,
                Description = dto.Description,
                Status = "accepted",  // ← сразу accepted
                CreatedAt = DateTime.UtcNow
            };

            _context.Applications.Add(application);

            // Автоматически создаём консультацию
            if (dto.ConsultantId.HasValue)
            {
                var consultation = new Consultation
                {
                    ConsultationId = Guid.NewGuid(),
                    ApplicationId = application.ApplicationId,
                    PatientId = patient.PatientId,
                    ConsultantId = dto.ConsultantId.Value,
                    Date = dto.ConsultationDate.HasValue
                        ? DateTime.SpecifyKind(dto.ConsultationDate.Value.AddHours(-3), DateTimeKind.Utc)
                        : DateTime.UtcNow,
                    Status = "scheduled"
                };
                _context.Consultations.Add(consultation);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Заявка создана", applicationId = application.ApplicationId });
        }

        // 2. Мои заявки (пациент)
        [HttpGet("my")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetMyApplications()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return NotFound("Пациент не найден");

            var applications = await _context.Applications
                .Where(a => a.PatientId == patient.PatientId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ApplicationDto
                {
                    ApplicationId = a.ApplicationId,
                    PatientId = a.PatientId,
                    PatientFullName = patient.Surname + " " + patient.Name + " " + patient.MiddleName,
                    Type = a.Type,
                    Subject = a.Subject,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt,
                    ConsultationDate = _context.Consultations
                        .Where(c => c.ApplicationId == a.ApplicationId)
                        .Select(c => (DateTime?)c.Date)
                        .FirstOrDefault(),
                    ConsultationCompleted = _context.Consultations
                        .Any(c => c.ApplicationId == a.ApplicationId && c.Status == "completed")
                })
                .ToListAsync();

            return Ok(applications);
        }

        // 3. Все заявки (консультант) — берёт заявку в работу
        [HttpGet("all")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetAllApplications()
        {
            var applications = await _context.Applications
                .Include(a => a.Patient)
                .Where(a => a.Status == "pending")
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ApplicationDto
                {
                    ApplicationId = a.ApplicationId,
                    PatientId = a.PatientId,
                    PatientFullName = a.Patient.Surname + " " + a.Patient.Name + " " + a.Patient.MiddleName,
                    Type = a.Type,
                    Subject = a.Subject,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            return Ok(applications);
        }

        // 4. Принять заявку (консультант)
        [HttpPost("{applicationId}/accept")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> AcceptApplication(Guid applicationId)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultant = await _context.Consultants
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consultant == null)
                return NotFound("Консультант не найден");

            var application = await _context.Applications
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (application == null)
                return NotFound("Заявка не найдена");

            if (application.Status != "pending")
                return BadRequest("Заявка уже обработана");

            application.Status = "accepted";

            // Создаём консультацию
            var consultation = new Consultation
            {
                ConsultationId = Guid.NewGuid(),
                ApplicationId = application.ApplicationId,
                PatientId = application.PatientId,
                ConsultantId = consultant.ConsultantId,
                Date = DateTime.UtcNow.AddDays(1), // По умолчанию завтра, можно поменять
                Status = "scheduled"
            };

            _context.Consultations.Add(consultation);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Заявка принята, консультация создана", consultationId = consultation.ConsultationId });
        }

        // 5. Отклонить заявку (консультант)
        [HttpPost("{applicationId}/reject")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> RejectApplication(Guid applicationId)
        {
            var application = await _context.Applications
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (application == null)
                return NotFound("Заявка не найдена");

            if (application.Status != "pending")
                return BadRequest("Заявка уже обработана");

            application.Status = "rejected";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Заявка отклонена" });
        }

        // 6. Новые заявки консультанта
        [HttpGet("my-new")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetMyNewApplications()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultant = await _context.Consultants
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consultant == null)
                return NotFound("Консультант не найден");

            var data = await _context.Applications
                .Include(a => a.Patient)
                .Where(a => a.ConsultantId == consultant.ConsultantId
                            && a.Status == "accepted"
                            && !_context.Consultations.Any(c => c.ApplicationId == a.ApplicationId && c.Status == "completed"))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ApplicationDto
                {
                    ApplicationId = a.ApplicationId,
                    PatientId = a.PatientId,
                    PatientFullName = a.Patient.Surname + " " + a.Patient.Name + " " + a.Patient.MiddleName,
                    Type = a.Type,
                    Subject = a.Subject,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt,
                    ConsultationDate = _context.Consultations
                        .Where(c => c.ApplicationId == a.ApplicationId)
                        .Select(c => (DateTime?)c.Date)
                        .FirstOrDefault(),
                    IsPrimary = !_context.Entries
                        .Any(e => e.TreatmentCourse.PatientId == a.PatientId
                                  && e.TreatmentCourse.ConsultantId == consultant.ConsultantId),
                    HasEntry = false
                })
                .ToListAsync();

            return Ok(data);
        }

        // 7. Завершённые консультации
        [HttpGet("my-completed")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetMyCompletedApplications()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultant = await _context.Consultants
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consultant == null)
                return NotFound("Консультант не найден");

            // Получаем ID завершённых консультаций этого консультанта
            var completedConsultationIds = await _context.Consultations
                .Where(c => c.ConsultantId == consultant.ConsultantId && c.Status == "completed")
                .Select(c => c.ConsultationId)
                .ToListAsync();

            var data = await _context.Applications
                .Include(a => a.Patient)
                .Where(a => a.ConsultantId == consultant.ConsultantId
                            && _context.Consultations.Any(c => c.ApplicationId == a.ApplicationId && c.Status == "completed"))
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var result = data.Select(a => new ApplicationDto
            {
                ApplicationId = a.ApplicationId,
                PatientId = a.PatientId,
                PatientFullName = a.Patient.Surname + " " + a.Patient.Name + " " + a.Patient.MiddleName,
                Type = a.Type,
                Subject = a.Subject,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                ConsultationDate = _context.Consultations
                    .Where(c => c.ApplicationId == a.ApplicationId)
                    .Select(c => (DateTime?)c.Date)
                    .FirstOrDefault(),
                IsPrimary = !_context.Entries
                    .Any(e => e.TreatmentCourse.PatientId == a.PatientId
                              && e.TreatmentCourse.ConsultantId == consultant.ConsultantId),
                HasEntry = _context.Entries
                    .Any(e => e.ConsultationId != null
                              && completedConsultationIds.Contains(e.ConsultationId.Value)
                              && _context.Consultations
                                  .Where(c => c.ConsultationId == e.ConsultationId.Value)
                                  .Select(c => c.ApplicationId)
                                  .FirstOrDefault() == a.ApplicationId)
            }).ToList();

            return Ok(result);
        }

        // 8. Завершённые консультации с записью (для вкладки "Завершённые")
        [HttpGet("my-completed-with-record")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetMyCompletedWithRecord()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultant = await _context.Consultants
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consultant == null)
                return NotFound("Консультант не найден");

            var completedConsultationIds = await _context.Consultations
                .Where(c => c.ConsultantId == consultant.ConsultantId && c.Status == "completed")
                .Select(c => c.ConsultationId)
                .ToListAsync();

            // ID консультаций, по которым уже есть запись
            var consultationsWithEntry = await _context.Entries
                .Where(e => e.ConsultationId != null && completedConsultationIds.Contains(e.ConsultationId.Value))
                .Select(e => e.ConsultationId.Value)
                .Distinct()
                .ToListAsync();

            var applicationIds = await _context.Consultations
                .Where(c => consultationsWithEntry.Contains(c.ConsultationId))
                .Select(c => c.ApplicationId)
                .ToListAsync();

            var data = await _context.Applications
                .Include(a => a.Patient)
                .Where(a => applicationIds.Contains(a.ApplicationId))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ApplicationDto
                {
                    ApplicationId = a.ApplicationId,
                    PatientId = a.PatientId,
                    PatientFullName = a.Patient.Surname + " " + a.Patient.Name + " " + a.Patient.MiddleName,
                    Type = a.Type,
                    Subject = a.Subject,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt,
                    ConsultationDate = _context.Consultations
                        .Where(c => c.ApplicationId == a.ApplicationId)
                        .Select(c => (DateTime?)c.Date)
                        .FirstOrDefault(),
                    IsPrimary = !_context.Entries
                        .Any(e => e.TreatmentCourse.PatientId == a.PatientId
                                  && e.TreatmentCourse.ConsultantId == consultant.ConsultantId),
                    HasEntry = true
                })
                .ToListAsync();

            return Ok(data);
        }
    }
}