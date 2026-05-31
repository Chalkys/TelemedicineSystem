using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Controllers;
using TelemedicineSystem.API.Hubs;
using TelemedicineSystem.Core.Entities;

namespace Telemedicine.Tests.Integration
{
    public class WebRtcAndDocsTests
    {
        private readonly AppDbContext _context;
        private Guid _patientUserId, _patientId;
        private Guid _consultantUserId, _consultantId;
        private Guid _consultationId;

        public WebRtcAndDocsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);
        }

        private ClaimsPrincipal CreatePrincipal(Guid userId, string role) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim("Email", $"{role}@test.ru")
            }));

        private async Task SeedData()
        {
            _patientUserId = Guid.NewGuid();
            _patientId = Guid.NewGuid();
            _context.Users.Add(new User { UserId = _patientUserId, Email = "pat@t.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = _patientId, UserId = _patientUserId, Name = "Иван", Surname = "Иванов", MiddleName = "", DateOfBirth = DateTime.UtcNow, Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });

            _consultantUserId = Guid.NewGuid();
            _consultantId = Guid.NewGuid();
            _context.Users.Add(new User { UserId = _consultantUserId, Email = "doc@t.ru", PasswordHash = "h", Role = "Consultant" });
            _context.Consultants.Add(new Consultant { ConsultantId = _consultantId, UserId = _consultantUserId, Name = "Пётр", Surname = "Петров", MiddleName = "", Specialty = "Терапевт" });

            var appId = Guid.NewGuid();
            _context.Applications.Add(new Application { ApplicationId = appId, PatientId = _patientId, ConsultantId = _consultantId, Type = 'F', Subject = "Тест", Status = "accepted", CreatedAt = DateTime.UtcNow });

            _consultationId = Guid.NewGuid();
            _context.Consultations.Add(new Consultation { ConsultationId = _consultationId, ApplicationId = appId, PatientId = _patientId, ConsultantId = _consultantId, Date = DateTime.UtcNow, Status = "in_progress" });
            await _context.SaveChangesAsync();
        }

        private ConsultationHub CreateHub(Guid userId, string role)
        {
            var clientsMock = new Mock<IHubCallerClients>();
            var groupsMock = new Mock<IGroupManager>();
            var contextMock = new Mock<HubCallerContext>();

            contextMock.Setup(c => c.User).Returns(CreatePrincipal(userId, role));
            contextMock.Setup(c => c.ConnectionId).Returns("conn1");
            groupsMock.Setup(g => g.AddToGroupAsync("conn1", It.IsAny<string>(), default)).Returns(Task.CompletedTask);

            var proxyMock = new Mock<IClientProxy>();
            clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(proxyMock.Object);
            clientsMock.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(proxyMock.Object);
            var singleProxyMock = new Mock<ISingleClientProxy>();
            clientsMock.Setup(c => c.Caller).Returns(singleProxyMock.Object);
            clientsMock.Setup(c => c.User(It.IsAny<string>())).Returns(singleProxyMock.Object);

            return new ConsultationHub(_context)
            {
                Clients = clientsMock.Object,
                Groups = groupsMock.Object,
                Context = contextMock.Object
            };
        }

        [Fact]
        public async Task SendChatMessage_SavesAndBroadcasts()
        {
            await SeedData();
            var hub = CreateHub(_patientUserId, "Patient");
            await hub.JoinConsultation(_consultationId);

            await hub.SendMessage(_consultationId, "Здравствуйте, доктор!");

            _context.ConsultationMessages.Count().Should().Be(1);
            var msg = await _context.ConsultationMessages.FirstAsync();
            msg.Text.Should().Be("Здравствуйте, доктор!");
        }

        [Fact]
        public async Task WebRtcSignaling_OfferAnswerFlow()
        {
            await SeedData();
            var patientHub = CreateHub(_patientUserId, "Patient");
            var consultantHub = CreateHub(_consultantUserId, "Consultant");

            await patientHub.JoinConsultation(_consultationId);
            await consultantHub.JoinConsultation(_consultationId);

            // Пациент отправляет offer
            await patientHub.SendOffer(_consultationId, "offer-data");

            // Консультант получает offer через GetPendingOffer
            await consultantHub.GetPendingOffer(_consultationId);

            // Консультант отправляет answer
            await consultantHub.SendAnswer(_consultationId, "answer-data");

            // ICE candidates
            await patientHub.SendIceCandidate(_consultationId, "candidate1");
            await consultantHub.SendIceCandidate(_consultationId, "candidate2");

            // HangUp
            await patientHub.HangUpSignal(_consultationId);
            await consultantHub.HangUpSignal(_consultationId);

            // Все вызовы прошли без исключений
            Assert.True(true);
        }

        [Fact]
        public async Task UploadDocument_CreatesFileRecord()
        {
            await SeedData();

            // Создаём папку Uploads
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var controller = new ConsultationController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal(_consultantUserId, "Consultant") } }
            };

            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "test pdf content");
            var file = new FormFile(new FileStream(tempFile, FileMode.Open), 0, new FileInfo(tempFile).Length, "file", "test.pdf");

            var result = await controller.UploadDocument(_consultationId, file);

            result.Should().BeOfType<OkObjectResult>();
            _context.Documents.Count().Should().Be(1);
        }

        [Fact]
        public async Task DownloadDocument_ReturnsNotFoundWhenFileMissing()
        {
            await SeedData();
            var docId = Guid.NewGuid();
            _context.Documents.Add(new Document
            {
                DocumentId = docId,
                FileName = "test.pdf",
                FilePath = "Uploads/nonexistent.pdf",
                DocType = "document",
                UploadedBy = _consultantUserId
            });
            await _context.SaveChangesAsync();

            var controller = new DocumentsController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal(_patientUserId, "Patient") } }
            };

            var result = await controller.Download(docId);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task FullConsultationCycle_PatientToConsultant()
        {
            await SeedData();

            // 1. Чат
            var patientHub = CreateHub(_patientUserId, "Patient");
            await patientHub.JoinConsultation(_consultationId);
            await patientHub.SendMessage(_consultationId, "Здравствуйте!");

            var consultantHub = CreateHub(_consultantUserId, "Consultant");
            await consultantHub.JoinConsultation(_consultationId);
            await consultantHub.SendMessage(_consultationId, "Добрый день!");

            _context.ConsultationMessages.Count().Should().Be(2);

            // 2. Звонок
            await consultantHub.IncomingCall(_consultationId, "Петров Пётр", _patientUserId);
            await patientHub.SendOffer(_consultationId, "offer");
            await consultantHub.SendAnswer(_consultationId, "answer");

            // 3. Завершение консультации
            var controller = new ConsultationController(_context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal(_consultantUserId, "Consultant") } }
            };
            var completeResult = await controller.CompleteConsultation(_consultationId);
            completeResult.Should().BeOfType<OkObjectResult>();

            var consultation = await _context.Consultations.FindAsync(_consultationId);
            consultation!.Status.Should().Be("completed");
        }
    }
}