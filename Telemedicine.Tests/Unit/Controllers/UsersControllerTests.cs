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
    public class UsersControllerTests
    {
        private readonly AppDbContext _context;
        private readonly Guid _userId = Guid.NewGuid();

        public UsersControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        private UsersController CreateController(Guid userId, string role)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }));
            return new UsersController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };
        }

        [Fact]
        public async Task GetCurrentUser_Patient_ReturnsPatientInfo()
        {
            _context.Users.Add(new User { UserId = _userId, Email = "pat@mail.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = Guid.NewGuid(), UserId = _userId, Name = "Иван", Surname = "Иванов", MiddleName = "", DateOfBirth = DateTime.UtcNow, Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });
            await _context.SaveChangesAsync();
            var controller = CreateController(_userId, "Patient");

            var result = await controller.GetCurrentUser();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetCurrentUser_NoToken_ReturnsUnauthorized()
        {
            var controller = new UsersController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var result = await controller.GetCurrentUser();

            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task GetCurrentUser_UnknownUser_ReturnsNotFound()
        {
            var controller = CreateController(Guid.NewGuid(), "Patient");

            var result = await controller.GetCurrentUser();

            result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}