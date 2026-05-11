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
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Applications.Add(application);
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
                    CreatedAt = a.CreatedAt
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

        // 6. Принятые заявки консультанта
        [HttpGet("my-consultant")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetMyConsultantApplications()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultant = await _context.Consultants
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consultant == null)
                return NotFound("Консультант не найден");

            var applications = await _context.Applications
                .Include(a => a.Patient)
                .Where(a => a.ConsultantId == consultant.ConsultantId && a.Status == "accepted")
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
    }
}