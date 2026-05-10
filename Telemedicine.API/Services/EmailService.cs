using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Threading.Tasks;

namespace Telemedicine.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        // IOptions позволяет получить настройки из appsettings.json и secrets.json
        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("Телемедицина", _emailSettings.From));
            emailMessage.To.Add(new MailboxAddress("", toEmail));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart("html") { Text = message };

            using (var client = new SmtpClient())
            {
                // Подключаемся к SMTP-серверу
                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, SecureSocketOptions.StartTls);
                // Аутентифицируемся
                await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
                // Отправляем письмо
                await client.SendAsync(emailMessage);
                // Закрываем соединение
                await client.DisconnectAsync(true);
            }
        }
    }
}