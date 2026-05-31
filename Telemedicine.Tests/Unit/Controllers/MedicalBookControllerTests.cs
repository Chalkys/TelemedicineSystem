using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Controllers;
using TelemedicineSystem.Core.Entities;

namespace Telemedicine.Tests.Unit.Controllers
{
    public class MedicalBookControllerTests
    {
        private readonly AppDbContext _context;
        private readonly Guid _patientUserId = Guid.NewGuid();
        private readonly Guid _patientId = Guid.NewGuid();
        private readonly Guid _consultantUserId = Guid.NewGuid();
        private readonly Guid _consultantId = Guid.NewGuid();
        private readonly Guid _appId = Guid.NewGuid();

        public MedicalBookControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        private MedicalBookController CreateController(Guid userId, string role)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }));
            return new MedicalBookController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };
        }

        private async Task SeedBase()
        {
            _context.Users.Add(new User { UserId = _patientUserId, Email = "p@t.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = _patientId, UserId = _patientUserId, Name = "Иван", Surname = "Иванов", MiddleName = "Иванович", DateOfBirth = DateTime.UtcNow, Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });
            _context.Users.Add(new User { UserId = _consultantUserId, Email = "c@t.ru", PasswordHash = "h", Role = "Consultant" });
            _context.Consultants.Add(new Consultant { ConsultantId = _consultantId, UserId = _consultantUserId, Name = "Пётр", Surname = "Петров", Specialty = "Терапевт" });
            _context.Applications.Add(new Application { ApplicationId = _appId, PatientId = _patientId, ConsultantId = _consultantId, Type = 'F', Subject = "Тест", Status = "accepted", CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetMyMedicalBook_PatientExists_ReturnsOk()
        {
            await SeedBase();
            var controller = CreateController(_patientUserId, "Patient");

            var result = await controller.GetMyMedicalBook();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task AddEntry_FirstEntry_CreatesBookAndCourse()
        {
            await SeedBase();
            var controller = CreateController(_consultantUserId, "Consultant");
            var dto = new AddEntryDto
            {
                PatientId = _patientId,
                ApplicationId = _appId,
                Complaints = "Головная боль",
                Conclusion = "Мигрень"
            };

            var result = await controller.AddEntry(dto);

            result.Should().BeOfType<OkObjectResult>();
            _context.MedicalBooks.Count().Should().Be(1);
            _context.TreatmentCourses.Count().Should().Be(1);
            _context.Entries.Count().Should().Be(1);
        }

        [Fact]
        public async Task AddEntry_SecondEntry_CopiesFromPrevious()
        {
            await SeedBase();
            var book = new MedicalBook { MedicalBookId = Guid.NewGuid(), PatientId = _patientId, CreationDate = DateTime.UtcNow, Description = "МК" };
            var course = new TreatmentCourse { TreatmentCourseId = Guid.NewGuid(), PatientId = _patientId, ConsultantId = _consultantId, StartDate = DateTime.UtcNow, Status = "active" };
            var entry = new Entry { EntryId = Guid.NewGuid(), MedicalBookId = book.MedicalBookId, TreatmentCourseId = course.TreatmentCourseId, ConsultantId = _consultantId, Complaints = "Боль", PreviousDiagnoses = "Грипп", CurrentMedications = "Аспирин", Conclusion = "ОРВИ", CreatedAt = DateTime.UtcNow };
            _context.MedicalBooks.Add(book);
            _context.TreatmentCourses.Add(course);
            _context.Entries.Add(entry);
            await _context.SaveChangesAsync();

            var controller = CreateController(_consultantUserId, "Consultant");
            var dto = new AddEntryDto { PatientId = _patientId, TreatmentCourseId = course.TreatmentCourseId };

            var result = await controller.AddEntry(dto);

            var newEntry = _context.Entries.OrderByDescending(e => e.CreatedAt).First();
            newEntry.Complaints.Should().Be("Боль");
            newEntry.PreviousDiagnoses.Should().Be("Грипп");
            newEntry.CurrentMedications.Should().Be("Аспирин");
        }

        [Fact]
        public async Task CompleteCourse_Valid_ChangesStatus()
        {
            await SeedBase();
            var course = new TreatmentCourse { TreatmentCourseId = Guid.NewGuid(), PatientId = _patientId, ConsultantId = _consultantId, StartDate = DateTime.UtcNow, Status = "active" };
            _context.TreatmentCourses.Add(course);
            await _context.SaveChangesAsync();

            var controller = CreateController(_consultantUserId, "Consultant");
            var dto = new CompleteCourseDto { CauseOfEnd = "Выздоровление" };

            var result = await controller.CompleteCourse(course.TreatmentCourseId, dto);

            result.Should().BeOfType<OkObjectResult>();
            var updated = await _context.TreatmentCourses.FindAsync(course.TreatmentCourseId);
            updated!.Status.Should().Be("completed");
            updated.CauseOfEnd.Should().Be("Выздоровление");
        }

        [Fact]
        public async Task GetMyPatients_ReturnsList()
        {
            await SeedBase();
            var controller = CreateController(_consultantUserId, "Consultant");

            var result = await controller.GetMyPatients();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task UpdatePatientInfo_Valid_UpdatesFields()
        {
            await SeedBase();
            var controller = CreateController(_patientUserId, "Patient");
            var dto = new UpdatePatientInfoDto { BloodType = "II", RhFactor = "+" };

            var result = await controller.UpdatePatientInfo(dto);

            result.Should().BeOfType<OkObjectResult>();
            var patient = await _context.Patients.FindAsync(_patientId);
            patient!.BloodType.Should().Be("II");
            patient.RhFactor.Should().Be("+");
        }

        [Fact]
        public async Task GetDictionaries_ReturnsAll()
        {
            await SeedBase();
            var controller = CreateController(_consultantUserId, "Consultant");

            var result = await controller.GetDictionaries();

            result.Should().BeOfType<OkObjectResult>();
        }
    }
}