using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Telemedicine.API.Services;
using Telemedicine.Infrastructure.Data;
using TelemedicineSystem.API.Hubs;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// 1. НАСТРОЙКА СЕРВИСОВ
// Добавляем контроллеры - обрабатывают HTTP запросы (GET, POST и т.д.)
builder.Services.AddControllers();

// Добавляем Swagger - автоматическая документация API (страница /swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Подключаем PostgreSQL через Entity Framework
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. НАСТРОЙКА JWT АУТЕНТИФИКАЦИИ (проверка токенов)

var jwtKey = builder.Configuration["Jwt:Key"];      // Секретный ключ из appsettings.json
var key = Encoding.UTF8.GetBytes(jwtKey);           // Преобразуем строку в байты

builder.Services.AddAuthentication(options =>
{
    // Говорим: "Для проверки входа используем JWT схему"
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Для SignalR — извлекаем токен из query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddSignalR();

// 3. НАСТРОЙКА CORS (разрешаем запросы с других сайтов)

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://127.0.0.1:5500",
            "http://localhost:5500",
            "http://192.168.0.106:5500",
            "http://192.168.1.10:5500",
            "https://suwmw5-95-27-36-221.ru.tuna.am"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();  // ← обязательно для SignalR
    });
});

// Регистрируем настройки Email, чтобы они были доступны через IOptions
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Регистрируем наш сервис как scoped (на время одного запроса)
builder.Services.AddScoped<IEmailService, EmailService>();

// 4. ПОСТРОЕНИЕ ПРИЛОЖЕНИЯ
var app = builder.Build();

// 5. НАСТРОЙКА HTTP-КОНВЕЙЕРА (порядок обработки запросов)

// Включаем Swagger только в режиме разработки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();      // Генерирует JSON документацию
    app.UseSwaggerUI();    // Показывает красивую страницу /swagger
    app.UseDeveloperExceptionPage();
}

// Перенаправляем HTTP на HTTPS
app.UseHttpsRedirection();

// ВАЖНО: CORS должен быть ДО аутентификации!
app.UseCors("AllowFrontend");

// Аутентификация - проверяет, кто отправил запрос (проверяет JWT токен)
app.UseAuthentication();

// Авторизация - проверяет, имеет ли пользователь право на действие
app.UseAuthorization();

app.MapHub<ConsultationHub>("/hubs/consultation");

// Запускаем контроллеры (обработчики API запросов)
app.MapControllers();

// Запускаем приложение
app.Run();