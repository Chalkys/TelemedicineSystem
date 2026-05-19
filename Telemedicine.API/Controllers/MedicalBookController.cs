using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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

            var courses = await _context.TreatmentCourses
                .Include(t => t.Consultant)
                .Include(t => t.Entries)
                    .ThenInclude(e => e.EntryMedications)
                        .ThenInclude(em => em.Medication)
                .Include(t => t.Entries)
                    .ThenInclude(e => e.EntryProcedures)
                        .ThenInclude(ep => ep.Procedure)
                .Include(t => t.Entries)
                    .ThenInclude(e => e.EntryAnalyses)
                        .ThenInclude(ea => ea.Analysis)
                .Include(t => t.Entries)
                    .ThenInclude(e => e.Disease)
                .Where(t => t.PatientId == patient.PatientId)
                .OrderByDescending(t => t.StartDate)
                .ToListAsync();

            var result = courses.Select(c => new
            {
                c.TreatmentCourseId,
                c.HistoryCode,
                c.Status,
                c.StartDate,
                c.EndDate,
                c.CauseOfEnd,
                ConsultantFullName = c.Consultant.Surname + " " + c.Consultant.Name + " " + c.Consultant.MiddleName,
                c.Consultant.Specialty,
                Entries = c.Entries.OrderByDescending(e => e.CreatedAt).Select(e => new
                {
                    e.EntryId,
                    e.Conclusion,
                    e.Recommendations,
                    e.CreatedAt,
                    Disease = e.Disease != null ? new { e.Disease.MkbCode, e.Disease.Name } : null,
                    Medications = e.EntryMedications.Select(em => new
                    {
                        em.Medication.MedicationId,
                        em.Medication.Name,
                        em.Medication.Dosage,
                        em.Medication.Frequency
                    }),
                    Procedures = e.EntryProcedures.Select(ep => new
                    {
                        ep.Procedure.ProcedureId,
                        ep.Procedure.Name
                    }),
                    Analyses = e.EntryAnalyses.Select(ea => new
                    {
                        ea.Analysis.AnalysisId,
                        ea.Analysis.Name
                    })
                })
            });

            return Ok(new { courses = result });
        }

        // 2. Добавить запись в медкарту (консультант)
        [HttpPost("entry")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> AddEntry([FromBody] AddEntryDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultant = await _context.Consultants
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consultant == null)
                return NotFound("Консультант не найден");

            // Найти или создать медкарту
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

            // Найти активный курс или создать новый
            TreatmentCourse course;
            if (dto.TreatmentCourseId.HasValue)
            {
                course = await _context.TreatmentCourses
                    .FirstOrDefaultAsync(t => t.TreatmentCourseId == dto.TreatmentCourseId.Value);
            }
            else
            {
                course = await _context.TreatmentCourses
                    .Where(t => t.PatientId == dto.PatientId
                                && t.ConsultantId == consultant.ConsultantId
                                && t.Status == "active")
                    .FirstOrDefaultAsync();

                if (course == null)
                {
                    course = new TreatmentCourse
                    {
                        TreatmentCourseId = Guid.NewGuid(),
                        PatientId = dto.PatientId,
                        ConsultantId = consultant.ConsultantId,
                        StartDate = DateTime.UtcNow,
                        Status = "active"
                    };
                    _context.TreatmentCourses.Add(course);
                    await _context.SaveChangesAsync();
                }
            }

            // Создать запись, скопировав данные из последней записи курса (если есть)
            var lastEntry = await _context.Entries
                .Include(e => e.EntryMedications)
                .Include(e => e.EntryProcedures)
                .Include(e => e.EntryAnalyses)
                .Where(e => e.TreatmentCourseId == course.TreatmentCourseId)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            var entry = new Entry
            {
                EntryId = Guid.NewGuid(),
                MedicalBookId = book.MedicalBookId,
                TreatmentCourseId = course.TreatmentCourseId,
                ConsultantId = consultant.ConsultantId,
                DiseaseId = dto.DiseaseId,
                Conclusion = dto.Conclusion ?? lastEntry?.Conclusion,
                Recommendations = dto.Recommendations ?? lastEntry?.Recommendations,
                CreatedAt = DateTime.UtcNow
            };
            // Привязываем запись к консультации, если указан ApplicationId
            if (dto.ApplicationId.HasValue)
            {
                entry.ConsultationId = await _context.Consultations
                    .Where(c => c.ApplicationId == dto.ApplicationId.Value)
                    .Select(c => (Guid?)c.ConsultationId)
                    .FirstOrDefaultAsync();
            }
            _context.Entries.Add(entry);
            await _context.SaveChangesAsync();

            // Обработка медикаментов
            if (dto.Medications != null && dto.Medications.Any())
            {
                foreach (var med in dto.Medications)
                {
                    // Если дозировка или частота новые — обновляем справочник
                    var medication = await _context.Medications.FindAsync(med.MedicationId);
                    if (medication != null && !string.IsNullOrEmpty(med.Dosage))
                        medication.Dosage = med.Dosage;
                    if (medication != null && !string.IsNullOrEmpty(med.Frequency))
                        medication.Frequency = med.Frequency;

                    _context.EntryMedications.Add(new EntryMedication
                    {
                        EntryMedicationId = Guid.NewGuid(),
                        EntryId = entry.EntryId,
                        MedicationId = med.MedicationId
                    });
                }
            }
            else if (lastEntry != null)
            {
                foreach (var em in lastEntry.EntryMedications)
                {
                    _context.EntryMedications.Add(new EntryMedication
                    {
                        EntryMedicationId = Guid.NewGuid(),
                        EntryId = entry.EntryId,
                        MedicationId = em.MedicationId
                    });
                }
            }

            // Копируем процедуры
            if (dto.ProcedureIds != null && dto.ProcedureIds.Any())
            {
                foreach (var procId in dto.ProcedureIds)
                {
                    _context.EntryProcedures.Add(new EntryProcedure
                    {
                        EntryProcedureId = Guid.NewGuid(),
                        EntryId = entry.EntryId,
                        ProcedureId = procId
                    });
                }
            }
            else if (lastEntry != null)
            {
                foreach (var ep in lastEntry.EntryProcedures)
                {
                    _context.EntryProcedures.Add(new EntryProcedure
                    {
                        EntryProcedureId = Guid.NewGuid(),
                        EntryId = entry.EntryId,
                        ProcedureId = ep.ProcedureId
                    });
                }
            }

            // Копируем анализы
            if (dto.AnalysisIds != null && dto.AnalysisIds.Any())
            {
                foreach (var anId in dto.AnalysisIds)
                {
                    _context.EntryAnalyses.Add(new EntryAnalysis
                    {
                        EntryAnalysisId = Guid.NewGuid(),
                        EntryId = entry.EntryId,
                        AnalysisId = anId
                    });
                }
            }
            else if (lastEntry != null)
            {
                foreach (var ea in lastEntry.EntryAnalyses)
                {
                    _context.EntryAnalyses.Add(new EntryAnalysis
                    {
                        EntryAnalysisId = Guid.NewGuid(),
                        EntryId = entry.EntryId,
                        AnalysisId = ea.AnalysisId
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Запись добавлена",
                entryId = entry.EntryId,
                treatmentCourseId = course.TreatmentCourseId
            });
        }

        // 3. Завершить курс лечения
        [HttpPost("complete-course/{courseId}")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> CompleteCourse(Guid courseId, [FromBody] CompleteCourseDto dto)
        {
            var course = await _context.TreatmentCourses
                .FirstOrDefaultAsync(t => t.TreatmentCourseId == courseId);

            if (course == null)
                return NotFound("Курс не найден");

            if (course.Status == "completed")
                return BadRequest("Курс уже завершён");

            course.Status = "completed";
            course.EndDate = DateTime.UtcNow;
            course.CauseOfEnd = dto.CauseOfEnd;

            // Обновляем последнюю запись — добавляем рекомендации и заключение
            var lastEntry = await _context.Entries
                .Where(e => e.TreatmentCourseId == courseId)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastEntry != null)
            {
                if (!string.IsNullOrEmpty(dto.FinalConclusion))
                    lastEntry.Conclusion = dto.FinalConclusion;
                if (!string.IsNullOrEmpty(dto.Recommendations))
                    lastEntry.Recommendations = dto.Recommendations;
                if (dto.DiseaseId.HasValue)
                    lastEntry.DiseaseId = dto.DiseaseId;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Курс лечения завершён" });
        }

        // 4. Получить медкарту пациента (консультант)
        [HttpGet("patient/{patientId}")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetPatientMedicalBook(Guid patientId)
        {
            var patient = await _context.Patients.FindAsync(patientId);

            var courses = await _context.TreatmentCourses
                .Include(t => t.Consultant)
                .Include(t => t.Entries)
                    .ThenInclude(e => e.EntryMedications)
                        .ThenInclude(em => em.Medication)
                .Include(t => t.Entries)
                    .ThenInclude(e => e.EntryProcedures)
                        .ThenInclude(ep => ep.Procedure)
                .Include(t => t.Entries)
                    .ThenInclude(e => e.EntryAnalyses)
                        .ThenInclude(ea => ea.Analysis)
                .Include(t => t.Entries)
                    .ThenInclude(e => e.Disease)
                .Where(t => t.PatientId == patientId)
                .OrderByDescending(t => t.StartDate)
                .ToListAsync();

            var result = new
            {
                PatientFullName = patient?.Surname + " " + patient?.Name + " " + patient?.MiddleName,
                Courses = courses.Select(c => new
                {
                    c.TreatmentCourseId,
                    c.HistoryCode,
                    c.Status,
                    c.StartDate,
                    c.EndDate,
                    c.CauseOfEnd,
                    ConsultantFullName = c.Consultant.Surname + " " + c.Consultant.Name + " " + c.Consultant.MiddleName,
                    Entries = c.Entries.OrderByDescending(e => e.CreatedAt).Select(e => new
                    {
                        e.EntryId,
                        e.Conclusion,
                        e.Recommendations,
                        e.CreatedAt,
                        Disease = e.Disease != null ? new { e.Disease.MkbCode, e.Disease.Name } : null,
                        Medications = e.EntryMedications.Select(em => new
                        {
                            em.Medication.MedicationId,
                            em.Medication.Name,
                            em.Medication.Dosage,
                            em.Medication.Frequency
                        }),
                        Procedures = e.EntryProcedures.Select(ep => new
                        {
                            ep.Procedure.ProcedureId,
                            ep.Procedure.Name
                        }),
                        Analyses = e.EntryAnalyses.Select(ea => new
                        {
                            ea.Analysis.AnalysisId,
                            ea.Analysis.Name
                        })
                    })
                })
            };

            return Ok(result);
        }

        // 5. Список пациентов консультанта
        [HttpGet("my-patients")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetMyPatients()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var consultant = await _context.Consultants
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consultant == null)
                return NotFound("Консультант не найден");

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

        // 6. Справочники для фронта
        [HttpGet("dictionaries")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetDictionaries()
        {
            var medications = await _context.Medications
                .Select(m => new { m.MedicationId, m.Name, m.Dosage, m.Frequency })
                .ToListAsync();

            var procedures = await _context.Procedures
                .Select(p => new { p.ProcedureId, p.Name })
                .ToListAsync();

            var analyses = await _context.Analyses
                .Select(a => new { a.AnalysisId, a.Name })
                .ToListAsync();

            return Ok(new { medications, procedures, analyses });
        }
    }

    // DTO
    public class AddEntryDto
    {
        public Guid PatientId { get; set; }
        public Guid? ApplicationId { get; set; }
        public Guid? TreatmentCourseId { get; set; }
        public Guid? DiseaseId { get; set; }
        public string? Conclusion { get; set; }
        public string? Recommendations { get; set; }
        public List<MedicationEntryDto>? Medications { get; set; }
        public List<Guid>? ProcedureIds { get; set; }
        public List<Guid>? AnalysisIds { get; set; }
    }

    public class MedicationEntryDto
    {
        public Guid MedicationId { get; set; }
        public string? Dosage { get; set; }
        public string? Frequency { get; set; }
    }

    public class CompleteCourseDto
    {
        public string CauseOfEnd { get; set; }
        public string? FinalConclusion { get; set; }
        public string? Recommendations { get; set; }
        public Guid? DiseaseId { get; set; }
    }
}