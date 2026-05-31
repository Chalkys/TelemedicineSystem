using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Controllers;

namespace Telemedicine.Tests.Unit.Controllers
{
    public class SeedControllerTests
    {
        private readonly AppDbContext _context;

        public SeedControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        [Fact]
        public async Task SeedMkb_NoFile_Throws()
        {
            var controller = new SeedController(_context);

            // Act
            var exception = await Record.ExceptionAsync(() => controller.SeedMkb());

            // Assert — падает с любой ошибкой, т.к. файла нет
            Assert.NotNull(exception);
        }

        [Fact]
        public async Task SeedMedications_NoFile_Throws()
        {
            var controller = new SeedController(_context);

            var exception = await Record.ExceptionAsync(() => controller.SeedMedications());

            Assert.NotNull(exception);
        }
    }
}