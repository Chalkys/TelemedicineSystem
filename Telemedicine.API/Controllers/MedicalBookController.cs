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
                // Жалобы и предв. диагноз из первой записи курса
                Complaints = c.Entries.OrderBy(e => e.CreatedAt).FirstOrDefault() != null
                    ? c.Entries.OrderBy(e => e.CreatedAt).FirstOrDefault().Complaints
                    : null,
                PreliminaryDiagnosis = c.Entries.OrderBy(e => e.CreatedAt).FirstOrDefault() != null
                    ? c.Entries.OrderBy(e => e.CreatedAt).FirstOrDefault().Conclusion
                    : null,
                Entries = c.Entries.OrderByDescending(e => e.CreatedAt).Select(e => new
                {
                    e.EntryId,
                    e.Complaints,
                    e.PreviousDiagnoses,
                    e.CurrentMedications,
                    e.Conclusion,
                    e.Recommendations,
                    e.CreatedAt,
                    e.ConsultationId,
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
                    }),
                    Referrals = _context.AnalysisReferrals
                        .Where(r => r.EntryId == e.EntryId)
                        .Select(r => new
                        {
                            r.ReferralId,
                            r.OrganizationName,
                            r.ReferralPurpose,
                            r.Tests,
                            r.ServiceCode
                        })
                        .ToList(),
                    // Документы, привязанные к консультации этой записи
                    Documents = _context.Documents
                        .Where(d => d.ConsultationId == e.ConsultationId)
                        .Select(d => new { d.DocumentId, d.FileName, d.UploadDate })
                        .ToList()
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

            // === ДИАГНОСТИКА ===
            var allEntriesInCourse = await _context.Entries
                .Where(e => e.TreatmentCourseId == course.TreatmentCourseId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            Console.WriteLine($"=== КУРС: {course.TreatmentCourseId} ===");
            Console.WriteLine($"Всего записей в курсе: {allEntriesInCourse.Count}");
            foreach (var e in allEntriesInCourse)
            {
                Console.WriteLine($"  Запись {e.EntryId}: Complaints='{e.Complaints}', PrevDiagnoses='{e.PreviousDiagnoses}', CurrentMeds='{e.CurrentMedications}', Conclusion='{e.Conclusion}'");
            }

            var lastEntry = allEntriesInCourse.FirstOrDefault();
            Console.WriteLine($"lastEntry == null: {lastEntry == null}");
            if (lastEntry != null)
            {
                Console.WriteLine($"lastEntry.Complaints: '{lastEntry.Complaints}'");
                Console.WriteLine($"lastEntry.PreviousDiagnoses: '{lastEntry.PreviousDiagnoses}'");
                Console.WriteLine($"lastEntry.CurrentMedications: '{lastEntry.CurrentMedications}'");
                Console.WriteLine($"lastEntry.Conclusion: '{lastEntry.Conclusion}'");
            }

            // Данные из DTO
            Console.WriteLine($"=== DTO ===");
            Console.WriteLine($"dto.Complaints: '{dto.Complaints}'");
            Console.WriteLine($"dto.PreviousDiagnoses: '{dto.PreviousDiagnoses}'");
            Console.WriteLine($"dto.CurrentMedications: '{dto.CurrentMedications}'");
            Console.WriteLine($"dto.Conclusion: '{dto.Conclusion}'");
            Console.WriteLine($"dto.ApplicationId: {dto.ApplicationId}");

            var entry = new Entry
            {
                EntryId = Guid.NewGuid(),
                MedicalBookId = book.MedicalBookId,
                TreatmentCourseId = course.TreatmentCourseId,
                ConsultantId = consultant.ConsultantId,
                DiseaseId = dto.DiseaseId,
                CreatedAt = DateTime.UtcNow
            };

            // Сохраняем направления на анализы
            if (dto.Referrals != null && dto.Referrals.Any())
            {
                foreach (var refDto in dto.Referrals)
                {
                    _context.AnalysisReferrals.Add(new AnalysisReferral
                    {
                        ReferralId = Guid.NewGuid(),
                        EntryId = entry.EntryId,
                        OrganizationName = refDto.OrganizationName,
                        ReferralPurpose = refDto.ReferralPurpose,
                        Tests = refDto.Tests,
                        ServiceCode = refDto.ServiceCode,
                        ReferralDate = DateTime.UtcNow
                    });
                }
            }

            // Автозаполнение
            if (lastEntry == null)
            {
                Console.WriteLine(">>> ПЕРВАЯ запись в курсе");
                if (dto.ApplicationId.HasValue)
                {
                    var application = await _context.Applications
                        .FirstOrDefaultAsync(a => a.ApplicationId == dto.ApplicationId.Value);

                    if (application != null)
                    {
                        Console.WriteLine($"application.Complaints: '{application.Complaints}'");
                        entry.Complaints = !string.IsNullOrWhiteSpace(dto.Complaints) ? dto.Complaints : application.Complaints;
                        entry.PreviousDiagnoses = !string.IsNullOrWhiteSpace(dto.PreviousDiagnoses) ? dto.PreviousDiagnoses : application.PreviousDiagnoses;
                        entry.CurrentMedications = !string.IsNullOrWhiteSpace(dto.CurrentMedications) ? dto.CurrentMedications : application.CurrentMedications;
                    }
                    else
                    {
                        Console.WriteLine("application == null!");
                        entry.Complaints = dto.Complaints;
                        entry.PreviousDiagnoses = dto.PreviousDiagnoses;
                        entry.CurrentMedications = dto.CurrentMedications;
                    }
                }
                else
                {
                    Console.WriteLine("dto.ApplicationId == null, не из чего автозаполнять");
                    entry.Complaints = dto.Complaints;
                    entry.PreviousDiagnoses = dto.PreviousDiagnoses;
                    entry.CurrentMedications = dto.CurrentMedications;
                }
                entry.Conclusion = dto.Conclusion;
                entry.Recommendations = dto.Recommendations;
            }
            else
            {
                Console.WriteLine(">>> НЕ ПЕРВАЯ запись — копируем из предыдущей");
                entry.Complaints = !string.IsNullOrWhiteSpace(dto.Complaints) ? dto.Complaints : lastEntry.Complaints;
                entry.PreviousDiagnoses = !string.IsNullOrWhiteSpace(dto.PreviousDiagnoses) ? dto.PreviousDiagnoses : lastEntry.PreviousDiagnoses;
                entry.CurrentMedications = !string.IsNullOrWhiteSpace(dto.CurrentMedications) ? dto.CurrentMedications : lastEntry.CurrentMedications;
                entry.Conclusion = !string.IsNullOrWhiteSpace(dto.Conclusion) ? dto.Conclusion : lastEntry.Conclusion;
                entry.Recommendations = !string.IsNullOrWhiteSpace(dto.Recommendations) ? dto.Recommendations : lastEntry.Recommendations;
            }

            Console.WriteLine($"=== РЕЗУЛЬТАТ entry ===");
            Console.WriteLine($"entry.Complaints: '{entry.Complaints}'");
            Console.WriteLine($"entry.PreviousDiagnoses: '{entry.PreviousDiagnoses}'");
            Console.WriteLine($"entry.CurrentMedications: '{entry.CurrentMedications}'");
            Console.WriteLine($"entry.Conclusion: '{entry.Conclusion}'");

            // Привязываем запись к консультации
            if (dto.ApplicationId.HasValue)
            {
                entry.ConsultationId = await _context.Consultations
                    .Where(c => c.ApplicationId == dto.ApplicationId.Value)
                    .Select(c => (Guid?)c.ConsultationId)
                    .FirstOrDefaultAsync();
            }

            _context.Entries.Add(entry);
            await _context.SaveChangesAsync();

            // Медикаменты
            if (dto.Medications != null && dto.Medications.Any())
            {
                foreach (var med in dto.Medications)
                {
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
                var lastMeds = await _context.EntryMedications
                    .Where(em => em.EntryId == lastEntry.EntryId)
                    .ToListAsync();
                Console.WriteLine($"Копируем медикаментов из предыдущей: {lastMeds.Count}");
                foreach (var em in lastMeds)
                {
                    _context.EntryMedications.Add(new EntryMedication
                    {
                        EntryMedicationId = Guid.NewGuid(),
                        EntryId = entry.EntryId,
                        MedicationId = em.MedicationId
                    });
                }
            }

            // Процедуры
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
                var lastProcs = await _context.EntryProcedures
                    .Where(ep => ep.EntryId == lastEntry.EntryId)
                    .ToListAsync();
                foreach (var ep in lastProcs)
                {
                    _context.EntryProcedures.Add(new EntryProcedure
                    {
                        EntryProcedureId = Guid.NewGuid(),
                        EntryId = entry.EntryId,
                        ProcedureId = ep.ProcedureId
                    });
                }
            }

            // Анализы
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
                var lastAnalyses = await _context.EntryAnalyses
                    .Where(ea => ea.EntryId == lastEntry.EntryId)
                    .ToListAsync();
                foreach (var ea in lastAnalyses)
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
                    // Жалобы и предв. диагноз из первой записи курса
                    Complaints = c.Entries.OrderBy(e => e.CreatedAt).FirstOrDefault() != null
                        ? c.Entries.OrderBy(e => e.CreatedAt).FirstOrDefault().Complaints
                        : null,
                    PreliminaryDiagnosis = c.Entries.OrderBy(e => e.CreatedAt).FirstOrDefault() != null
                        ? c.Entries.OrderBy(e => e.CreatedAt).FirstOrDefault().Conclusion
                        : null,
                    Entries = c.Entries.OrderByDescending(e => e.CreatedAt).Select(e => new
                    {
                        e.EntryId,
                        e.Complaints,
                        e.PreviousDiagnoses,
                        e.CurrentMedications,
                        e.Conclusion,
                        e.Recommendations,
                        e.CreatedAt,
                        e.ConsultationId,
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
                        }),
                        Referrals = _context.AnalysisReferrals
                            .Where(r => r.EntryId == e.EntryId)
                            .Select(r => new
                            {
                                r.ReferralId,
                                r.OrganizationName,
                                r.ReferralPurpose,
                                r.Tests,
                                r.ServiceCode
                            })
                            .ToList(),
                        // Документы, привязанные к консультации этой записи
                        Documents = _context.Documents
                            .Where(d => d.ConsultationId == e.ConsultationId)
                            .Select(d => new { d.DocumentId, d.FileName, d.UploadDate })
                            .ToList()
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

        // 7. Создать направление на анализ
        [HttpPost("referral")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> AddReferral([FromBody] AddReferralDto dto)
        {
            var referral = new AnalysisReferral
            {
                ReferralId = Guid.NewGuid(),
                EntryId = dto.EntryId,
                PatientName = dto.PatientName,
                PatientAge = dto.PatientAge,
                MedicalBookNumber = dto.MedicalBookNumber,
                OrganizationName = dto.OrganizationName,
                ReferralDate = DateTime.UtcNow,
                DoctorName = dto.DoctorName,
                MkbCode = dto.MkbCode,
                ReferralPurpose = dto.ReferralPurpose,
                Tests = dto.Tests,
                ServiceCode = dto.ServiceCode
            };

            _context.AnalysisReferrals.Add(referral);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Направление создано", referralId = referral.ReferralId });
        }

        // 8. Получить информацию о пациенте (для пациента)
        [HttpGet("patient-info/my")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetMyPatientInfo()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return NotFound("Пациент не найден");

            var medicalBook = await _context.MedicalBooks
                .FirstOrDefaultAsync(mb => mb.PatientId == patient.PatientId);

            var dto = new PatientInfoDto
            {
                PatientId = patient.PatientId,
                Surname = patient.Surname,
                Name = patient.Name,
                MiddleName = patient.MiddleName,
                FullName = $"{patient.Surname} {patient.Name} {patient.MiddleName}".Trim(),
                DateOfBirth = patient.DateOfBirth,
                Age = DateTime.Now.Year - patient.DateOfBirth.Year -
                      (DateTime.Now.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0),
                Gender = patient.Gender,
                ContactInfo = patient.ContactInfo,
                OmsPolicy = patient.InsurancePolicyNumber,
                MedicalBookCreatedAt = medicalBook?.CreationDate ?? DateTime.MinValue,
                MaritalStatus = patient.MaritalStatus,
                Education = patient.Education,
                Employment = patient.Employment,
                Disability = patient.Disability,
                Workplace = patient.Workplace,
                WorkplaceChanged = patient.WorkplaceChanged,
                BloodType = patient.BloodType,
                RhFactor = patient.RhFactor,
                AllergicReactions = patient.AllergicReactions
            };

            return Ok(dto);
        }

        // 9. Получить информацию о пациенте (для консультанта)
        [HttpGet("patient-info/{patientId}")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> GetPatientInfo(Guid patientId)
        {
            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PatientId == patientId);

            if (patient == null)
                return NotFound("Пациент не найден");

            var medicalBook = await _context.MedicalBooks
                .FirstOrDefaultAsync(mb => mb.PatientId == patientId);

            var dto = new PatientInfoDto
            {
                PatientId = patient.PatientId,
                Surname = patient.Surname,
                Name = patient.Name,
                MiddleName = patient.MiddleName,
                FullName = $"{patient.Surname} {patient.Name} {patient.MiddleName}".Trim(),
                DateOfBirth = patient.DateOfBirth,
                Age = DateTime.Now.Year - patient.DateOfBirth.Year -
                      (DateTime.Now.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0),
                Gender = patient.Gender,
                ContactInfo = patient.ContactInfo,
                OmsPolicy = patient.InsurancePolicyNumber,
                MedicalBookCreatedAt = medicalBook?.CreationDate ?? DateTime.MinValue,
                MaritalStatus = patient.MaritalStatus,
                Education = patient.Education,
                Employment = patient.Employment,
                Disability = patient.Disability,
                Workplace = patient.Workplace,
                WorkplaceChanged = patient.WorkplaceChanged,
                BloodType = patient.BloodType,
                RhFactor = patient.RhFactor,
                AllergicReactions = patient.AllergicReactions
            };

            return Ok(dto);
        }

        // 10. Обновление данных пациентом
        [HttpPut("patient-info")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> UpdatePatientInfo([FromBody] UpdatePatientInfoDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null)
                return NotFound("Пациент не найден");

            if (dto.ContactInfo != null) patient.ContactInfo = dto.ContactInfo;
            if (dto.MaritalStatus != null) patient.MaritalStatus = dto.MaritalStatus;
            if (dto.Education != null) patient.Education = dto.Education;
            if (dto.Employment != null) patient.Employment = dto.Employment;
            if (dto.Disability != null) patient.Disability = dto.Disability;
            if (dto.Workplace != null) patient.Workplace = dto.Workplace;
            if (dto.WorkplaceChanged != null) patient.WorkplaceChanged = dto.WorkplaceChanged;
            if (dto.BloodType != null) patient.BloodType = dto.BloodType;
            if (dto.RhFactor != null) patient.RhFactor = dto.RhFactor;
            if (dto.AllergicReactions != null) patient.AllergicReactions = dto.AllergicReactions;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Данные обновлены" });
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
        public List<ReferralEntryDto>? Referrals { get; set; }
        public string? Complaints { get; set; }
        public string? PreviousDiagnoses { get; set; }
        public string? CurrentMedications { get; set; }
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
    public class AddReferralDto
    {
        public Guid EntryId { get; set; }
        public string? PatientName { get; set; }
        public int? PatientAge { get; set; }
        public string? MedicalBookNumber { get; set; }
        public string? OrganizationName { get; set; }
        public string? DoctorName { get; set; }
        public string? MkbCode { get; set; }
        public string? ReferralPurpose { get; set; }
        public string? Tests { get; set; }
        public string? ServiceCode { get; set; }
    }
    public class ReferralEntryDto
    {
        public string? OrganizationName { get; set; }
        public string? ReferralPurpose { get; set; }
        public string? Tests { get; set; }
        public string? ServiceCode { get; set; }
    }

    public class PatientInfoDto
    {
        public Guid PatientId { get; set; }
        public string Surname { get; set; }
        public string Name { get; set; }
        public string? MiddleName { get; set; }
        public string FullName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public int Age { get; set; }
        public string Gender { get; set; }
        public string? ContactInfo { get; set; }
        public string? OmsPolicy { get; set; }
        public DateTime MedicalBookCreatedAt { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Education { get; set; }
        public string? Employment { get; set; }
        public string? Disability { get; set; }
        public string? Workplace { get; set; }
        public string? WorkplaceChanged { get; set; }
        public string? BloodType { get; set; }
        public string? RhFactor { get; set; }
        public string? AllergicReactions { get; set; }
    }

    public class UpdatePatientInfoDto
    {
        public string? ContactInfo { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Education { get; set; }
        public string? Employment { get; set; }
        public string? Disability { get; set; }
        public string? Workplace { get; set; }
        public string? WorkplaceChanged { get; set; }
        public string? BloodType { get; set; }
        public string? RhFactor { get; set; }
        public string? AllergicReactions { get; set; }
    }
}