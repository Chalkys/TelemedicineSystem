using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Telemedicine.API.Services;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;

namespace Telemedicine.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;

        public AuthController(AppDbContext context, IConfiguration config, IEmailService emailService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
        }

        [HttpPost("register/patient")]
        public async Task<IActionResult> RegisterPatient([FromBody] RegisterPatientDto dto)
        {
            // 1. Проверка существующего email
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest(new { message = "Email already exists" });

            // 2. Создание пользователя
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "Patient"
            };
            _context.Users.Add(user);

            // 3. Создание пациента
            var utcDateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth, DateTimeKind.Utc);
            var patient = new Patient
            {
                PatientId = Guid.NewGuid(),
                UserId = user.UserId,
                Name = dto.Name,
                Surname = dto.Surname,
                MiddleName = dto.MiddleName ?? "",
                DateOfBirth = utcDateOfBirth,
                Gender = dto.Gender,
                ContactInfo = dto.ContactInfo ?? "",
                InsurancePolicyNumber = dto.InsurancePolicyNumber ?? ""
            };
            _context.Patients.Add(patient);

            // 4. Сохраняем всё в БД ОДНИМ вызовом
            await _context.SaveChangesAsync();

            // 5. Отправляем письмо (после успешного сохранения в БД)
            try
            {
                string subject = "Добро пожаловать в Телемедицину!";
                string message = $@"
        <h2>Уважаемый(ая) {dto.Surname} {dto.Name}!</h2>
        <p>Ваша учетная запись в системе телемедицины успешно создана.</p>
        <p>Вы можете войти в систему, используя свой email: <strong>{dto.Email}</strong></p>
        <hr>
        <p>С уважением,<br>Команда Телемедицины</p>";

                await _emailService.SendEmailAsync(dto.Email, subject, message);
                Console.WriteLine("Registration email sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED to send registration email: {ex.Message}");
                // Не прерываем выполнение — письмо не критично для регистрации
            }

            return Ok(new { message = "Patient registered successfully", userId = user.UserId });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid email or password" });

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                token = token,
                role = user.Role,
                userId = user.UserId
            });
        }

        // Отправка письма для сброса пароля
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            // Для безопасности: не сообщаем, существует ли пользователь
            if (user == null)
            {
                return Ok(new { message = "Если email зарегистрирован, вы получите инструкции по сбросу пароля" });
            }

            // Удаляем старые неиспользованные токены для этого email
            var oldTokens = _context.PasswordResetTokens
                .Where(t => t.Email == dto.Email && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);
            _context.PasswordResetTokens.RemoveRange(oldTokens);

            // Генерируем новый токен
            var resetToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("/", "_").Replace("+", "-").Replace("=", "");

            var tokenEntity = new PasswordResetToken
            {
                Email = dto.Email,
                Token = resetToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(tokenEntity);
            await _context.SaveChangesAsync();

            // Ссылка для сброса
            var resetLink = $"http://localhost:5500/reset-password.html?token={resetToken}&email={dto.Email}";

            string subject = "Сброс пароля - Телемедицина";
            string message = $@"
    <h2>Здравствуйте!</h2>
    <p>Вы запросили сброс пароля для вашей учётной записи в системе телемедицины.</p>
    <p>Для сброса пароля перейдите по ссылке:</p>
    <p><a href='{resetLink}'>Сбросить пароль</a></p>
    <p>Ссылка действительна в течение 1 часа.</p>
    <p>Если вы не запрашивали сброс пароля, проигнорируйте это письмо.</p>
    <hr>
    <p>С уважением,<br>Команда Телемедицины</p>";

            await _emailService.SendEmailAsync(dto.Email, subject, message);

            return Ok(new { message = "Если email зарегистрирован, вы получите инструкции по сбросу пароля" });
        }

        // Установка нового пароля по токену
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            // Находим токен в БД
            var tokenEntity = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == dto.Token && t.Email == dto.Email && !t.IsUsed);

            if (tokenEntity == null)
                return BadRequest(new { message = "Недействительная или уже использованная ссылка" });

            if (tokenEntity.ExpiresAt < DateTime.UtcNow)
                return BadRequest(new { message = "Срок действия ссылки истёк. Запросите сброс пароля заново." });

            // Находим пользователя
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
                return BadRequest(new { message = "Пользователь не найден" });

            // Проверяем, что новый пароль не совпадает со старым
            if (BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash))
                return BadRequest(new { message = "Новый пароль должен отличаться от текущего" });

            // Устанавливаем новый пароль
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

            // Помечаем токен как использованный
            tokenEntity.IsUsed = true;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Пароль успешно изменён. Теперь вы можете войти с новым паролем." });
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("Email", user.Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(_config["Jwt:ExpireMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // DTO классы
    public class RegisterPatientDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string MiddleName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string ContactInfo { get; set; }
        public string InsurancePolicyNumber { get; set; }
    }

    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class ForgotPasswordDto
    {
        public string Email { get; set; }
    }

    public class ResetPasswordDto
    {
        public string Email { get; set; }
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }
}