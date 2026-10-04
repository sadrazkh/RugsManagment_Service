using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Common;
using RugsManagment.Infrastructure;
using RugsManagment.Infrastructure.Persistence.Seed;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Filters;
using RugsManagment.Web.Home;
using RugsManagment.Web.Navigation;
using RugsManagment.Web.Settings;
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

// DomainExceptionPageFilter سراسری: خطای دامنه در فرم‌های Razor (فرش در نگله است، مقدار نامعتبر) toast خطا +
// بازگشت به صفحهٔ قبل می‌شود، نه صفحهٔ ۵۰۰. کنترلرهای API را کاری ندارد (ApiExceptionFilter آن‌ها را دارد).
builder.Services.AddControllersWithViews(options => options.Filters.Add<DomainExceptionPageFilter>())
    .ConfigureApiBehaviorOptions(options =>
    {
        // خطای bind بدنهٔ JSON (عدد نامعتبر، enum ناشناخته) هم همان شکل خطاهای سرویس را دارد:
        // {message, errors:{field:[msg]}} با پیام فارسی، نه ProblemDetails انگلیسی که جزیره نمی‌فهمد.
        options.InvalidModelStateResponseFactory = context =>
        {
            // دو کلید ModelState ممکن است به یک نام فیلد برسند («$.textScale» و «TextScale»)؛ با هم ادغام می‌شوند
            var errors = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .SelectMany(entry => entry.Value!.Errors.Select(e => (
                    Field: HostConventions.ApiFieldKey(entry.Key),
                    Message: e.Exception is null && HostConventions.IsPersian(e.ErrorMessage)
                        ? e.ErrorMessage
                        : HostConventions.InvalidFieldMessage)))
                .GroupBy(e => e.Field, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(e => e.Message).Distinct().ToArray(),
                    StringComparer.Ordinal);

            return new BadRequestObjectResult(new { message = Messages.ValidationFailed.Text, errors });
        };
    });

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
        // هر درخواست، مهر امنیتی و نقش/فعال‌بودن کاربر و کارگاه را با دیتابیس (با کش کوتاه) می‌سنجد؛
        // وگرنه کاربرِ غیرفعال‌شده یا تنزل‌یافته تا پایان عمر کوکی (۸ ساعت لغزان) دسترسی می‌ماند.
        options.Events.OnValidatePrincipal = RugsManagment.Web.Auth.SecurityStampValidator.ValidateAsync;

        // APIها کد وضعیت می‌گیرند نه ریدایرکت: fetch جزیره ریدایرکت را دنبال می‌کرد و به‌جای ۴۰۱/۴۰۳ صفحهٔ
        // HTML ورود یا «دسترسی مجاز نیست» را با ۲۰۰ می‌گرفت. صفحه‌های MVC همان ریدایرکت قبلی را دارند.
        options.Events.OnRedirectToLogin = context =>
        {
            if (!HostConventions.IsApiRequest(context.Request))
            {
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new { message = HostConventions.SessionExpiredMessage });
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (!HostConventions.IsApiRequest(context.Request))
            {
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(new { message = Messages.Forbidden.Text });
        };
    });

// کش حافظه برای SecurityStampValidator تا هر درخواست یک کوئری دیتابیس نخورد
builder.Services.AddMemoryCache();

// سه سیاست Floor / Business / Configure (Web/Auth/Policies.cs) — جای [Authorize(Roles = …)]ِ پراکنده
builder.Services.AddAuthorization(options => options.AddRugsPolicies());
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<RugsManagment.Web.Frontend.ViteAssets>();

// پل هویت: تنها جایی که لایهٔ وب کاربر جاری را به Application می‌رساند (برای لاگ حسابرسی)
builder.Services.AddScoped<RugsManagment.Application.Abstractions.Services.ICurrentUser,
    RugsManagment.Web.Auth.HttpCurrentUser>();

