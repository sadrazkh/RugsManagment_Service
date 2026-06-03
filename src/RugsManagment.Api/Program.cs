using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RugsManagment.Application;
using RugsManagment.Infrastructure;
using RugsManagment.Infrastructure.Identity;
using RugsManagment.Infrastructure.Persistence.Seed;

// ═══════════════════════════════════════════════════════════════════
// نقطهٔ ورود API — ترتیب راه‌اندازی:
// 1) ثبت سرویس‌های Application و Infrastructure (دیتابیس، JWT، Repository)
// 2) احراز هویت JWT
// 3) CORS برای فرانت Vue روی پورت 5173
// 4) هنگام استارت: migration + دادهٔ اولیه (Seed)
// ═══════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// هر درخواست باید هدر Authorization: Bearer <token> داشته باشد (به جز login)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Client", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                ?? ["http://localhost:5173", "http://127.0.0.1:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Client");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// اگر مستقیم آدرس API را در مرورگر باز کنید، به Swagger هدایت می‌شود (نه Not Found)
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/api", () => Results.Json(new
{
    message = "Rugs Management API فعال است. رابط کاربری: http://localhost:5173",
    swagger = "/swagger",
    login = "POST /api/auth/login"
}));

// مراحل سیستم، ادمین، کارگاه دمو، قالب‌های نمونه
await DatabaseSeeder.SeedAsync(app.Services);

app.Run();

/*

cd c:\Users\sadra\source\repos\RugsManagment_Service\client
npm run dev

*/