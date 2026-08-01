using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using RugsManagment.Application;
using RugsManagment.Infrastructure;
using RugsManagment.Infrastructure.Persistence.Seed;
using RugsManagment.Web.Support;

// ═══════════════════════════════════════════════════════════════════
// میزبان یکپارچه: MVC/Razor + کنترلرهای API + جزیره‌های Vue
// یک اپلیکیشن، یک پورت. بدون سرور جداگانه‌ی فرانت در تولید.
// آماده برای اجرا پشت پراکسی معکوس (CapRover/Nginx) که SSL را مدیریت می‌کند.
// ═══════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

// لاگ ساخت‌یافته: در تولید JSON یک‌خطی تا مستقیم قابل ingest و جستجو باشد.
// در توسعه خروجی خوانای معمولی می‌ماند. IncludeScopes لازم است وگرنه
// کلیدهای RequestId/TenantId/UserId که میان‌افزار می‌گذارد در خروجی نمی‌آیند.
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(o => { o.IncludeScopes = true; o.SingleLine = true; });
}
else
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.UseUtcTimestamp = true; });
}

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

// پل هویت: تنها جایی که لایهٔ وب کاربر جاری را به Application می‌رساند (برای لاگ حسابرسی)
builder.Services.AddScoped<RugsManagment.Application.Abstractions.Services.ICurrentUser,
    RugsManagment.Web.Auth.HttpCurrentUser>();

// دسترسی viewها به تنظیمات کارگاه (واحد پول، لوگو) — با کش کوتاه در طول هر درخواست
builder.Services.AddScoped<RugsManagment.Web.Support.TenantSettingsAccessor>();
builder.Services.AddScoped<RugsManagment.Web.Support.IImageUploadHelper,
    RugsManagment.Web.Support.ImageUploadHelper>();

// ═══════════════════════════════════════════════════════════════════
// محدودیت نرخ — دفاع در برابر حدس رمز
// ═══════════════════════════════════════════════════════════════════
// فقط روی ورود اعمال می‌شود؛ بقیهٔ اپ پشت احراز هویت است و محدودکردنش
// کار اپراتوری را که تند کار می‌کند مختل می‌کرد.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // کلید بر اساس IP — پشت پراکسی، UseForwardedHeaders آی‌پی واقعی را می‌دهد
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    // پیام فارسی به‌جای صفحهٔ خالی ۴۲۹
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "تلاش‌های زیاد برای ورود. لطفاً چند دقیقه صبر کنید و دوباره امتحان کنید.", ct);
    };
});

// بررسی سلامت: اتصال دیتابیس مهم‌ترین وابستگی است
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RugsManagment.Infrastructure.Persistence.AppDbContext>("database");

var app = builder.Build();

// باید قبل از بقیهٔ میان‌افزارها باشد تا scheme درست تشخیص داده شود
app.UseForwardedHeaders();

// هدرهای امنیتی و CSP — قبل از هر چیزی که پاسخ می‌سازد
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// صفحهٔ فارسی و هم‌سبک برای ۴۰۴/۴۰۳/… (بدون تغییر آدرس و بدون تغییر کد وضعیت).
// مسیرهای /api/* از این شاخه بیرون می‌مانند تا همچنان JSON برگردانند نه HTML.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase),
    branch => branch.UseStatusCodePagesWithReExecute("/status/{0}"));

// پیش‌فرض خاموش است چون CapRover در لبه HTTPS را اجبار می‌کند؛ برای اجرای مستقیم HTTPS قابل‌فعال‌سازی.
if (builder.Configuration.GetValue("Hosting:UseHttpsRedirection", false))
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

// محدودیت نرخ باید بعد از Routing باشد تا سیاست هر endpoint شناخته شود
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// بعد از احراز هویت تا کاربر و کارگاه در scope لاگ بنشینند
app.UseRequestLogScope();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// بررسی سلامت برای CapRover و مانیتورینگ — بدون احراز هویت، بدون افشای جزئیات
app.MapHealthChecks("/health").AllowAnonymous();

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
