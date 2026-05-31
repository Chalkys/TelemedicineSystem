using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Controllers;
using TelemedicineSystem.Core.Entities;

namespace Telemedicine.Tests.Unit.Controllers
{
    public class MkbControllerTests
    {
        private readonly AppDbContext _context;

        public MkbControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        private MkbController CreateController()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Consultant")
            }));
            return new MkbController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };
        }

        [Fact]
        public async Task Search_EmptyQuery_ReturnsEmpty()
        {
            var controller = CreateController();

            var result = await controller.Search("");

            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Search_ValidQuery_ReturnsResults()
        {
            _context.Diseases.Add(new Disease { DiseaseId = Guid.NewGuid(), MkbCode = "G43", Name = "Мигрень" });
            await _context.SaveChangesAsync();
            var controller = CreateController();

            var result = await controller.Search("Мигрень");

            result.Should().BeOfType<OkObjectResult>();
        }
    }
}