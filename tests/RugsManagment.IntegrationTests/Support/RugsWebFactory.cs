using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Web.Auth;

namespace RugsManagment.IntegrationTests.Support;

/// <summary>
/// میزبان واقعیِ Web (همان Program.cs با همهٔ میان‌افزارها، فیلترها و قوانین دسترسی) روی یک دیتابیس
/// دورریختنی <c>rugs_it_xxxxxxxx</c>. خودِ برنامه هنگام استارت دیتابیس را می‌سازد، migration می‌زند و
/// دادهٔ اولیه (کارگاه دمو، ادمین سیستم، …) را می‌کارد — دقیقاً مثل یک سرور خالی.
///
/// دو راه ورود برای تست‌ها:
/// <list type="bullet">
/// <item>کوکی واقعی (<see cref="NewSession"/> + <see cref="WebSession.LoginAsync"/>) — از صفحهٔ ورود،
/// با SecurityStampValidator؛ برای تست‌های خودِ نشست و مهر امنیتی.</item>
/// <item>سربرگ <see cref="TestAuthHandler.HeaderName"/> (<see cref="CreateClientAs"/>) — هویتِ دلخواه بدون
/// رفتن به صفحهٔ ورود؛ برای ماتریس «نقش × مسیر» فازهای بعد، چون محدودیت نرخ ورود (۱۰ تلاش در ۵ دقیقه)
/// چند ده ورود پشت سر هم را رد می‌کرد.</item>
/// </list>
///
/// ایمنی: رشتهٔ اتصال در appsettings.json به دیتابیس واقعیِ صاحب پروژه اشاره می‌کند. برای اینکه
/// هیچ مسیری به آن نرسد، هم تنظیم ConnectionStrings بازنویسی می‌شود و هم ثبتِ DbContext در DI به‌کلی
/// با نسخهٔ دیتابیس دورریختنی جایگزین می‌شود؛ بعد از استارت هم نام دیتابیسِ واقعاً استفاده‌شده بررسی می‌شود.
/// </summary>
public sealed class RugsWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>طرح احراز هویتِ انتخاب‌گر: با سربرگ تست → <see cref="TestAuthHandler"/>، وگرنه کوکی واقعی.</summary>
    private const string SelectorScheme = "IntegrationTestSelector";

    private readonly NonExistentTestDatabase _database = new();
    private readonly string _scratchDirectory =
        Path.Combine(Path.GetTempPath(), "rugs-it-web", Guid.NewGuid().ToString("N")[..8]);

    public string DatabaseName => _database.DatabaseName;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        if (PostgresTestEnvironment.ShouldSkip)
            return; // تست‌ها با PostgresFact رد می‌شوند

        // استارت همین‌جا: migration و دادهٔ اولیه یک بار برای کل کلاس، و خطای استارت به‌جای اولین تست
        // همین‌جا دیده می‌شود.
        _ = Server;

        await using var scope = Services.CreateAsyncScope();
        var used = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString();
        var usedDatabase = new NpgsqlConnectionStringBuilder(used).Database;
        if (!string.Equals(usedDatabase, DatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"میزبان تست به دیتابیس «{usedDatabase}» وصل شد، نه دیتابیس دورریختنی «{DatabaseName}».");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();

        try
        {
            if (Directory.Exists(_scratchDirectory))
                Directory.Delete(_scratchDirectory, recursive: true);
        }
        catch (IOException)
        {
            // پوشهٔ موقت سیستم است؛ ماندنش ضرری ندارد
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = _database.ConnectionString;
        if (connectionString.Length == 0)
            throw new InvalidOperationException("RugsWebFactory پیش از InitializeAsync (یا بدون RUGS_TEST_PG) ساخته شد.");

        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("DataProtection:KeyPath", Path.Combine(_scratchDirectory, "keys"));
        builder.UseSetting("Storage:ImagePath", Path.Combine(_scratchDirectory, "images"));

        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        builder.ConfigureTestServices(services =>
        {
            // DbContext از نو: هر پیکربندیِ قبلی (از جمله UseNpgsql با رشتهٔ appsettings) حذف می‌شود
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { })
                .AddPolicyScheme(SelectorScheme, SelectorScheme, options =>
                    options.ForwardDefaultSelector = context =>
                        context.Request.Headers.ContainsKey(TestAuthHandler.HeaderName)
                            ? TestAuthHandler.SchemeName
                            : CookieAuthenticationDefaults.AuthenticationScheme);

            // بعد از AddAuthentication(Cookies)ِ Program ثبت می‌شود، پس همین برنده است
            services.Configure<AuthenticationOptions>(options => options.DefaultScheme = SelectorScheme);
        });
    }

    // ── کلاینت‌ها ─────────────────────────────────────────────────

    /// <summary>کلاینت خام: بدون دنبال کردن ریدایرکت و بدون کوکی خودکار (کوکی‌ها دستِ خود تست است).</summary>
    public HttpClient CreateRawClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = false
    });

    /// <summary>یک «مرورگر» با ظرف کوکی خودش — برای ورود واقعی با کوکی.</summary>
    public WebSession NewSession() => new(CreateRawClient());

    /// <summary>کلاینتی که هر درخواستش با هویتِ داده‌شده (بدون صفحهٔ ورود) احراز می‌شود.</summary>
    public HttpClient CreateClientAs(TestPrincipal principal)
    {
        var client = CreateRawClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderName, principal.ToHeaderValue());
        return client;
    }

    // ── دیتابیس ───────────────────────────────────────────────────

    /// <summary>DbContext مستقل روی همان دیتابیس دورریختنی، بیرون از DIِ برنامه.</summary>
    public AppDbContext CreateDbContext() => _database.CreateContext();

    /// <summary>کارگاه دموی دادهٔ اولیه و مدیرش.</summary>
    public async Task<(Guid TenantId, Guid AdminUserId)> GetDemoTenantAsync()
    {
        await using var db = CreateDbContext();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Slug == "demo-weaver");
        var admin = await db.Users.AsNoTracking()
            .FirstAsync(u => u.TenantId == tenant.Id && u.Role == UserRole.TenantAdmin);
        return (tenant.Id, admin.Id);
    }

    /// <summary>ادمین سیستمِ دادهٔ اولیه.</summary>
    public async Task<Guid> GetSystemAdminIdAsync()
    {
        await using var db = CreateDbContext();
        return await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.SystemAdmin)
            .Select(u => u.Id)
            .FirstAsync();
    }

    /// <summary>کارگاه تازه و فعال — برای تستی که وضعیت کارگاه را عوض می‌کند و نباید روی بقیه اثر بگذارد.</summary>
    public async Task<Guid> CreateTenantAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var tenant = new Tenant { Name = $"IT web tenant {suffix}", Slug = $"it-web-{suffix}", IsActive = true };

        await using var db = CreateDbContext();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    /// <summary>کاربر تازه با رمز معلوم، مستقیم در دیتابیس (نه از مسیر ثبت‌نام).</summary>
    public async Task<SeededUser> CreateUserAsync(Guid tenantId, UserRole role, string password = "it-Web-Passw0rd")
    {
        var user = new User
        {
            TenantId = tenantId,
            Email = $"it-web-{Guid.NewGuid():N}@it.test",
            FullName = $"IT {role}",
            Role = role,
            PasswordHash = AuthService.HashPassword(password),
            IsActive = true
        };

        await using var db = CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new SeededUser(user.Id, tenantId, user.Email, password, role);
    }

    /// <summary>مهر امنیتیِ ذخیره‌شده در دیتابیس.</summary>
    public async Task<string> GetStampAsync(Guid userId)
    {
        await using var db = CreateDbContext();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.SecurityStamp).SingleAsync();
    }

    /// <summary>
    /// چرخش مهر «پشت سرِ برنامه»: همان اثری که بازنشانی رمز روی نمونهٔ دیگری از اپ دارد —
    /// دیتابیس عوض می‌شود ولی کش همین پروسه پاک نمی‌شود.
    /// </summary>
    public async Task<string> RotateStampBehindTheAppsBackAsync(Guid userId)
    {
        var stamp = Guid.NewGuid().ToString("N");
        await using var db = CreateDbContext();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, stamp));
        return stamp;
    }

    /// <summary>فعال/غیرفعال کردن کارگاه مستقیم در دیتابیس (بدون پاک شدن کش برنامه).</summary>
    public async Task SetTenantActiveBehindTheAppsBackAsync(Guid tenantId, bool isActive)
    {
        await using var db = CreateDbContext();
        await db.Tenants.Where(t => t.Id == tenantId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, isActive));
    }

    /// <summary>شبیه‌سازی تمام شدن عمر کش اعتبارسنجی (۵ دقیقه) برای یک کاربر.</summary>
    public void ExpireCachedSecurityState(Guid userId)
        => SecurityStampValidator.Evict(Services.GetRequiredService<IMemoryCache>(), userId);
}

