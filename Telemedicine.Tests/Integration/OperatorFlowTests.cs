using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Telemedicine.API.Controllers;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Controllers;

namespace Telemedicine.Tests.Integration
{
    public class OperatorFlowTests
    {
        private readonly AppDbContext _context;
        private readonly Guid _operatorUserId = Guid.NewGuid();

        public OperatorFlowTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            _context.Users.Add(new User { UserId = _operatorUserId, Email = "admin@mail.ru", PasswordHash = "h", Role = "Operator" });
            _context.Operators.Add(new Operator { OperatorId = Guid.NewGuid(), UserId = _operatorUserId, FullName = "Админ", Address = "", ContactInfo = "", ConnectionToSystem = "" });
        }

        private OperatorController CreateController()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _operatorUserId.ToString()),
                new Claim(ClaimTypes.Role, "Operator")
            }));
            return new OperatorController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user } }
            };
        }

        [Fact]
        public async Task RegisterConsultant_CreatesUserAndConsultant()
        {
            var controller = CreateController();
            var dto = new RegisterConsultantDto
            {
                Email = "newdoc@mail.ru",
                Password = "Pass123!",
                Name = "Ольга",
                Surname = "Сидорова",
                Specialty = "Кардиолог",
                Post = "Врач"
            };

            var result = await controller.RegisterConsultant(dto);

            result.Should().BeOfType<OkObjectResult>();
            _context.Users.Any(u => u.Email == "newdoc@mail.ru").Should().BeTrue();
            _context.Consultants.Any(c => c.Name == "Ольга").Should().BeTrue();
        }

        [Fact]
        public async Task ViewAllUsers_ReturnsAll()
        {
            var controller = CreateController();

            var result = await controller.GetAllUsers();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task DeleteUser_RemovesUser()
        {
            var victimId = Guid.NewGuid();
            _context.Users.Add(new User { UserId = victimId, Email = "victim@mail.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = Guid.NewGuid(), UserId = victimId, Name = "Жертва", Surname = "Тестов", MiddleName = "", DateOfBirth = DateTime.UtcNow, Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });
            await _context.SaveChangesAsync();
            var controller = CreateController();

            var result = await controller.DeleteUser(victimId);

            result.Should().BeOfType<OkObjectResult>();
            _context.Users.Any(u => u.UserId == victimId).Should().BeFalse();
        }

        [Fact]
        public async Task ViewUserDetails_ReturnsProfile()
        {
            var controller = CreateController();
            var patientUserId = Guid.NewGuid();
            _context.Users.Add(new User { UserId = patientUserId, Email = "view@mail.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = Guid.NewGuid(), UserId = patientUserId, Name = "Тест", Surname = "Тестов", MiddleName = "", DateOfBirth = DateTime.UtcNow, Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });
            await _context.SaveChangesAsync();

            var result = await controller.GetUserDetails(patientUserId);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task EditUser_UpdatePatientInfo()
        {
            // Используем MedicalBookController для редактирования данных пациента
            var patientUserId = Guid.NewGuid();
            var patientId = Guid.NewGuid();
            _context.Users.Add(new User { UserId = patientUserId, Email = "edit@mail.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = patientId, UserId = patientUserId, Name = "Старое", Surname = "Имя", MiddleName = "", DateOfBirth = DateTime.UtcNow, Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });
            await _context.SaveChangesAsync();

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
        new Claim(ClaimTypes.NameIdentifier, patientUserId.ToString()),
        new Claim(ClaimTypes.Role, "Patient")
    }));
            var controller = new MedicalBookController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user } }
            };

            var dto = new TelemedicineSystem.API.Controllers.UpdatePatientInfoDto
            {
                BloodType = "III",
                Education = "Высшее"
            };
            var result = await controller.UpdatePatientInfo(dto);

            result.Should().BeOfType<OkObjectResult>();
            var patient = await _context.Patients.FindAsync(patientId);
            patient!.BloodType.Should().Be("III");
            patient.Education.Should().Be("Высшее");
        }
    }
}