using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.Core.Entities;

namespace TelemedicineSystem.API.Hubs
{
    [Authorize]
    public class ConsultationHub : Hub
    {
        private readonly AppDbContext _context;

        public ConsultationHub(AppDbContext context)
        {
            _context = context;
        }

        public async Task JoinConsultation(Guid consultationId)
        {
            var userId = Guid.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Проверка: пользователь — участник консультации
            var consultation = await _context.Consultations
                .Include(c => c.Application)
                .FirstOrDefaultAsync(c => c.ConsultationId == consultationId);

            if (consultation == null) return;

            var isPatient = await _context.Patients.AnyAsync(p => p.UserId == userId && p.PatientId == consultation.PatientId);
            var isConsultant = await _context.Consultants.AnyAsync(c => c.UserId == userId && c.ConsultantId == consultation.ConsultantId);

            if (!isPatient && !isConsultant) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, consultationId.ToString());
        }

        public async Task SendMessage(Guid consultationId, string text)
        {
            var userId = Guid.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var userName = Context.User.FindFirst("Email")?.Value ?? "User";

            var message = new ConsultationMessage
            {
                Id = Guid.NewGuid(),
                ConsultationId = consultationId,
                SenderId = userId,
                Text = text,
                SentAt = DateTime.UtcNow
            };

            _context.ConsultationMessages.Add(message);
            await _context.SaveChangesAsync();

            await Clients.Group(consultationId.ToString()).SendAsync("ReceiveMessage", new
            {
                message.Id,
                message.Text,
                SenderId = userId,
                SenderName = userName,
                message.SentAt
            });
        }

        // Сигналинг для WebRTC
        public async Task SendOffer(Guid consultationId, string offer)
        {
            await Clients.OthersInGroup(consultationId.ToString()).SendAsync("ReceiveOffer", offer);
        }

        public async Task SendAnswer(Guid consultationId, string answer)
        {
            await Clients.OthersInGroup(consultationId.ToString()).SendAsync("ReceiveAnswer", answer);
        }

        public async Task SendIceCandidate(Guid consultationId, string candidate)
        {
            await Clients.OthersInGroup(consultationId.ToString()).SendAsync("ReceiveIceCandidate", candidate);
        }
    }
}