/// <summary>کاربری که تست ساخته، با رمز معلوم برای ورود واقعی.</summary>
public sealed record SeededUser(Guid Id, Guid TenantId, string Email, string Password, UserRole Role);

/// <summary>
/// هویتِ دلخواه برای <see cref="TestAuthHandler"/>. در سربرگ به‌صورت base64ِ JSON می‌رود تا نام فارسی
/// هم (که در سربرگ HTTP مجاز نیست) سالم برسد.
/// </summary>
public sealed record TestPrincipal(
    Guid UserId,
    UserRole Role,
    Guid? TenantId = null,
    string? SecurityStamp = null,
    string Name = "IT user")
{
    public static TestPrincipal SystemAdmin(Guid userId) => new(userId, UserRole.SystemAdmin);
    public static TestPrincipal TenantAdmin(Guid userId, Guid tenantId) => new(userId, UserRole.TenantAdmin, tenantId);
    public static TestPrincipal Operator(Guid userId, Guid tenantId) => new(userId, UserRole.Operator, tenantId);

    public string ToHeaderValue() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this));

    public static TestPrincipal Parse(string headerValue)
        => JsonSerializer.Deserialize<TestPrincipal>(Convert.FromBase64String(headerValue))
           ?? throw new InvalidOperationException("سربرگ هویت تست خالی است.");
}

