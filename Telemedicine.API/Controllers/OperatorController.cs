using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;
using System.Security.Claims;

namespace Telemedicine.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Operator")]
    public class OperatorController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OperatorController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Получить всех пользователей (упрощённая версия)
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Include(u => u.Patient)
                .Include(u => u.Consultant)
                .Include(u => u.Operator)
                .ToListAsync();

            // Формируем результат на клиенте (после загрузки из БД)
            var result = users.Select(u => new
            {
                u.UserId,
                u.Email,
                u.Role,
                FullName = GetUserFullName(u)
            })
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FullName)
            .ToList();

            return Ok(result);
        }

        // Вспомогательный метод для получения ФИО
        private string GetUserFullName(User user)
        {
            if (user.Role == "Patient" && user.Patient != null)
            {
                return $"{user.Patient.Surname} {user.Patient.Name} {user.Patient.MiddleName}".Trim();
            }
            else if (user.Role == "Consultant" && user.Consultant != null)
            {
                return $"{user.Consultant.Surname} {user.Consultant.Name} {user.Consultant.MiddleName}".Trim();
            }
            return user.Email;
        }

        // 2. Получить детали пользователя
        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUserDetails(Guid userId)
        {
            var user = await _context.Users
                .Include(u => u.Patient)
                .Include(u => u.Consultant)
                .Include(u => u.Operator)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound(new { message = "User not found" });

            object profile = null;

            switch (user.Role)
            {
                case "Patient":
                    profile = new
                    {
                        user.UserId,
                        user.Email,
                        user.Role,
                        Patient = new
                        {
                            user.Patient?.Name,
                            user.Patient?.Surname,
                            user.Patient?.MiddleName,
                            user.Patient?.DateOfBirth,
                            user.Patient?.Gender,
                            user.Patient?.ContactInfo,
                            user.Patient?.InsurancePolicyNumber
                        }
                    };
                    break;

                case "Consultant":
                    profile = new
                    {
                        user.UserId,
                        user.Email,
                        user.Role,
                        Consultant = new
                        {
                            user.Consultant?.Name,
                            user.Consultant?.Surname,
                            user.Consultant?.MiddleName,
                            user.Consultant?.Specialty,
                            user.Consultant?.Post,
                            user.Consultant?.EducationInfo,
                            user.Consultant?.Seniority,
                            user.Consultant?.Experience,
                            user.Consultant?.WorkHours,
                            user.Consultant?.ConnectionToCenter
                        }
                    };
                    break;

                case "Operator":
                    profile = new
                    {
                        user.UserId,
                        user.Email,
                        user.Role,
                        Operator = new
                        {
                            user.Operator?.FullName,
                            user.Operator?.Address,
                            user.Operator?.ContactInfo,
                            user.Operator?.ConnectionToSystem
                        }
                    };
                    break;
            }

            return Ok(profile);
        }

        // 3. Регистрация консультанта
        [HttpPost("register/consultant")]
        public async Task<IActionResult> RegisterConsultant([FromBody] RegisterConsultantDto dto)
        {
            // Проверка существующего email
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest(new { message = "Email already exists" });

            // Создание пользователя
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = "Consultant"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Создание консультанта
            var consultant = new Consultant
            {
                ConsultantId = Guid.NewGuid(),
                UserId = user.UserId,
                Name = dto.Name,
                Surname = dto.Surname,
                MiddleName = dto.MiddleName ?? "",
                Specialty = dto.Specialty,
                Post = dto.Post,
                EducationInfo = dto.EducationInfo ?? "",
                Seniority = dto.Seniority,
                Experience = dto.Experience ?? "",
                WorkHours = dto.WorkHours ?? "",
                ConnectionToCenter = dto.ConnectionToCenter ?? ""
            };
            _context.Consultants.Add(consultant);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Consultant registered successfully", userId = user.UserId });
        }
        
        // 4. Удаление пользователя 
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(Guid userId)
        {
            // Находим пользователя
            var user = await _context.Users
                .Include(u => u.Patient)
                .Include(u => u.Consultant)
                .Include(u => u.Operator)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound(new { message = "User not found" });

            // Не даём удалить самого себя (оператора, который выполняет действие)
            var currentUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (user.UserId == currentUserId)
                return BadRequest(new { message = "You cannot delete your own account" });

            // Удаляем связанные данные в зависимости от роли
            switch (user.Role)
            {
                case "Patient":
                    if (user.Patient != null)
                        _context.Patients.Remove(user.Patient);
                    break;
                case "Consultant":
                    if (user.Consultant != null)
                        _context.Consultants.Remove(user.Consultant);
                    break;
                case "Operator":
                    if (user.Operator != null)
                        _context.Operators.Remove(user.Operator);
                    break;
            }

            // Удаляем самого пользователя
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User {user.Email} deleted successfully" });
        }
    }

    public class RegisterConsultantDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string MiddleName { get; set; }
        public string Specialty { get; set; }
        public string Post { get; set; }
        public string EducationInfo { get; set; }
        public int Seniority { get; set; }
        public string Experience { get; set; }
        public string WorkHours { get; set; }
        public string ConnectionToCenter { get; set; }
    }
}