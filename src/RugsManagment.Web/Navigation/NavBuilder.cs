using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Support;

namespace RugsManagment.Web.Navigation;

/// <summary>
/// منوی آمادهٔ یک درخواست: آیتم‌های نوار کناری، نوار پایین گوشی، صفحهٔ «بیشتر» و فرمان‌های Ctrl+K.
/// </summary>
/// <param name="Context">بیننده</param>
/// <param name="Sidebar">نوار کناری (دسکتاپ) و کشوی منو (گوشی)، به ترتیب</param>
/// <param name="Bottom">نوار پایین گوشی؛ صریح و حداکثر پنج تا</param>
/// <param name="More">فهرست صفحهٔ «بیشتر»</param>
/// <param name="Commands">فرمان‌های جعبهٔ جست‌وجو (<c>commands</c> در app-context)</param>
public sealed record NavModel(
    NavContext Context,
    IReadOnlyList<NavItem> Sidebar,
    IReadOnlyList<NavItem> Bottom,
    IReadOnlyList<NavItem> More,
    IReadOnlyList<NavItem> Commands)
{
    /// <summary>منوی خالی برای کاربر واردنشده.</summary>
    public static NavModel Empty(NavContext context) => new(context, [], [], [], []);

    /// <summary>آیا این آیتم صفحهٔ جاری است (برای <c>aria-current</c> و رنگ فعال).</summary>
    public bool IsActive(NavItem item) => NavBuilder.IsActive(item, Context.CurrentPath);
}

