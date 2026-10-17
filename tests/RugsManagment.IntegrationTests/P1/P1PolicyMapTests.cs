using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Controllers.Api;

namespace RugsManagment.IntegrationTests.P1;

/// <summary>
/// نقشهٔ دسترسیِ همهٔ درهای وب، از روی attributeها (بدون دیتابیس) — مکملِ ماتریس HTTP در
/// <see cref="P1AuthMatrixTests"/>.
///
/// دو لایه:
/// <list type="bullet">
/// <item>قاعده‌های کلی برای هر اکشنِ هر کنترلر، از جمله کنترلرهای فازهای بعد: هیچ دری بی‌سیاست نیست؛
/// <c>Roles = …</c> فقط برای پنل مدیر سامانه؛ هر API از <see cref="ApiControllerBase"/> (پیش‌فرض Floor)؛
/// هر <c>/api/settings/*</c> و هر کنترلر <c>Controllers/Settings</c> فقط Configure؛ هر POST صفحه توکن CSRF.</item>
/// <item>جدول J.2 برای درهایی که تا پایان فاز ۱ هستند. فاز تازه برای کنترلر تازه‌اش این‌جا چیزی اضافه
/// نمی‌کند — قاعده‌های کلی آن را می‌پوشانند.</item>
/// </list>
/// چند <c>[Authorize]</c> روی هم «و» می‌شوند، پس سیاستِ مؤثر سخت‌گیرترینِ آن‌هاست
/// (Configure ⊂ Business ⊂ Floor).
/// </summary>
public sealed class P1PolicyMapTests
{
    /// <summary>دری که اکشن پشتش است.</summary>
    public enum Gate
    {
        /// <summary>هیچ <c>[Authorize]</c>ی نیست — همیشه خطا</summary>
        None,
        Anonymous,
        /// <summary><c>[Authorize]</c> بدون سیاست: هر کاربر واردشده</summary>
        Authenticated,
        Floor,
        Business,
        Configure,
        SystemAdmin,
        /// <summary>ترکیبی که این آزمون نمی‌شناسد (مثلاً <c>Roles</c> با نقش‌های کارگاه)</summary>
        Unknown
    }

    private static readonly Assembly WebAssembly = typeof(Policies).Assembly;

    // ── قاعده‌های کلی ───────────────────────────────────────────────────────

    /// <summary>درهای عمومی (بدون ورود): ورود، «دسترسی مجاز نیست» و صفحه‌های خطا.</summary>
    private static readonly HashSet<string> AnonymousAllowed = new(StringComparer.Ordinal)
    {
        "Account.Login", "Account.Denied", "Home.Error", "Home.StatusCodeError"
    };

    /// <summary>
    /// درهایی که فقط «واردشده» می‌خواهند: خانه (مدیر سامانه از آن‌جا به <c>/Admin</c> می‌رود) و حساب خودِ کاربر.
    /// </summary>
    private static readonly HashSet<string> AuthenticatedAllowed = new(StringComparer.Ordinal)
    {
        "Home.Index", "Account.Logout", "Account.Profile"
    };

    [Fact]
    public void EveryAction_HasAKnownGate_AndOnlyTheAllowListIsOpen()
    {
        var problems = new List<string>();

        foreach (var (controller, action) in Actions())
        {
            var key = Key(controller, action);
            var gate = GateOf(controller, action);

            var ok = gate switch
            {
                Gate.Floor or Gate.Business or Gate.Configure => true,
                Gate.Anonymous => AnonymousAllowed.Contains(key),
                Gate.Authenticated => AuthenticatedAllowed.Contains(key),
                Gate.SystemAdmin => controller.Name == "AdminController",
                _ => false
            };

            if (!ok)
                problems.Add($"{key}: {gate}");
        }

        Assert.True(problems.Count == 0, "درهای بی‌سیاست یا ناشناخته: " + string.Join("، ", problems));
    }

    [Fact]
    public void ApiControllers_DeriveFromApiControllerBase()
    {
        var offenders = Controllers()
            .Where(t => !typeof(Controller).IsAssignableFrom(t) && !typeof(ApiControllerBase).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "کنترلر API بیرون از ApiControllerBase (بدون Floor پیش‌فرض و نگاشت خطا): " + string.Join("، ", offenders));
    }

