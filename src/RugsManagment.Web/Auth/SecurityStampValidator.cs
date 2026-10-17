using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Web.Auth;

/// <summary>
/// اعتبارسنجی دوبارهٔ کوکی ورود در هر درخواست (CookieAuthenticationEvents.OnValidatePrincipal).
///
/// چرا لازم است: نقش و کارگاه کاربر داخل کوکی است و کوکی تا ۸ ساعت (با تمدید لغزان) معتبر می‌ماند.
/// بدون این بررسی، کاربرِ غیرفعال‌شده، مدیرِ تنزل‌یافته، کارگاهِ معلق یا اشتراکِ تمام‌شده
/// تا پایان عمر کوکی همچنان دسترسی داشت، و تغییر رمز هم نشست‌های دیگر را نمی‌بست.
///
/// برای اینکه هر درخواست به دیتابیس نخورد، وضعیت هر کاربر ۵ دقیقه در IMemoryCache می‌ماند.
/// چرخش مهر در همین پروسه با <see cref="Evict(IMemoryCache, Guid)"/> کش را فوراً پاک می‌کند؛ روی نمونه‌های دیگرِ
/// اپ (اگر چند نمونه اجرا شود) بدترین حالت همان ۵ دقیقه است.
///
/// کش فقط برای «پذیرفتن» به آن اعتماد می‌شود: اگر وضعیتِ کش‌شده رد کند، پیش از رد کردن یک بار از دیتابیس
/// خوانده می‌شود (کهنگیِ کش نباید کاربرِ معتبر را بیرون بیندازد). ورود موفق و فعال/غیرفعال کردن کارگاه هم
/// کش کاربرانِ مربوط را پاک می‌کنند.
/// </summary>
public static class SecurityStampValidator
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>آنچه برای تصمیم لازم است — نه کل موجودیت، تا کش سبک بماند و چیزی tracked نگه ندارد.</summary>
    private sealed record UserSecurityState(
        bool IsActive,
        string Role,
        string SecurityStamp,
        Guid? TenantId,
        bool TenantIsActive,
        DateTimeOffset? SubscriptionExpiresAt);

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
            return;

        var stamp = principal.FindFirstValue(ClaimsPrincipalExtensions.SecurityStampClaim);
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || string.IsNullOrEmpty(stamp))
        {
            // کوکی‌های صادرشده پیش از وجود مهر هم اینجا رد می‌شوند: یک‌بار ورود دوباره، نه حفرهٔ دائمی
            await RejectAsync(context);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<IMemoryCache>();
        var ct = context.HttpContext.RequestAborted;

        if (cache.TryGetValue(CacheKey(userId), out UserSecurityState? cached) && cached is not null)
        {
            if (IsStillValid(principal, stamp, cached))
                return;

            // وضعیتِ کش‌شده رد می‌کند، ولی ممکن است کهنه باشد: کارگاه دوباره فعال شده، اشتراک تمدید شده، یا
            // مهر روی نمونهٔ دیگری چرخیده و این کوکیِ تازه همان مهر جدید را دارد. پیش از بیرون انداختنِ کاربر
            // یک بار (بدون کش) از دیتابیس خوانده می‌شود و تصمیم با وضعیتِ تازه گرفته می‌شود. بدون این، کاربرِ
            // معتبر تا ۵ دقیقه با هر ورود دوباره بی‌پیام به صفحهٔ ورود برمی‌گشت.
        }

        var fresh = await LoadAsync(services, userId, ct);

        // کاربر حذف‌شده کش نمی‌شود؛ همین درخواست کوکی‌اش را پاک می‌کند و دیگر برنمی‌گردد
        if (fresh is null)
        {
            cache.Remove(CacheKey(userId));
            await RejectAsync(context);
            return;
        }

        cache.Set(CacheKey(userId), fresh, CacheDuration);

        if (!IsStillValid(principal, stamp, fresh))
            await RejectAsync(context);
    }

    /// <summary>
    /// پاک کردن وضعیت کش‌شدهٔ یک کاربر — بعد از هر چرخش مهر در همین پروسه صدا زده شود
    /// تا نشست‌های قدیمی همان درخواست بعدی رد شوند، نه ۵ دقیقه بعد.
    /// </summary>
    public static void Evict(IMemoryCache cache, Guid userId) => cache.Remove(CacheKey(userId));

    /// <summary>
    /// پاک کردن وضعیت کش‌شدهٔ چند کاربر — مثلاً همهٔ کاربرانِ کارگاهی که ادمین سیستم فعال/غیرفعالش کرد،
    /// تا غیرفعال‌سازی همان درخواست بعدی اثر کند و فعال‌سازی هم کسی را بی‌دلیل بیرون نیندازد.
    /// </summary>
    public static void Evict(IMemoryCache cache, IEnumerable<Guid> userIds)
    {
        foreach (var userId in userIds)
            Evict(cache, userId);
    }

    private static string CacheKey(Guid userId) => $"sstamp:{userId}";

    private static bool IsStillValid(ClaimsPrincipal principal, string stamp, UserSecurityState state)
    {
        if (!string.Equals(stamp, state.SecurityStamp, StringComparison.Ordinal))
            return false;

        if (!state.IsActive)
            return false;

        if (!string.Equals(principal.FindFirstValue(ClaimTypes.Role), state.Role, StringComparison.Ordinal))
            return false;

        // کارگاه داخل کوکی باید همان کارگاه فعلی کاربر باشد
        if (principal.GetTenantId() != state.TenantId)
            return false;

        // ادمین سیستم کارگاه ندارد و بررسی‌های کارگاه/اشتراک برایش معنا ندارد
        if (state.TenantId is null)
            return true;

        if (!state.TenantIsActive)
            return false;

        // همان قاعدهٔ صفحهٔ ورود: سررسید گذشته یعنی دسترسی تمام
        return state.SubscriptionExpiresAt is not { } expiry || expiry > DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// خواندن بدون ردیابی (ListAsync = AsNoTracking): این کد قبل از هر کنترلری اجرا می‌شود و
    /// User/Tenantِ tracked در DbContextِ همان درخواست ممکن بود در SaveChangesِ یک عملیات
    /// بی‌ربط هم کشیده شود.
    /// </summary>
    private static async Task<UserSecurityState?> LoadAsync(IServiceProvider services, Guid userId, CancellationToken ct)
    {
        var users = services.GetRequiredService<IUserRepository>();
        var user = (await users.ListAsync(u => u.Id == userId, ct)).FirstOrDefault();
        if (user is null)
            return null;

        Tenant? tenant = null;
        if (user.TenantId is Guid tenantId)
        {
            var tenants = services.GetRequiredService<ITenantRepository>();
            tenant = (await tenants.ListAsync(t => t.Id == tenantId, ct)).FirstOrDefault();
        }

        return new UserSecurityState(
            user.IsActive,
            user.Role.ToString(),
            user.SecurityStamp,
            user.TenantId,
            // کاربر کارگاه بدون کارگاهِ قابل‌بارگذاری = نامعتبر؛ ادمین سیستم کارگاه ندارد
            tenant?.IsActive ?? user.TenantId is null,
            tenant?.SubscriptionExpiresAt);
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
