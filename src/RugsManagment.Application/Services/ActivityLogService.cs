using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Activity;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>خواندن تاریخچهٔ فعالیت کارگاه. فقط خواندنی — لاگ ویرایش نمی‌شود.</summary>
public interface IActivityLogService
{
    Task<ActivityPageDto> ListAsync(Guid tenantId, ActivityQuery query, CancellationToken ct = default);

    /// <summary>رویدادهای یک موجودیت مشخص — مثلاً تاریخچهٔ یک فرش.</summary>
    Task<IReadOnlyList<ActivityEntryDto>> ForEntityAsync(
        Guid tenantId, string entityType, Guid entityId, CancellationToken ct = default);
}

/// <summary>
/// صفحه‌بندی و شمارش در SQL (<see cref="IAuditEntryRepository"/>)؛ برچسب‌ها با واژهٔ نگله/عدلِ همان کارگاه.
///
/// صفحهٔ تاریخچه فقط برای تنظیم‌کننده است؛ ولی تاریخچهٔ یک موجودیت (مثلاً یک فرش) به صفحه‌هایی می‌رسد که
/// انباردار هم می‌بیند، پس آن‌جا رویدادهای پولی (<see cref="MoneyActions"/>) برای غیر Business حذف می‌شوند.
/// </summary>
public sealed class ActivityLogService(
    IAuditEntryRepository entries,
    ITenantPreferencesRepository preferences,
    IMoneyVisibility money) : IActivityLogService
{
    /// <summary>کمترین و بیشترین اندازهٔ صفحه.</summary>
    public const int MinPageSize = 10;
    public const int MaxPageSize = 100;

    public async Task<ActivityPageDto> ListAsync(
        Guid tenantId, ActivityQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, MinPageSize, MaxPageSize);

        var result = await entries.PageAsync(tenantId, query.Action, query.From, query.To, page, size, ct);
        var terms = await TermsAsync(tenantId, ct);

        // شمارهٔ صفحه همان است که مخزن واقعاً برگرداند: صفحهٔ بعد از آخر به آخرین صفحه بریده می‌شود
        return new ActivityPageDto(
            result.Items.Select(e => ToDto(e, terms)).ToList(), result.TotalCount, result.Page, size);
    }

    public async Task<IReadOnlyList<ActivityEntryDto>> ForEntityAsync(
        Guid tenantId, string entityType, Guid entityId, CancellationToken ct = default)
    {
        var list = await entries.ListForEntityAsync(tenantId, entityType, entityId, ct);
        var terms = await TermsAsync(tenantId, ct);
        return list
            .Where(e => money.CanSeeMoney || !MoneyActions.Contains(e.Action))
            .Select(e => ToDto(e, terms))
            .ToList();
    }

    /// <summary>واژهٔ نگله/عدل کارگاه؛ کارگاهی که هنوز ردیف ترجیحات ندارد «نگله».</summary>
    internal async Task<BaleTermWords> TermsAsync(Guid tenantId, CancellationToken ct)
        => BaleTerms.For((await preferences.GetAsync(tenantId, ct))?.Preferences.BaleTerm ?? BaleTerm.Negleh);

    /// <summary>یک رویداد برای نمایش (صفحهٔ تاریخچه، تاریخچهٔ فرش، «فعالیت‌های اخیر» خانه).</summary>
    public static ActivityEntryDto ToDto(AuditEntry e, BaleTermWords? terms = null) => new(
        e.Id,
        e.UserName,
        e.Action,
        ActionLabel(e.Action, terms),
        e.EntityType,
        e.EntityId,
        e.EntityLabel,
        e.Summary,
        e.CreatedAt);

    /// <summary>
    /// رویدادهای پولی — برای کسی که مبلغ نمی‌بیند (انباردار) از «فعالیت‌های اخیر» حذف می‌شوند، چون
    /// خلاصهٔ آن‌ها (فروش به چه کسی، پرداخت به کدام کارگاه، هزینهٔ چه) خودش اطلاعات مالی است.
    /// </summary>
    public static IReadOnlyCollection<AuditAction> MoneyActions { get; } =
    [
        AuditAction.SaleRecorded,
        AuditAction.SaleCancelled,
        AuditAction.ProviderPaid,
        AuditAction.CostEntryCreated,
        AuditAction.CostEntryUpdated,
        AuditAction.CostEntryDeleted,
        AuditAction.CostEntryRestored,
        AuditAction.CostCategoryChanged,
        AuditAction.RevenueChanged
    ];

    /// <summary>
    /// برچسب فارسی هر نوع رویداد — برای چیپ فیلتر و ستون «کنش». هر مقدار <see cref="AuditAction"/> برچسب
    /// دارد (آزمون واحد بررسی می‌کند)؛ واژهٔ نگله از <paramref name="terms"/> (پیش‌فرض «نگله») می‌آید تا
    /// کارگاهی که «عدل» می‌گوید در تاریخچه هم «عدل» ببیند.
    /// </summary>
    public static string ActionLabel(AuditAction action, BaleTermWords? terms = null)
    {
        var t = terms ?? BaleTerms.Negleh;
        return action switch
        {
            AuditAction.Created => "ایجاد",
            AuditAction.Updated => "ویرایش",
            AuditAction.Deleted => "حذف",
            AuditAction.Restored => "بازگردانی",
            AuditAction.StepAdvanced => "پیشبرد مرحله",
            AuditAction.StepReverted => "بازگشت مرحله",
            AuditAction.StepSkipped => "رد کردن مرحله",
            AuditAction.WorkflowChanged => "تغییر مسیر",
            AuditAction.SaleRecorded => "ثبت فروش",
            AuditAction.SaleCancelled => "لغو فروش",
            AuditAction.ProviderPaid => "پرداخت به طرف خدمات",
            AuditAction.UserInvited => "افزودن کاربر",
            AuditAction.UserDisabled => "غیرفعال کردن کاربر",
            AuditAction.PasswordChanged => "تغییر رمز",
            AuditAction.SettingsChanged => "تغییر تنظیمات",

            AuditAction.PreferencesChanged => "تغییر ترجیحات",
            AuditAction.PresetApplied => "اعمال بستهٔ آماده",
            AuditAction.GridLayoutChanged => "تغییر ستون‌های جدول",

            AuditAction.RugSplit => "جدا کردن دسته",
            AuditAction.FinalSizeRecorded => "ثبت اندازهٔ نهایی",

            AuditAction.ShipmentCreated => "ساخت محموله",
            AuditAction.ShipmentUpdated => "ویرایش محموله",
            AuditAction.ShipmentDeleted => "حذف محموله",
            AuditAction.ShipmentFinalized => "قطعی کردن محموله",
            AuditAction.ShipmentReopened => "باز کردن دوبارهٔ محموله",
            AuditAction.BaleCreated => $"ساخت {t.Bale}",
            AuditAction.BaleDeleted => $"حذف {t.Bale}",
            AuditAction.BalesRenumbered => $"شماره‌گذاری دوبارهٔ {t.Bales}",
            AuditAction.LinesChanged => $"{t.Bale}‌بندی",
            AuditAction.DeclaredValuesChanged => "تغییر ارزش گمرکی",

            AuditAction.CostEntryCreated => "ثبت هزینه",
            AuditAction.CostEntryUpdated => "ویرایش هزینه",
            AuditAction.CostEntryDeleted => "حذف هزینه",
            AuditAction.CostEntryRestored => "بازگرداندن هزینه",
            AuditAction.CostCategoryChanged => "تغییر نوع هزینه",
            AuditAction.RevenueChanged => "ثبت فروش محموله",

            AuditAction.TemplateCreated => "ساخت قالب اکسل و چاپ",
            AuditAction.TemplateUpdated => "ویرایش قالب اکسل و چاپ",
            AuditAction.TemplateDeleted => "حذف قالب اکسل و چاپ",
            AuditAction.TemplateRestored => "بازگرداندن قالب اکسل و چاپ",
            AuditAction.Exported => "گرفتن اکسل یا چاپ",

            AuditAction.ImportCommitted => "ورود از اکسل",
            AuditAction.ImportUndone => "بازگرداندن ورود از اکسل",

            AuditAction.LedgerRebuilt => "بازسازی حساب‌ها",

            // مقدار ناشناخته (دادهٔ نسخهٔ جدیدتر) نام انگلیسی نشان نمی‌دهد
            _ => "رویداد"
        };
    }
}
