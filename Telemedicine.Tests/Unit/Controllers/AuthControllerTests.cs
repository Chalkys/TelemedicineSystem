using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Telemedicine.API.Controllers;
using Telemedicine.API.Services;
using Telemedicine.Core.Entities;
using Telemedicine.Infrastructure.Data;

namespace Telemedicine.Tests.Unit.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly AppDbContext _context;

        public AuthControllerTests()
        {
            _emailServiceMock = new Mock<IEmailService>();
            _configMock = new Mock<IConfiguration>();

            // InMemory DB
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new AppDbContext(options);

            // JWT config mock
            _configMock.Setup(c => c["Jwt:Key"]).Returns("ThisIsASuperSecretKeyForTesting123!");
            _configMock.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
            _configMock.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
            _configMock.Setup(c => c["Jwt:ExpireMinutes"]).Returns("60");
        }

        private AuthController CreateController() =>
            new(_context, _configMock.Object, _emailServiceMock.Object);

        #region RegisterPatient

        [Fact]
        public async Task RegisterPatient_ValidData_ReturnsOk()
        {
            // Arrange
            var dto = new RegisterPatientDto
            {
                Email = "test@mail.ru",
                Password = "Test123!",
                Name = "Иван",
                Surname = "Иванов",
                MiddleName = "Иванович",
                DateOfBirth = new DateTime(1990, 1, 15),
                Gender = "Male",
                ContactInfo = "+79991234567",
                InsurancePolicyNumber = "1234567890123456"
            };
            var controller = CreateController();

            // Act
            var result = await controller.RegisterPatient(dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _context.Users.Count().Should().Be(1);
            _context.Patients.Count().Should().Be(1);
            _emailServiceMock.Verify(e => e.SendEmailAsync(
                dto.Email,
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RegisterPatient_DuplicateEmail_ReturnsBadRequest()
        {
            // Arrange
            var existingUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = "duplicate@mail.ru",
                PasswordHash = "hash",
                Role = "Patient"
            };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var dto = new RegisterPatientDto
            {
                Email = "duplicate@mail.ru",
                Password = "Test123!",
                Name = "Пётр",
                Surname = "Петров",
                DateOfBirth = new DateTime(1985, 5, 20),
                Gender = "Male"
            };
            var controller = CreateController();

            // Act
            var result = await controller.RegisterPatient(dto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region Login

        [Fact]
        public async Task Login_ValidCredentials_ReturnsToken()
        {
            // Arrange
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Test123!");
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "login@mail.ru",
                PasswordHash = passwordHash,
                Role = "Patient"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var dto = new LoginDto { Email = "login@mail.ru", Password = "Test123!" };
            var controller = CreateController();

            // Act
            var result = await controller.Login(dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = (OkObjectResult)result;
            var valueType = okResult.Value!.GetType();

            ((string)valueType.GetProperty("role")!.GetValue(okResult.Value)!).Should().Be("Patient");
            ((Guid)valueType.GetProperty("userId")!.GetValue(okResult.Value)!).Should().Be(user.UserId);
            ((string)valueType.GetProperty("token")!.GetValue(okResult.Value)!).Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsUnauthorized()
        {
            // Arrange
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass1!");
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "wrong@mail.ru",
                PasswordHash = passwordHash,
                Role = "Patient"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var dto = new LoginDto { Email = "wrong@mail.ru", Password = "WrongPass1!" };
            var controller = CreateController();

            // Act
            var result = await controller.Login(dto);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        #endregion

        #region ForgotPassword

        [Fact]
        public async Task ForgotPassword_ExistingEmail_CreatesTokenAndSendsEmail()
        {
            // Arrange
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "forgot@mail.ru",
                PasswordHash = "hash",
                Role = "Patient"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var dto = new ForgotPasswordDto { Email = "forgot@mail.ru" };
            var controller = CreateController();

            // Act
            var result = await controller.ForgotPassword(dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _context.PasswordResetTokens.Count().Should().Be(1);
            _emailServiceMock.Verify(e => e.SendEmailAsync(
                dto.Email,
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ForgotPassword_NonExistentEmail_StillReturnsOk()
        {
            // Arrange
            var dto = new ForgotPasswordDto { Email = "nonexistent@mail.ru" };
            var controller = CreateController();

            // Act
            var result = await controller.ForgotPassword(dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _context.PasswordResetTokens.Should().BeEmpty();
        }

        #endregion

        #region ResetPassword

        [Fact]
        public async Task ResetPassword_ValidToken_ChangesPassword()
        {
            // Arrange
            var oldHash = BCrypt.Net.BCrypt.HashPassword("OldPass1!");
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "reset@mail.ru",
                PasswordHash = oldHash,
                Role = "Patient"
            };
            _context.Users.Add(user);

            var token = new PasswordResetToken
            {
                Email = "reset@mail.ru",
                Token = "valid-token-123",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var dto = new ResetPasswordDto
            {
                Email = "reset@mail.ru",
                Token = "valid-token-123",
                NewPassword = "NewPass1!"
            };
            var controller = CreateController();

            // Act
            var result = await controller.ResetPassword(dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            token.IsUsed.Should().BeTrue();
            var updatedUser = await _context.Users.FirstAsync(u => u.Email == "reset@mail.ru");
            BCrypt.Net.BCrypt.Verify("NewPass1!", updatedUser.PasswordHash).Should().BeTrue();
        }

        [Fact]
        public async Task ResetPassword_SamePassword_ReturnsBadRequest()
        {
            // Arrange
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("SamePass1!");
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "same@mail.ru",
                PasswordHash = passwordHash,
                Role = "Patient"
            };
            _context.Users.Add(user);

            var token = new PasswordResetToken
            {
                Email = "same@mail.ru",
                Token = "token-same",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync();

            var dto = new ResetPasswordDto
            {
                Email = "same@mail.ru",
                Token = "token-same",
                NewPassword = "SamePass1!"  // Тот же пароль
            };
            var controller = CreateController();

            // Act
            var result = await controller.ResetPassword(dto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion
    }
}