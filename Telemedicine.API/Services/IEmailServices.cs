using System.Threading.Tasks;

namespace Telemedicine.API.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string message);
    }
}