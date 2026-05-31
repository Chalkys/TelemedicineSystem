using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Security.Claims;
using Telemedicine.API.Controllers;
using Telemedicine.API.Services;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Controllers;
using TelemedicineSystem.Core.Entities;

namespace Telemedicine.Tests.Integration
{
    public class ConsultantFlowTests
    {
        private readonly AppDbContext _context;
        private Guid _patientUserId, _patientId;
        private Guid _consultantUserId, _consultantId;
        private Guid _appId, _consultationId;

        public ConsultantFlowTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        private ApplicationController CreateAppController(Guid userId, string role)
        {
            var c = new ApplicationController(_context);
            c.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = CreatePrincipal(userId, role) } };
            return c;
        }

        private MedicalBookController CreateMedController(Guid userId, string role)
        {
            var c = new MedicalBookController(_context);
            c.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = CreatePrincipal(userId, role) } };
            return c;
        }

        private ConsultationController CreateConsController(Guid userId, string role)
        {
            var c = new ConsultationController(_context);
            c.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = CreatePrincipal(userId, role) } };
            return c;
        }

        private ClaimsPrincipal CreatePrincipal(Guid userId, string role) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }));

        private async Task SeedData()
        {
            // Пациент
            _patientUserId = Guid.NewGuid();
            _patientId = Guid.NewGuid();
            _context.Users.Add(new User { UserId = _patientUserId, Email = "pat@t.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = _patientId, UserId = _patientUserId, Name = "Иван", Surname = "Иванов", MiddleName = "", DateOfBirth = DateTime.UtcNow, Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });

            // Консультант
            _consultantUserId = Guid.NewGuid();
            _consultantId = Guid.NewGuid();
            _context.Users.Add(new User { UserId = _consultantUserId, Email = "doc@t.ru", PasswordHash = "h", Role = "Consultant" });
            _context.Consultants.Add(new Consultant { ConsultantId = _consultantId, UserId = _consultantUserId, Name = "Пётр", Surname = "Петров", MiddleName = "", Specialty = "Терапевт" });

            // Заявка
            _appId = Guid.NewGuid();
            _context.Applications.Add(new Application { ApplicationId = _appId, PatientId = _patientId, ConsultantId = _consultantId, Type = 'F', Subject = "Кашель", Status = "pending", CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task ViewNewApplications_ReturnsList()
        {
            await SeedData();
            var controller = CreateAppController(_consultantUserId, "Consultant");

            var result = await controller.GetAllApplications();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task AcceptApplication_And_StartConsultation()
        {
            await SeedData();
            var appController = CreateAppController(_consultantUserId, "Consultant");

            // Принять заявку
            var acceptResult = await appController.AcceptApplication(_appId);
            acceptResult.Should().BeOfType<OkObjectResult>();

            // Найти созданную консультацию
            var consultation = await _context.Consultations.FirstAsync();
            _consultationId = consultation.ConsultationId;

            // Начать консультацию
            var consController = CreateConsController(_consultantUserId, "Consultant");
            var startResult = await consController.StartConsultation(_consultationId);
            startResult.Should().BeOfType<OkObjectResult>();

            var updated = await _context.Consultations.FindAsync(_consultationId);
            updated!.Status.Should().Be("in_progress");
        }

        [Fact]
        public async Task AddEntry_ToMedicalBook()
        {
            await AcceptApplication_And_StartConsultation();
            var controller = CreateMedController(_consultantUserId, "Consultant");

            var dto = new TelemedicineSystem.API.Controllers.AddEntryDto
            {
                PatientId = _patientId,
                ApplicationId = _appId,
                Complaints = "Кашель сухой",
                Conclusion = "ОРВИ",
                Recommendations = "Постельный режим"
            };
            var result = await controller.AddEntry(dto);

            result.Should().BeOfType<OkObjectResult>();
            _context.Entries.Count().Should().Be(1);
            _context.MedicalBooks.Count().Should().Be(1);
            _context.TreatmentCourses.Count().Should().Be(1);
        }

        [Fact]
        public async Task ViewPatientMedicalBook_ReturnsData()
        {
            await AddEntry_ToMedicalBook();
            var controller = CreateMedController(_consultantUserId, "Consultant");

            var result = await controller.GetPatientMedicalBook(_patientId);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CompleteCourse_MarksCompleted()
        {
            await AddEntry_ToMedicalBook();
            var controller = CreateMedController(_consultantUserId, "Consultant");
            var course = await _context.TreatmentCourses.FirstAsync();

            var dto = new TelemedicineSystem.API.Controllers.CompleteCourseDto { CauseOfEnd = "Выздоровление" };
            var result = await controller.CompleteCourse(course.TreatmentCourseId, dto);

            result.Should().BeOfType<OkObjectResult>();
            var updated = await _context.TreatmentCourses.FindAsync(course.TreatmentCourseId);
            updated!.Status.Should().Be("completed");
        }
    }
}