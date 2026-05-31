using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;
using Telemedicine.API.Controllers;

namespace Telemedicine.Tests.Unit.Controllers
{
    public class OperatorControllerTests
    {
        private readonly AppDbContext _context;
        private readonly Guid _operatorUserId = Guid.NewGuid();

        public OperatorControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
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
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };
        }

        [Fact]
        public async Task GetAllUsers_ReturnsList()
        {
            var controller = CreateController();

            var result = await controller.GetAllUsers();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task RegisterConsultant_ValidData_ReturnsOk()
        {
            var controller = CreateController();
            var dto = new RegisterConsultantDto
            {
                Email = "doctor@mail.ru",
                Password = "Pass123!",
                Name = "Пётр",
                Surname = "Петров",
                Specialty = "Терапевт",
                Post = "Врач"
            };

            var result = await controller.RegisterConsultant(dto);

            result.Should().BeOfType<OkObjectResult>();
            _context.Users.Count().Should().Be(1);
            _context.Consultants.Count().Should().Be(1);
        }

        [Fact]
        public async Task RegisterConsultant_DuplicateEmail_ReturnsBadRequest()
        {
            _context.Users.Add(new User { UserId = Guid.NewGuid(), Email = "doctor@mail.ru", PasswordHash = "h", Role = "Consultant" });
            await _context.SaveChangesAsync();
            var controller = CreateController();
            var dto = new RegisterConsultantDto { Email = "doctor@mail.ru", Password = "Pass123!", Name = "Дубль", Surname = "Дублёв", Specialty = "Хирург" };

            var result = await controller.RegisterConsultant(dto);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task DeleteUser_ExistingUser_Deletes()
        {
            var victimId = Guid.NewGuid();
            _context.Users.Add(new User { UserId = victimId, Email = "victim@mail.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = Guid.NewGuid(), UserId = victimId, Name = "Жертва", Surname = "Тестов", MiddleName = "", DateOfBirth = DateTime.UtcNow, Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });
            await _context.SaveChangesAsync();
            var controller = CreateController();

            var result = await controller.DeleteUser(victimId);

            result.Should().BeOfType<OkObjectResult>();
            _context.Users.Count().Should().Be(0);
        }

        [Fact]
        public async Task DeleteUser_Self_ReturnsBadRequest()
        {
            _context.Users.Add(new User { UserId = _operatorUserId, Email = "op@mail.ru", PasswordHash = "h", Role = "Operator" });
            await _context.SaveChangesAsync();
            var controller = CreateController();

            var result = await controller.DeleteUser(_operatorUserId);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetUserDetails_NotFound_Returns404()
        {
            var controller = CreateController();

            var result = await controller.GetUserDetails(Guid.NewGuid());

            result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}