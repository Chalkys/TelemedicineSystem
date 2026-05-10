using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;

namespace Telemedicine.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]  // Только авторизованные пользователи
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            // Получаем userId из JWT токена
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized(new { message = "Invalid token" });

            var userId = Guid.Parse(userIdClaim.Value);

            // Ищем пользователя
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound(new { message = "User not found" });

            // Возвращаем информацию в зависимости от роли
            if (user.Role == "Patient")
            {
                var patient = await _context.Patients
                    .FirstOrDefaultAsync(p => p.UserId == userId);

                return Ok(new
                {
                    userId = user.UserId,
                    email = user.Email,
                    role = user.Role,
                    name = patient?.Name,
                    surname = patient?.Surname,
                    middleName = patient?.MiddleName,
                    dateOfBirth = patient?.DateOfBirth,
                    gender = patient?.Gender,
                    contactInfo = patient?.ContactInfo
                });
            }

            // Для Consultant и Operator добавите позже
            return Ok(new
            {
                userId = user.UserId,
                email = user.Email,
                role = user.Role
            });
        }
    }
}