using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using RugsManagment.Application;
using RugsManagment.Infrastructure;
using RugsManagment.Infrastructure.Persistence.Seed;

// ═══════════════════════════════════════════════════════════════════
// میزبان یکپارچه: MVC/Razor + کنترلرهای API + جزیره‌های Vue
// یک اپلیکیشن، یک پورت. بدون سرور جداگانه‌ی فرانت در تولید.
// آماده برای اجرا پشت پراکسی معکوس (CapRover/Nginx) که SSL را مدیریت می‌کند.
// ═══════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// کلیدهای Data Protection را ماندگار کن تا کوکی و توکن antiforgery با ری‌استارت باطل نشوند.
// مسیر از طریق DataProtection:KeyPath قابل تنظیم است تا در کانتینر به یک volume ماندگار اشاره کند.
var keyPath = builder.Configuration["DataProtection:KeyPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    .SetApplicationName("RugsManagment");

// پردازش هدرهای X-Forwarded-* از پراکسی تا scheme/آی‌پی واقعی شناخته شود (کوکی Secure درست کار کند)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // پراکسی CapRover در شبکهٔ داخلی است؛ محدودیت شبکه/پراکسی را برمی‌داریم
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews();

// خروجی HTML فارسی به‌صورت UTF-8 تمیز (کاراکترهای خطرناک HTML همچنان escape می‌شوند).
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));

// محافظت CSRF برای APIها: جزیره‌های Vue توکن را از meta خوانده و در این هدر می‌فرستند.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddScoped<RugsManagment.Web.Controllers.Api.ApiExceptionFilter>();

// احراز هویت با کوکی — همان claimها (NameIdentifier / Role / tenant_id) که بک‌اند انتظار دارد
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "rugs.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // پشت HTTPS (پراکسی) کوکی Secure می‌شود؛ در توسعهٔ HTTP معمولی می‌ماند
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<RugsManagment.Web.Frontend.ViteAssets>();

var app = builder.Build();

// باید قبل از بقیهٔ میان‌افزارها باشد تا scheme درست تشخیص داده شود
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// پیش‌فرض خاموش است چون CapRover در لبه HTTPS را اجبار می‌کند؛ برای اجرای مستقیم HTTPS قابل‌فعال‌سازی.
if (builder.Configuration.GetValue("Hosting:UseHttpsRedirection", false))
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// migration + دادهٔ اولیه — با چند بار تلاش تا اگر دیتابیس هنگام استارت هنوز آماده نبود کرش نکند
await MigrateAndSeedWithRetryAsync(app);

app.Run();

// ─────────────────────────────────────────────────────────────
static async Task MigrateAndSeedWithRetryAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    const int maxAttempts = 10;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await DatabaseSeeder.SeedAsync(app.Services);
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "اتصال/مهاجرت دیتابیس ناموفق (تلاش {Attempt}/{Max}). تلاش دوباره...", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}