/// <summary>
/// احراز هویت تست: claimهای همان کوکیِ واقعی (NameIdentifier / Name / Role / tenant_id / sstamp) را از
/// سربرگ <see cref="HeaderName"/> می‌سازد. SecurityStampValidator روی این طرح اجرا نمی‌شود (رویداد کوکی است)؛
/// پس این مسیر فقط برای سنجیدن قوانین دسترسی است، نه خودِ نشست.
/// بدون سربرگ NoResult برمی‌گرداند؛ challenge = ۴۰۱ و forbid = ۴۰۳ (پیش‌فرض AuthenticationHandler).
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationTest";
    public const string HeaderName = "X-Test-Principal";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var raw = Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return Task.FromResult(AuthenticateResult.NoResult());

        TestPrincipal principal;
        try
        {
            principal = TestPrincipal.Parse(raw);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return Task.FromResult(AuthenticateResult.Fail("سربرگ هویت تست نامعتبر است."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, principal.UserId.ToString()),
            new(ClaimTypes.Name, principal.Name),
            new(ClaimTypes.Role, principal.Role.ToString()),
            new(ClaimsPrincipalExtensions.SecurityStampClaim, principal.SecurityStamp ?? "integration-test")
        };
        if (principal.TenantId is Guid tenantId)
            claims.Add(new Claim(ClaimsPrincipalExtensions.TenantIdClaim, tenantId.ToString()));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
