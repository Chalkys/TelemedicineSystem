using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Hubs;
using TelemedicineSystem.Core.Entities;

namespace Telemedicine.Tests.Unit.Hubs
{
    public class ConsultationHubTests
    {
        private readonly AppDbContext _context;
        private readonly Guid _consultationId = Guid.NewGuid();
        private readonly Guid _patientUserId = Guid.NewGuid();
        private readonly Guid _patientId = Guid.NewGuid();
        private readonly Guid _consultantUserId = Guid.NewGuid();
        private readonly Guid _consultantId = Guid.NewGuid();
        private readonly Mock<IHubCallerClients> _clientsMock;
        private readonly Mock<IGroupManager> _groupsMock;
        private readonly Mock<HubCallerContext> _contextMock;
        private readonly ConsultationHub _hub;

        public ConsultationHubTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            _clientsMock = new Mock<IHubCallerClients>();
            _groupsMock = new Mock<IGroupManager>();
            _contextMock = new Mock<HubCallerContext>();

            _hub = new ConsultationHub(_context)
            {
                Clients = _clientsMock.Object,
                Groups = _groupsMock.Object,
                Context = _contextMock.Object
            };
        }

        private async Task SeedConsultation()
        {
            _context.Users.Add(new User { UserId = _patientUserId, Email = "pat@t.ru", PasswordHash = "h", Role = "Patient" });
            _context.Patients.Add(new Patient { PatientId = _patientId, UserId = _patientUserId, Name = "Иван", Surname = "Иванов", MiddleName = "", DateOfBirth = DateTime.UtcNow, Gender = "Male", ContactInfo = "+7", InsurancePolicyNumber = "1" });
            _context.Users.Add(new User { UserId = _consultantUserId, Email = "cons@t.ru", PasswordHash = "h", Role = "Consultant" });
            _context.Consultants.Add(new Consultant { ConsultantId = _consultantId, UserId = _consultantUserId, Name = "Пётр", Surname = "Петров", MiddleName = "", Specialty = "Терапевт" });
            var app = new Application { ApplicationId = Guid.NewGuid(), PatientId = _patientId, ConsultantId = _consultantId, Type = 'F', Subject = "T", Status = "accepted", CreatedAt = DateTime.UtcNow };
            _context.Applications.Add(app);
            await _context.SaveChangesAsync();
            _context.Consultations.Add(new Consultation { ConsultationId = _consultationId, ApplicationId = app.ApplicationId, PatientId = _patientId, ConsultantId = _consultantId, Date = DateTime.UtcNow, Status = "in_progress" });
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task JoinConsultation_ValidPatient_JoinsGroup()
        {
            await SeedConsultation();
            _contextMock.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _patientUserId.ToString())
            })));
            _contextMock.Setup(c => c.ConnectionId).Returns("conn1");
            _groupsMock.Setup(g => g.AddToGroupAsync("conn1", _consultationId.ToString(), default))
                .Returns(Task.CompletedTask);

            await _hub.JoinConsultation(_consultationId);

            _groupsMock.Verify(g => g.AddToGroupAsync("conn1", _consultationId.ToString(), default), Times.Once);
        }

        [Fact]
        public async Task JoinConsultation_InvalidUser_DoesNotJoin()
        {
            await SeedConsultation();
            _contextMock.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            })));
            _contextMock.Setup(c => c.ConnectionId).Returns("conn2");

            await _hub.JoinConsultation(_consultationId);

            _groupsMock.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task SendMessage_SavesAndBroadcasts()
        {
            await SeedConsultation();
            _contextMock.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _patientUserId.ToString()),
                new Claim("Email", "pat@t.ru")
            })));
            var proxyMock = new Mock<IClientProxy>();
            _clientsMock.Setup(c => c.Group(_consultationId.ToString())).Returns(proxyMock.Object);

            await _hub.SendMessage(_consultationId, "Привет!");

            proxyMock.Verify(p => p.SendCoreAsync("ReceiveMessage", It.IsAny<object[]>(), default), Times.Once);
            _context.ConsultationMessages.Count().Should().Be(1);
        }

        [Fact]
        public async Task SendOffer_BroadcastsToOthers()
        {
            await SeedConsultation();
            var proxyMock = new Mock<IClientProxy>();
            _clientsMock.Setup(c => c.OthersInGroup(_consultationId.ToString())).Returns(proxyMock.Object);

            await _hub.SendOffer(_consultationId, "offer-data");

            proxyMock.Verify(p => p.SendCoreAsync("ReceiveOffer", It.Is<object[]>(o => (string)o[0] == "offer-data"), default), Times.Once);
        }
    }
}