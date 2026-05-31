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
    public class ConsultantControllerTests
    {
        private readonly AppDbContext _context;
        private readonly Guid _consultantId = Guid.NewGuid();

        public ConsultantControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        private ConsultantController CreateController()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Patient")
            }));
            return new ConsultantController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };
        }

        private async Task Seed()
        {
            _context.Consultants.Add(new Consultant
            {
                ConsultantId = _consultantId,
                UserId = Guid.NewGuid(),
                Name = "Пётр",
                Surname = "Петров",
                Specialty = "Терапевт"
            });
            _context.Consultants.Add(new Consultant
            {
                ConsultantId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Name = "Ольга",
                Surname = "Сидорова",
                Specialty = "Кардиолог"
            });
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetSpecializations_ReturnsList()
        {
            await Seed();
            var controller = CreateController();

            var result = await controller.GetSpecializations();

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetConsultants_All_ReturnsAll()
        {
            await Seed();
            var controller = CreateController();

            var result = await controller.GetConsultants(null);

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetConsultants_BySpecialty_ReturnsFiltered()
        {
            await Seed();
            var controller = CreateController();

            var result = await controller.GetConsultants("Кардиолог");

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetSlots_ReturnsSlots()
        {
            var controller = CreateController();

            var result = await controller.GetSlots(Guid.NewGuid(), DateTime.Today);

            result.Should().BeOfType<OkObjectResult>();
        }
    }
}