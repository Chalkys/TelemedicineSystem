using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Telemedicine.API.Controllers;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Controllers;
using TelemedicineSystem.Core.DTOs;
using TelemedicineSystem.Core.Entities;

namespace Telemedicine.Tests.Integration
{
    public class PatientFlowTests
    {
        private readonly AppDbContext _context;
        private readonly AuthController _authController;
        private readonly ApplicationController _appController;
        private readonly MedicalBookController _medicalBookController;
        private readonly ConsultantController _consultantController;
        private readonly UsersController _usersController;
        private Guid _patientUserId;
        private Guid _patientId;

        public PatientFlowTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            // Контроллеры без авторизации (для регистрации/логина)
            var configMock = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Jwt:Key", "ThisIsASuperSecretKeyForTesting123!" },
                    { "Jwt:Issuer", "TestIssuer" },
                    { "Jwt:Audience", "TestAudience" },
                    { "Jwt:ExpireMinutes", "60" }
                })
                .Build();
            var emailMock = new Moq.Mock<Telemedicine.API.Services.IEmailService>();
            _authController = new AuthController(_context, configMock, emailMock.Object);

            _appController = new ApplicationController(_context);
            _medicalBookController = new MedicalBookController(_context);
            _consultantController = new ConsultantController(_context);
            _usersController = new UsersController(_context);
        }

        private void AuthenticateAsPatient()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _patientUserId.ToString()),
                new Claim(ClaimTypes.Role, "Patient")
            }));
            _appController.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user } };
            _medicalBookController.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user } };
            _usersController.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user } };
        }

        private async Task SeedConsultant()
        {
            var cUserId = Guid.NewGuid();
            var cId = Guid.NewGuid();
            _context.Users.Add(new User { UserId = cUserId, Email = "doctor@mail.ru", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Doc123!"), Role = "Consultant" });
            _context.Consultants.Add(new Consultant { ConsultantId = cId, UserId = cUserId, Name = "Пётр", Surname = "Петров", MiddleName = "", Specialty = "Терапевт" });
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task RegisterAndLogin_ValidData_ReturnsToken()
        {
            // Act: Регистрация
            var regDto = new Telemedicine.API.Controllers.RegisterPatientDto
            {
                Email = "patient@mail.ru",
                Password = "Pass123!",
                Name = "Иван",
                Surname = "Иванов",
                MiddleName = "Иванович",
                DateOfBirth = new DateTime(1990, 5, 15),
                Gender = "Male",
                ContactInfo = "+79991234567",
                InsurancePolicyNumber = "1234567890123456"
            };
            var regResult = await _authController.RegisterPatient(regDto);
            regResult.Should().BeOfType<OkObjectResult>();

            // Act: Логин
            var loginDto = new Telemedicine.API.Controllers.LoginDto { Email = "patient@mail.ru", Password = "Pass123!" };
            var loginResult = await _authController.Login(loginDto);
            var okResult = loginResult.Should().BeOfType<OkObjectResult>().Subject;
            var value = okResult.Value!;
            ((string)value.GetType().GetProperty("role")!.GetValue(value)!).Should().Be("Patient");

            // Сохраняем ID для следующих тестов
            _patientUserId = (Guid)value.GetType().GetProperty("userId")!.GetValue(value)!;
            _patientId = (await _context.Patients.FirstAsync(p => p.UserId == _patientUserId)).PatientId;
        }

        [Fact]
        public async Task CreateApplication_CreatesConsultation()
        {
            // Arrange
            await RegisterAndLogin_ValidData_ReturnsToken();
            await SeedConsultant();
            var consultant = await _context.Consultants.FirstAsync();
            AuthenticateAsPatient();

            var dto = new CreateApplicationDto
            {
                Type = 'P',
                Subject = "Головная боль",
                ConsultantId = consultant.ConsultantId,
                ConsultationDate = DateTime.UtcNow.AddDays(1),
                Complaints = "Болит голова второй день",
                PreviousDiagnoses = "Мигрень",
                CurrentMedications = "Анальгин"
            };

            // Act
            var result = await _appController.CreateApplication(dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _context.Applications.Count().Should().Be(1);
            _context.Consultations.Count().Should().Be(1);
        }

        [Fact]
        public async Task ViewMyConsultations_ReturnsList()
        {
            await CreateApplication_CreatesConsultation();
            AuthenticateAsPatient();
            var controller = new ConsultationController(_context)
            {
                ControllerContext = _appController.ControllerContext
            };

            var result = await controller.GetMyConsultations();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ViewMedicalBook_ReturnsOk()
        {
            await RegisterAndLogin_ValidData_ReturnsToken();
            AuthenticateAsPatient();

            var result = await _medicalBookController.GetMyMedicalBook();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ViewConsultants_ReturnsList()
        {
            await RegisterAndLogin_ValidData_ReturnsToken();
            await SeedConsultant();

            var result = await _consultantController.GetConsultants(null);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task UpdateProfile_UpdatesFields()
        {
            await RegisterAndLogin_ValidData_ReturnsToken();
            AuthenticateAsPatient();

            var dto = new TelemedicineSystem.API.Controllers.UpdatePatientInfoDto
            {
                BloodType = "I",
                RhFactor = "+",
                AllergicReactions = "Пыльца"
            };
            var result = await _medicalBookController.UpdatePatientInfo(dto);

            result.Should().BeOfType<OkObjectResult>();
            var patient = await _context.Patients.FirstAsync(p => p.UserId == _patientUserId);
            patient.BloodType.Should().Be("I");
            patient.AllergicReactions.Should().Be("Пыльца");
        }
    }
}