    [Fact]
    public void SettingsRoutes_RequireConfigure()
    {
        var problems = new List<string>();

        foreach (var (controller, action) in Actions())
        {
            var template = RouteTemplate(controller);
            var isSettingsApi = template is not null
                                && template.StartsWith("api/settings", StringComparison.OrdinalIgnoreCase);
            var isSettingsPage = controller.Namespace == "RugsManagment.Web.Controllers.Settings"
                                 || controller.Name == "SettingsController";

            if ((isSettingsApi || isSettingsPage) && GateOf(controller, action) != Gate.Configure)
                problems.Add($"{Key(controller, action)}: {GateOf(controller, action)}");
        }

        Assert.True(problems.Count == 0, "در تنظیمات بدون Configure: " + string.Join("، ", problems));
    }

    [Fact]
    public void PagePosts_ValidateTheAntiforgeryToken()
    {
        var problems = new List<string>();

        foreach (var (controller, action) in Actions())
        {
            if (!typeof(Controller).IsAssignableFrom(controller))
                continue; // APIها [AutoValidateAntiforgeryToken] را از ApiControllerBase دارند

            var isPost = action.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                .Any(a => a.HttpMethods.Contains("POST", StringComparer.OrdinalIgnoreCase));
            if (!isPost)
                continue;

            var validated = action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true) is not null
                            || controller.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>(inherit: true) is not null
                            || controller.GetCustomAttribute<AutoValidateAntiforgeryTokenAttribute>(inherit: true) is not null;
            if (!validated)
                problems.Add(Key(controller, action));
        }