/// <summary>
/// منو را از همهٔ <see cref="INavContributor"/>های ثبت‌شده می‌سازد (جدول ناوبری A.4).
///
/// چرا یک سازنده و نه آرایهٔ ثابت در layout: منوی قدیمی برای هر نقش یک تاپل جدا داشت و نوار پایین گوشی
/// «پنج تای اول» همان بود؛ هر صفحهٔ تازه layout را ویرایش می‌کرد و نوار پایین بی‌صدا عوض می‌شد. حالا هر
/// ماژول سهم خودش را می‌دهد، این کلاس یکی و مرتب می‌کند، و جای هر آیتم (کناری، پایین، «بیشتر»، Ctrl+K)
/// صریحاً در خود آیتم نوشته شده است.
///
/// منو در هر صفحه ساخته می‌شود، پس هیچ کوئری دیتابیسی ندارد؛ نتیجه در طول درخواست یک بار ساخته و
/// کش می‌شود (layout، صفحهٔ «بیشتر» و app-context همه همان را می‌خوانند). contributorی که خطا بدهد
/// فقط آیتم‌های خودش را از دست می‌دهد، نه کل صفحه را.
/// </summary>
public sealed class NavBuilder(
    IEnumerable<INavContributor> contributors,
    IUserPreferencesAccessor preferences,
    IHttpContextAccessor httpContextAccessor,
    ILogger<NavBuilder> logger)
{
    /// <summary>حداکثر آیتم نوار پایین گوشی (راهنمای UX: پنج).</summary>
    public const int MaxBottomItems = 5;

    /// <summary>کلید آیتم «بیشتر» — اگر نوار پایین شلوغ شد، این یکی همیشه می‌ماند.</summary>
    public const string MoreKey = "more";

    private const string CacheKey = "rugs.nav-model";

    private NavModel? _model;

    /// <summary>منوی درخواست جاری (یک بار ساخته می‌شود).</summary>
    public NavModel Build()
    {
        if (_model is not null) return _model;

        var http = httpContextAccessor.HttpContext;
        if (http?.User.Identity?.IsAuthenticated != true)
            return _model = NavModel.Empty(AnonymousContext(http?.Request.Path.Value));

        var context = CreateContext(http, preferences);
        return _model = Compose(contributors, context, logger);
    }

    /// <summary>
    /// منوی درخواست جاری از روی HttpContext، برای viewها. اگر <see cref="NavBuilder"/> در DI ثبت نشده باشد
    /// خودش را با همان وابستگی‌ها می‌سازد؛ نتیجه در <c>HttpContext.Items</c> می‌ماند تا layout و صفحهٔ
    /// «بیشتر» یک منو را دو بار نسازند.
    /// </summary>
    public static NavModel For(HttpContext http)
    {
        if (http.Items.TryGetValue(CacheKey, out var cached) && cached is NavModel model)
            return model;

        var builder = ActivatorUtilities.GetServiceOrCreateInstance<NavBuilder>(http.RequestServices);
        model = builder.Build();
        http.Items[CacheKey] = model;
        return model;
    }

    /// <summary>بینندهٔ منو از کاربر و ترجیحات درخواست جاری.</summary>
    public static NavContext CreateContext(HttpContext http, IUserPreferencesAccessor preferences)
    {
        var user = http.User;
        var role = Enum.TryParse<UserRole>(user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, out var parsed)
            ? parsed
            : UserRole.Operator; // نقش ناخوانا = کم‌ترین دسترسی؛ سیاست‌ها از خودِ claim خوانده می‌شوند

        return new NavContext(
            Role: role,
            CanFloor: Policies.IsFloor(user),
            CanBusiness: Policies.IsBusiness(user),
            CanConfigure: Policies.IsConfigure(user),
            IsSimple: preferences.IsSimple,
            PreviewSimple: preferences.PreviewSimple,
            CurrentPath: http.Request.Path.Value ?? "/");
    }

    /// <summary>
    /// قلب کار، بدون HttpContext (قابل آزمون): جمع، یکی کردن بر اساس کلید (کمترین Order می‌ماند)، مرتب
    /// کردن و پخش در جایگاه‌ها.
    /// </summary>
    public static NavModel Compose(IEnumerable<INavContributor> contributors, NavContext context, ILogger? logger = null)
    {
        var byKey = new Dictionary<string, NavItem>(StringComparer.Ordinal);

        foreach (var contributor in contributors)
        {
            IEnumerable<NavItem> items;
            try
            {
                items = contributor.GetItems(context).ToList();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "سهم منوی {Contributor} ساخته نشد؛ بقیهٔ منو نشان داده می‌شود.",
                    contributor.GetType().Name);
                continue;
            }

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || item.Slots == NavSlot.None) continue;
                if (byKey.TryGetValue(item.Key, out var existing) && existing.Order <= item.Order) continue;
                byKey[item.Key] = item;
            }
        }

        var ordered = byKey.Values
            .OrderBy(i => i.Order)
            .ThenBy(i => i.Key, StringComparer.Ordinal)
            .ToList();

        return new NavModel(
            context,
            Sidebar: ordered.Where(i => i.Slots.HasFlag(NavSlot.Sidebar)).ToList(),
            Bottom: LimitBottom(ordered.Where(i => i.Slots.HasFlag(NavSlot.Bottom)).ToList(), logger),
            More: ordered.Where(i => i.Slots.HasFlag(NavSlot.More)).ToList(),
            Commands: DistinctCommands(ordered.Where(i => i.Slots.HasFlag(NavSlot.Command))));
    }

    /// <summary>
    /// یک صفحه با یک برچسب یک بار در Ctrl+K (مثلاً «برچسب‌ها» هم آیتم منوست هم کاشی تنظیمات)؛ همان صفحه با
    /// برچسب دیگر («قالب‌های کارهای کارگاه» در کنار «کارهای کارگاه») می‌ماند تا با هر دو واژه پیدا شود.
    /// </summary>
    private static List<NavItem> DistinctCommands(IEnumerable<NavItem> items)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return items.Where(i => seen.Add($"{StripQuery(i.Href).TrimEnd('/')}\n{i.Label}")).ToList();
    }

    /// <summary>
    /// آیا مسیر جاری زیر این آیتم است. <see cref="NavItem.ActivePrefix"/> می‌تواند چند پیشوند جداشده با
    /// «|» باشد (مثلاً تنظیمات: «/Settings|/Users»)؛ پیشوند «/» فقط خودِ ریشه را می‌گیرد، نه همه‌چیز را.
    /// بی‌حساسیت به بزرگی حروف، چون مسیرهای قدیمی هم «/Rugs» دارند هم «/rugs».
    /// </summary>
    public static bool IsActive(NavItem item, string? currentPath)
    {
        var path = string.IsNullOrEmpty(currentPath) ? "/" : currentPath;
        var prefixes = (item.ActivePrefix ?? item.Href).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var raw in prefixes)
        {
            var prefix = StripQuery(raw);
            if (prefix == "/")
            {
                if (path == "/") return true;
                continue;
            }

            prefix = prefix.TrimEnd('/');
            if (path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// آدرس قابل نوشتن در <c>href</c>: آدرس‌های «/…» با PathBase برنامه (اگر پشت یک زیرمسیر اجرا شود)
    /// پیشوند می‌گیرند؛ بقیه دست‌نخورده.
    /// </summary>
    public static string Resolve(HttpContext http, string href)
        => href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal)
            ? http.Request.PathBase.Add(new PathString(StripQuery(href))).Value + QueryOf(href)
            : href;

    private static IReadOnlyList<NavItem> LimitBottom(List<NavItem> items, ILogger? logger)
    {
        if (items.Count <= MaxBottomItems) return items;

        logger?.LogWarning("نوار پایین {Count} آیتم دارد؛ فقط {Max} تا نشان داده می‌شود.", items.Count, MaxBottomItems);
        var more = items.FirstOrDefault(i => i.Key == MoreKey);
        if (more is null) return items.Take(MaxBottomItems).ToList();
        return items.Where(i => i.Key != MoreKey).Take(MaxBottomItems - 1).Append(more).ToList();
    }

    private static NavContext AnonymousContext(string? path)
        => new(UserRole.Operator, false, false, false, false, false, path ?? "/");

    private static string StripQuery(string href)
    {
        var cut = href.IndexOfAny(['?', '#']);
        return cut < 0 ? href : href[..cut];
    }

    private static string QueryOf(string href)
    {
        var cut = href.IndexOfAny(['?', '#']);
        return cut < 0 ? string.Empty : href[cut..];
    }
}
