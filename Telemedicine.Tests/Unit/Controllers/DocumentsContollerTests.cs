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
    public class DocumentsControllerTests
    {
        private readonly AppDbContext _context;

        public DocumentsControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        private DocumentsController CreateController()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Patient")
            }));
            return new DocumentsController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };
        }

        [Fact]
        public async Task Download_NotFound_Returns404()
        {
            var controller = CreateController();

            var result = await controller.Download(Guid.NewGuid());

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task Download_DocumentExistsButFileMissing_Returns404()
        {
            var docId = Guid.NewGuid();
            _context.Documents.Add(new Document
            {
                DocumentId = docId,
                FileName = "test.pdf",
                FilePath = "Uploads/nonexistent.pdf",
                DocType = "document",
                UploadedBy = Guid.NewGuid()
            });
            await _context.SaveChangesAsync();
            var controller = CreateController();

            var result = await controller.Download(docId);

            result.Should().BeOfType<NotFoundObjectResult>();
        }
    }
}