// «انباردار هیچ مبلغی نمی‌بیند»: پاسخ از سیاست Business. Replace تا اگر لایه‌ای پیش‌فرضی ثبت کرده بود
// (مثلاً برای اجرای بدون وب)، در میزبان وب همیشه همین پیاده‌سازی برنده باشد.
builder.Services.Replace(ServiceDescriptor.Scoped<IMoneyVisibility, MoneyVisibility>());

// ترجیحات کاربر جاری (حالت ساده/کامل، اندازهٔ نوشته، پیش‌نمایش «نمای ساده») برای layout و viewها
builder.Services.AddScoped<IUserPreferencesAccessor, UserPreferencesAccessor>();

// دسترسی viewها به تنظیمات کارگاه (واحد پول، لوگو) — با کش کوتاه در طول هر درخواست
builder.Services.AddScoped<RugsManagment.Web.Support.TenantSettingsAccessor>();
builder.Services.AddScoped<RugsManagment.Web.Support.IImageUploadHelper,
    RugsManagment.Web.Support.ImageUploadHelper>();

// درزهای پوسته: هر کلاسِ غیرانتزاعیِ این اسمبلی که آیتم منو، کارت خانه یا کاشی تنظیمات است خودکار ثبت می‌شود؛
// فازهای بعد فقط فایل اضافه می‌کنند و Program.cs را ویرایش نمی‌کنند.
HostConventions.AddWebSeams(builder.Services);

// ═══════════════════════════════════════════════════════════════════
// محدودیت نرخ — دفاع در برابر حدس رمز و کارهای سنگینِ پیاپی
// ═══════════════════════════════════════════════════════════════════
// فقط روی ورود و کارهای سنگین (اکسل، چاپ، ورود از اکسل) اعمال می‌شود؛ بقیهٔ اپ پشت احراز هویت است و
// محدودکردنش کار انباردار را که تند کار می‌کند مختل می‌کرد. نام سیاست‌ها: RateLimitPolicies.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Login, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // کلید بر اساس IP — پشت پراکسی، UseForwardedHeaders آی‌پی واقعی را می‌دهد
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    // هر کاربر سطل خودش را دارد (نه هر IP): چند کاربرِ یک کارگاه پشت یک اینترنت سهمیهٔ هم را نمی‌خورند.
    // ۱۰ درخواست پشت سر هم، بعد ۱۰ تا هر ۳۰ ثانیه.
    options.AddPolicy(RateLimitPolicies.Heavy, context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? context.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10,
                TokensPerPeriod = 10,
                ReplenishmentPeriod = TimeSpan.FromSeconds(30),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // پیام فارسی به‌جای صفحهٔ خالی ۴۲۹؛ برای API بدنهٔ JSON مثل بقیهٔ خطاها
    options.OnRejected = async (context, ct) =>
    {
        var http = context.HttpContext;
        var policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        var message = policy == RateLimitPolicies.Login
            ? "تلاش‌های زیاد برای ورود. لطفاً چند دقیقه صبر کنید و دوباره امتحان کنید."
            : "درخواست‌ها پشت سر هم زیاد شد؛ نیم دقیقه صبر کنید و دوباره بزنید.";

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        if (HostConventions.IsApiRequest(http.Request))
        {
            await http.Response.WriteAsJsonAsync(new { message }, ct);
            return;
        }

        http.Response.ContentType = "text/plain; charset=utf-8";
        await http.Response.WriteAsync(message, ct);
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

app.UseAuthentication();

// محدودیت نرخ باید بعد از Routing باشد تا سیاست هر endpoint شناخته شود، و بعد از احراز هویت تا سیاست
// «heavy» سطل هر کاربر را از روی خودِ کاربر بشناسد (سیاست ورود همچنان بر اساس IP است).
app.UseRateLimiter();

app.UseAuthorization();

// بعد از احراز هویت تا کاربر و کارگاه در scope لاگ بنشینند
app.UseRequestLogScope();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// بررسی سلامت برای CapRover و مانیتورینگ — بدون احراز هویت، بدون افشای جزئیات
app.MapHealthChecks("/health").AllowAnonymous();

// migration ← دادهٔ اولیه ← کارهای استارت، زیر قفل advisory پستگرس (StartupPipeline).
// پیش از app.Run تا هیچ درخواستی به schemaِ کهنه نرسد.
await MigrateAndSeedWithRetryAsync(app);

app.Run();

// ─────────────────────────────────────────────────────────────
// تلاش دوباره فقط برای خطای گذرا (دیتابیسی که هنگام استارت کانتینر هنوز بالا نیامده): ۱۰ بار، هر ۳ ثانیه.
// StartupAbortException (migration پشتیبان‌خواه بدون تأیید، مدل بدون migration، تنظیم نامعتبر) با
// تلاش دوباره درست نمی‌شود؛ همان بار اول Critical لاگ و برنامه متوقف می‌شود.
static async Task MigrateAndSeedWithRetryAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var stopping = app.Lifetime.ApplicationStopping;
    const int maxAttempts = 10;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await StartupPipeline.RunAsync(app.Services, logger, stopping);
            return;
        }
        catch (StartupAbortException ex)
        {
            logger.LogCritical(ex, "استارت متوقف شد: {Reason}", ex.Message);
            throw;
        }
        catch (Exception ex) when (attempt < maxAttempts && !stopping.IsCancellationRequested)
        {
            logger.LogWarning(ex, "آماده‌سازی دیتابیس ناموفق (تلاش {Attempt}/{Max}). تلاش دوباره…", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(3), stopping);
        }
    }
}

