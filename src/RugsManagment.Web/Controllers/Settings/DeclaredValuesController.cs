using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Fields;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Settings;

/// <summary>
/// صفحهٔ جدول ارزش گمرکی (<c>/settings/declared-values</c>) — فقط تنظیم‌کننده (Configure). خودِ جدول جزیرهٔ
/// <c>declared-values-editor</c> است که از <c>/api/declared-values</c> می‌خواند و با <c>PUT /api/settings/declared-values</c>
/// (جایگزینی کل جدول با مهر هم‌زمانی) ذخیره می‌کند؛ این اکشن فقط قاب صفحه و نام فیلد کلید را می‌دهد.
///
/// چرا نام فیلد این‌جا حساب می‌شود: کلید جدول ترجیح کارگاه است (<c>DeclaredValueKeyField</c>، پیش‌فرض محل بافت) و می‌تواند
/// یک فیلد سفارشی باشد؛ برچسب فارسی‌اش از همان کاتالوگ فیلدها می‌آید که جدول‌ها و اسناد می‌خوانند.
/// </summary>
[Authorize(Policy = Policies.Configure)]
[Route("settings/declared-values")]
public sealed class DeclaredValuesController(ITenantPreferencesService preferences, IFieldCatalog fields) : Controller
{
    /// <summary>برچسب کلیدهای پایه، اگر کاتالوگ چیزی نداد.</summary>
    private static readonly IReadOnlyDictionary<string, string> KnownLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [RugFieldKeys.Origin] = "محل بافت",
        [RugFieldKeys.Pattern] = "نقشه",
        [RugFieldKeys.Material] = "مواد مصرفی"
    };

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var tenantId = User.RequireTenantId();
        var prefs = await preferences.GetAsync(tenantId, ct);
        var keyField = string.IsNullOrWhiteSpace(prefs.DeclaredValueKeyField) ? RugFieldKeys.Origin : prefs.DeclaredValueKeyField;

        var catalog = await fields.ListAsync(tenantId, FieldPurpose.Grid, null, ct);
        var label = catalog.FirstOrDefault(f => string.Equals(f.Key, keyField, StringComparison.Ordinal))?.Label
                    ?? (KnownLabels.TryGetValue(keyField, out var known) ? known : keyField);

        return View("~/Views/Settings/DeclaredValues.cshtml",
            new DeclaredValuesPageModel(keyField, label, prefs.DeclaredValueFallbackUsd));
    }
}

/// <summary>مدل قاب صفحهٔ ارزش گمرکی (props جزیره).</summary>
/// <param name="KeyField">کلید فیلدی که نرخ با مقدارش پیدا می‌شود (مثل <c>rug.origin</c>)</param>
/// <param name="KeyFieldLabel">برچسب فارسی همان فیلد (مثل «محل بافت»)</param>
/// <param name="FallbackUsd">نرخ پیش‌فرض برای مقدارهای بیرون از جدول؛ null یعنی «ندارد»</param>
public sealed record DeclaredValuesPageModel(string KeyField, string KeyFieldLabel, decimal? FallbackUsd);