        Assert.True(problems.Count == 0, "POST صفحه بدون توکن CSRF: " + string.Join("، ", problems));
    }

    // ── جدول J.2 (فاز ۱) ─────────────────────────────────────────────────────

    /// <summary>
    /// (کنترلر بدون پسوند، اکشن یا null برای همهٔ اکشن‌ها، در). اکشنِ نام‌برده باید واقعاً وجود داشته باشد تا
    /// جدول کهنه نماند.
    /// </summary>
    public static TheoryData<string, string?, Gate> J2Sites => new()
    {
        // Floor: کار روزانهٔ انبار
        { "Dashboard", null, Gate.Floor },
        { "Groups", null, Gate.Floor },
        { "Media", null, Gate.Floor },
        { "Help", null, Gate.Floor },
        { "More", null, Gate.Floor },
        { "Labels", "Index", Gate.Floor },
        { "Labels", "Print", Gate.Floor },
        { "Workflows", "Index", Gate.Floor },
        { "Rugs", "Index", Gate.Floor },
        { "Rugs", "Details", Gate.Floor },
        { "Rugs", "Create", Gate.Floor },
        { "Rugs", "Edit", Gate.Floor },
        { "RugsApi", null, Gate.Floor },
        { "RugImagesApi", null, Gate.Floor },
        { "BatchesApi", null, Gate.Floor },
        { "LookupsApi", null, Gate.Floor },
        { "MeApi", null, Gate.Floor },
        { "HomeApi", null, Gate.Floor },
        { "AnalyticsApi", "Aging", Gate.Floor },
        { "StepTypesApi", "List", Gate.Floor },
        { "WorkflowsApi", "Templates", Gate.Floor },
        { "WorkflowsApi", "UpdateRugPath", Gate.Floor },
        { "LabelsApi", "List", Gate.Floor },
        { "LabelsApi", "Get", Gate.Floor },

        // Business: پول، فروش، طرف‌های خدمات، سطل زباله، گزارش‌ها
        { "Reports", null, Gate.Business },
        { "Rugs", "Trash", Gate.Business },
        { "Rugs", "Delete", Gate.Business },
        { "Rugs", "Restore", Gate.Business },
        { "Sales", null, Gate.Business },
        { "SalesApi", null, Gate.Business },
        { "AnalyticsApi", "Get", Gate.Business },
        { "Providers", null, Gate.Business },
        { "ProvidersApi", null, Gate.Business },
        { "PricingApi", null, Gate.Business },

        // Configure: پیکربندی
        { "Settings", null, Gate.Configure },
        { "SettingsHub", null, Gate.Configure },
        { "Preferences", null, Gate.Configure },
        { "SettingsPreferencesApi", null, Gate.Configure },
        { "Users", null, Gate.Configure },
        { "CustomFieldsApi", null, Gate.Configure },
        { "StepTypesApi", "Create", Gate.Configure },
        { "StepTypesApi", "Update", Gate.Configure },
        { "StepTypesApi", "Delete", Gate.Configure },
        { "WorkflowsApi", "CreateTemplate", Gate.Configure },
        { "WorkflowsApi", "UpdateTemplate", Gate.Configure },
        { "WorkflowsApi", "CreateProvider", Gate.Configure },
        { "Workflows", "Create", Gate.Configure },
        { "Workflows", "Edit", Gate.Configure },
        { "Labels", "Design", Gate.Configure },
        { "Labels", "Delete", Gate.Configure },
        { "LabelsApi", "Create", Gate.Configure },
        { "LabelsApi", "Update", Gate.Configure },
        { "LabelsApi", "Delete", Gate.Configure },

        // خانه و حساب
        { "Home", "Index", Gate.Authenticated },
        { "Home", "Error", Gate.Anonymous },
        { "Home", "StatusCodeError", Gate.Anonymous },
        { "Account", "Login", Gate.Anonymous },
        { "Account", "Denied", Gate.Anonymous },
        { "Account", "Logout", Gate.Authenticated },
        { "Account", "Profile", Gate.Authenticated },

        // پنل مدیر سامانه (بدون تغییر)
        { "Admin", null, Gate.SystemAdmin }
    };

    [Theory]
    [MemberData(nameof(J2Sites))]
    public void J2Site_HasItsPolicy(string controllerName, string? actionName, Gate expected)
    {
        var controller = Controllers().SingleOrDefault(t => t.Name == controllerName + "Controller");
        Assert.True(controller is not null, $"کنترلر {controllerName} پیدا نشد (جدول کهنه است؟)");

        var actions = Actions()
            .Where(a => a.Controller == controller && (actionName is null || a.Action.Name == actionName))
            .ToList();
        Assert.True(actions.Count > 0, $"اکشن {controllerName}.{actionName} پیدا نشد (جدول کهنه است؟)");

        foreach (var (type, action) in actions)
            Assert.True(GateOf(type, action) == expected,
                $"{Key(type, action)}: انتظار {expected}، ولی {GateOf(type, action)}");
    }

    [Fact]
    public void PolicyRoles_FollowTheSpec()
    {
        // A.1: Floor ⊃ Business ⊃ Configure، و مدیر سامانه در هیچ‌کدام
        Assert.Equal([UserRole.TenantAdmin, UserRole.Merchant, UserRole.Operator], Policies.FloorRoles);
        Assert.Equal([UserRole.TenantAdmin, UserRole.Merchant], Policies.BusinessRoles);
        Assert.Equal([UserRole.TenantAdmin], Policies.ConfigureRoles);
        Assert.DoesNotContain(UserRole.SystemAdmin, Policies.FloorRoles);
    }

    // ── ابزار ────────────────────────────────────────────────────────────────

    private static IEnumerable<Type> Controllers()
        => WebAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true }
                        && typeof(ControllerBase).IsAssignableFrom(t)
                        && t.Name.EndsWith("Controller", StringComparison.Ordinal))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions()
        => Controllers().SelectMany(type => type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null)
            .Select(m => (type, m)));

    private static string Key(Type controller, MethodInfo action)
        => $"{controller.Name[..^"Controller".Length]}.{action.Name}";

    private static string? RouteTemplate(Type controller)
        => controller.GetCustomAttributes<RouteAttribute>(inherit: true).FirstOrDefault()?.Template;

    /// <summary>
    /// در مؤثر: <c>[AllowAnonymous]</c> (روی اکشن یا کلاس) همه را کنار می‌زند؛ وگرنه سخت‌گیرترینِ همهٔ
    /// <c>[Authorize]</c>های کلاس (با پایه‌ها) و اکشن.
    /// </summary>
    public static Gate GateOf(Type controller, MethodInfo action)
    {
        if (action.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any()
            || controller.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any())
            return Gate.Anonymous;

        var data = controller.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>()
            .Concat(action.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>())
            .ToList();
        if (data.Count == 0)
            return Gate.None;

        var gates = data.Select(GateOf).ToList();
        if (gates.Contains(Gate.Unknown))
            return Gate.Unknown;
        if (gates.Contains(Gate.SystemAdmin))
            return gates.All(g => g is Gate.SystemAdmin or Gate.Authenticated) ? Gate.SystemAdmin : Gate.Unknown;

        return gates.Max();
    }

    private static Gate GateOf(IAuthorizeData data)
    {
        if (!string.IsNullOrEmpty(data.AuthenticationSchemes))
            return Gate.Unknown;

        if (!string.IsNullOrEmpty(data.Roles))
        {
            // فقط پنل مدیر سامانه با نقش است؛ نقش‌های کارگاه همیشه از راه سیاست
            return string.IsNullOrEmpty(data.Policy) && data.Roles == nameof(UserRole.SystemAdmin)
                ? Gate.SystemAdmin
                : Gate.Unknown;
        }

        return data.Policy switch
        {
            null or "" => Gate.Authenticated,
            Policies.Floor => Gate.Floor,
            Policies.Business => Gate.Business,
            Policies.Configure => Gate.Configure,
            _ => Gate.Unknown
        };
    }
}
