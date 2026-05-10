using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Telemedicine.API.Services;
using Telemedicine.Infrastructure.Data;

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
    // Разрешаем HTTP (не только HTTPS) - для разработки
    options.RequireHttpsMetadata = false;

    // Сохраняем токен в свойствах запроса
    options.SaveToken = true;

    // Параметры проверки токена
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // Проверяем, что подпись токена создана нашим ключом
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),

        // Проверяем, что токен выпущен нашим сервером
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        // Проверяем, что токен предназначен для нашего клиента
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],

        // Проверяем, что токен не просрочен
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero  // Без дополнительной задержки
    };
});

// 3. НАСТРОЙКА CORS (разрешаем запросы с других сайтов)

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        // Разрешаем запросы с любых адресов (для разработки)
        policy.AllowAnyOrigin()
              .AllowAnyMethod()   // GET, POST, PUT, DELETE
              .AllowAnyHeader();  // Любые заголовки
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

// Запускаем контроллеры (обработчики API запросов)
app.MapControllers();

// Запускаем приложение
app.Run();