/// <summary>قراردادهای کوچک میزبان که چند جای Program.cs به آن‌ها نیاز دارد.</summary>
file static class HostConventions
{
    /// <summary>پاسخ ۴۰۱ برای API: کوکی نیست یا باطل شده (مثلاً نقش عوض شد).</summary>
    public const string SessionExpiredMessage = "نشست شما تمام شده؛ صفحه را تازه کنید و دوباره وارد شوید.";

    /// <summary>پیام زیر خانه‌ای که مقدارش قابل خواندن نبود (عدد نامعتبر، گزینهٔ ناشناخته).</summary>
    public const string InvalidFieldMessage = "مقدار این خانه درست نیست.";

    /// <summary>درخواست API (JSON) است، نه صفحه: هر مسیری زیر <c>/api</c>.</summary>
    public static bool IsApiRequest(HttpRequest request)
        => request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// کلید ModelState به نام فیلد JSON (camelCase): «$.textScale» → «textScale»، «TextScale» → «textScale».
    /// کلید خالی یا «$» (کل بدنه) «body» می‌شود.
    /// </summary>
    public static string ApiFieldKey(string key)
    {
        var name = key.TrimStart('$').TrimStart('.');
        return name.Length == 0 ? "body" : char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>پیام خطا فارسی است (پیام DataAnnotationsِ خودمان) و می‌شود همان را به کاربر نشان داد.</summary>
    public static bool IsPersian(string? message)
        => !string.IsNullOrWhiteSpace(message) && message.Any(c => c is >= '؀' and <= 'ۿ');

    /// <summary>
    /// ثبت خودکار درزهای پوسته از اسمبلی وب: هر کلاس غیرانتزاعی که <see cref="INavContributor"/>،
    /// <see cref="IHomeCard"/> یا <see cref="ISettingsLink"/> است، به‌صورت scoped. TryAddEnumerable جلوی ثبت
    /// تکراری را می‌گیرد اگر جای دیگری هم دستی ثبت شده باشد.
    /// </summary>
    public static void AddWebSeams(IServiceCollection services)
    {
        Type[] seams = [typeof(INavContributor), typeof(IHomeCard), typeof(ISettingsLink)];

        var candidates = typeof(HostConventions).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var type in candidates)
        {
            foreach (var seam in seams)
            {
                if (seam.IsAssignableFrom(type))
                    services.TryAddEnumerable(ServiceDescriptor.Scoped(seam, type));
            }
        }
    }
}
