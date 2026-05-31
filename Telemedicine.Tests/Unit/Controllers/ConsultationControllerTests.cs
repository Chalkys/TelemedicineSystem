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
    public class ConsultationControllerTests
    {
        private readonly AppDbContext _context;
        private readonly Guid _patientUserId = Guid.NewGuid();
        private readonly Guid _patientId = Guid.NewGuid();
        private readonly Guid _consultantUserId = Guid.NewGuid();
        private readonly Guid _consultantId = Guid.NewGuid();
        private readonly Guid _consultationId = Guid.NewGuid();

        public ConsultationControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        private ConsultationController CreateController(Guid userId, string role)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }));
            return new ConsultationController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };
        }

        private async Task SeedData()
        {
            _context.Users.Add(new User { UserId = _patientUserId, Email = "pat@t.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = _patientId, UserId = _patientUserId, Name = "Иван", Surname = "Иванов", MiddleName = "Иванович", DateOfBirth = new DateTime(1990, 1, 1), Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });
            _context.Users.Add(new User { UserId = _consultantUserId, Email = "cons@t.ru", PasswordHash = "h", Role = "Consultant" });
            _context.Consultants.Add(new Consultant { ConsultantId = _consultantId, UserId = _consultantUserId, Name = "Пётр", Surname = "Петров", Specialty = "Терапевт" });
            await _context.SaveChangesAsync();

            var app = new Application { ApplicationId = Guid.NewGuid(), PatientId = _patientId, Type = 'F', Subject = "Тест", Status = "accepted", CreatedAt = DateTime.UtcNow };
            _context.Applications.Add(app);
            await _context.SaveChangesAsync();

            _context.Consultations.Add(new Consultation { ConsultationId = _consultationId, ApplicationId = app.ApplicationId, PatientId = _patientId, ConsultantId = _consultantId, Date = DateTime.UtcNow, Status = "scheduled" });
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetMyConsultations_Patient_ReturnsList()
        {
            await SeedData();
            var controller = CreateController(_patientUserId, "Patient");

            var result = await controller.GetMyConsultations();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetMyConsultations_Consultant_ReturnsList()
        {
            await SeedData();
            var controller = CreateController(_consultantUserId, "Consultant");

            var result = await controller.GetMyConsultations();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task StartConsultation_Valid_ChangesStatus()
        {
            await SeedData();
            var controller = CreateController(_consultantUserId, "Consultant");

            var result = await controller.StartConsultation(_consultationId);

            result.Should().BeOfType<OkObjectResult>();
            var c = await _context.Consultations.FindAsync(_consultationId);
            c!.Status.Should().Be("in_progress");
        }

        [Fact]
        public async Task StartConsultation_NotFound_Returns404()
        {
            var controller = CreateController(_consultantUserId, "Consultant");

            var result = await controller.StartConsultation(Guid.NewGuid());

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetMessages_ReturnsEmptyList()
        {
            await SeedData();
            var controller = CreateController(_patientUserId, "Patient");

            var result = await controller.GetMessages(_consultationId);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CompleteConsultation_ChangesStatus()
        {
            await SeedData();
            var controller = CreateController(_consultantUserId, "Consultant");

            var result = await controller.CompleteConsultation(_consultationId);

            result.Should().BeOfType<OkObjectResult>();
            var c = await _context.Consultations.FindAsync(_consultationId);
            c!.Status.Should().Be("completed");
        }

        [Fact]
        public async Task GetDocuments_ReturnsEmptyList()
        {
            await SeedData();
            var controller = CreateController(_patientUserId, "Patient");

            var result = await controller.GetDocuments(_consultationId);

            result.Should().BeOfType<OkObjectResult>();
        }
    }
}