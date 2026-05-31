using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Controllers;
using TelemedicineSystem.Core.DTOs;
using TelemedicineSystem.Core.Entities;

namespace Telemedicine.Tests.Unit.Controllers
{
    public class ApplicationControllerTests
    {
        private readonly AppDbContext _context;
        private readonly Guid _patientUserId = Guid.NewGuid();
        private readonly Guid _patientId = Guid.NewGuid();
        private readonly Guid _consultantUserId = Guid.NewGuid();
        private readonly Guid _consultantId = Guid.NewGuid();

        public ApplicationControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        private ApplicationController CreateController(Guid userId, string role)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }));

            return new ApplicationController(_context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };
        }

        private async Task SeedPatient()
        {
            _context.Users.Add(new User { UserId = _patientUserId, Email = "pat@mail.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient
            {
                PatientId = _patientId,
                UserId = _patientUserId,
                Name = "Иван",
                Surname = "Иванов",
                MiddleName = "Иванович",
                DateOfBirth = DateTime.UtcNow,
                Gender = "Male",
                ContactInfo = "+79990000000",
                InsurancePolicyNumber = "1234567890123456"
            });
            await _context.SaveChangesAsync();
        }

        private async Task SeedConsultant()
        {
            _context.Users.Add(new User { UserId = _consultantUserId, Email = "cons@mail.ru", PasswordHash = "h", Role = "Consultant" });
            _context.Consultants.Add(new Consultant { ConsultantId = _consultantId, UserId = _consultantUserId, Name = "Пётр", Surname = "Петров", MiddleName = "Петрович", Specialty = "Терапевт" });
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task CreateApplication_ValidData_CreatesApplicationAndConsultation()
        {
            await SeedPatient();
            await SeedConsultant();
            var dto = new CreateApplicationDto
            {
                Type = 'P',
                Subject = "Головная боль",
                ConsultantId = _consultantId,
                ConsultationDate = DateTime.UtcNow.AddDays(1),
                Complaints = "Болит голова",
                PreviousDiagnoses = "Мигрень",
                CurrentMedications = "Анальгин"
            };
            var controller = CreateController(_patientUserId, "Patient");

            var result = await controller.CreateApplication(dto);

            result.Should().BeOfType<OkObjectResult>();
            _context.Applications.Count().Should().Be(1);
            _context.Consultations.Count().Should().Be(1);
        }

        [Fact]
        public async Task GetMyApplications_ReturnsPatientApplications()
        {
            await SeedPatient();
            await SeedConsultant();
            var app = new Application
            {
                ApplicationId = Guid.NewGuid(),
                PatientId = _patientId,
                Type = 'F',
                Subject = "Тест",
                Status = "accepted",
                CreatedAt = DateTime.UtcNow
            };
            _context.Applications.Add(app);
            await _context.SaveChangesAsync();
            var controller = CreateController(_patientUserId, "Patient");

            var result = await controller.GetMyApplications();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var list = okResult.Value as System.Collections.IList;
            list.Should().NotBeNull();
            list!.Count.Should().Be(1);
        }

        [Fact]
        public async Task GetApplication_ById_ReturnsApplication()
        {
            await SeedPatient();
            var appId = Guid.NewGuid();
            _context.Applications.Add(new Application
            {
                ApplicationId = appId,
                PatientId = _patientId,
                Type = 'P',
                Subject = "Найти",
                Status = "accepted",
                CreatedAt = DateTime.UtcNow,
                Complaints = "Жалоба"
            });
            await _context.SaveChangesAsync();
            var controller = CreateController(_patientUserId, "Patient");

            var result = await controller.GetApplication(appId);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task GetApplication_NotFound_Returns404()
        {
            var controller = CreateController(_patientUserId, "Patient");

            var result = await controller.GetApplication(Guid.NewGuid());

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task AcceptApplication_Valid_ChangesStatusAndCreatesConsultation()
        {
            await SeedPatient();
            await SeedConsultant();
            var appId = Guid.NewGuid();
            _context.Applications.Add(new Application
            {
                ApplicationId = appId,
                PatientId = _patientId,
                ConsultantId = _consultantId,
                Type = 'F',
                Subject = "Принять",
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            var controller = CreateController(_consultantUserId, "Consultant");

            var result = await controller.AcceptApplication(appId);

            result.Should().BeOfType<OkObjectResult>();
            var app = await _context.Applications.FindAsync(appId);
            app!.Status.Should().Be("accepted");
            _context.Consultations.Count().Should().Be(1);
        }

        [Fact]
        public async Task RejectApplication_Valid_ChangesStatusToRejected()
        {
            await SeedPatient();
            var appId = Guid.NewGuid();
            _context.Applications.Add(new Application
            {
                ApplicationId = appId,
                PatientId = _patientId,
                Type = 'F',
                Subject = "Отклонить",
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            var controller = CreateController(_consultantUserId, "Consultant");

            var result = await controller.RejectApplication(appId);

            result.Should().BeOfType<OkObjectResult>();
            var app = await _context.Applications.FindAsync(appId);
            app!.Status.Should().Be("rejected");
        }
    }
}