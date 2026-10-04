using System.Text.RegularExpressions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Platform;

/// <summary>
/// ترجیحات کارگاه: خواندن برای پوستهٔ هر صفحه و صفحهٔ ترجیحات، و ذخیره با سه ترمز —
/// اعتبارسنجی هر خانه (۴۰۰ با پیام زیر همان خانه)، نسخهٔ هم‌زمانی (۴۰۹ برای ذخیرهٔ کهنه) و قفل
/// «تعداد رقم اعشار مبلغ» بعد از ثبت اولین مبلغ (۴۰۹).
///
/// <see cref="TenantPreferencesDto.DefaultDeclarationTemplateId"/> از این مسیر نوشته نمی‌شود: فقط دکمهٔ
/// «پیش‌فرض کن» قالب‌ها (فاز اسناد) آن را می‌گذارد، چون باید وجود و درستیِ قالب را بسنجد.
/// </summary>
public sealed partial class TenantPreferencesService(
    ITenantPreferencesRepository repository,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : ITenantPreferencesService
{
    /// <summary>بیشترین شمارهٔ شروع (نه جای عدد دیگری در int).</summary>
    public const int MaxSerialStart = 999_999_999;

    /// <summary>کمترین و بیشترین «سقف ضلع» (متر)؛ هر ضلع واقعی بین ۵ سانت و ۳۰ متر است.</summary>
    public const decimal MinMaxPlausibleSideM = 1m;
    public const decimal MaxMaxPlausibleSideM = 30m;

    /// <summary>سقف نرخ دلار و ارزش گمرکی، هم‌اندازهٔ ستون‌هایشان.</summary>
    public const decimal MaxUsdFx = 999_999_999_999.999999m;
    public const decimal MaxUsdPerM2 = 999_999_999_999.99m;

    /// <summary>numeric(6,2)</summary>
    public const decimal MaxBaleTargetAreaM2 = 9_999.99m;

    public const int ExporterNameMaxLength = 200;
    public const int ExporterCodeMaxLength = 60;
    public const int ExporterAddressMaxLength = 500;
    public const int ExporterPhoneMaxLength = 40;
    public const int CustomsOfficeMaxLength = 200;

    public async Task<TenantPreferencesDto> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var row = await repository.GetAsync(tenantId, cancellationToken);
        var locked = await repository.HasStoredMoneyAsync(tenantId, cancellationToken);

        return row is null
            ? ToDto(TenantPreferences.CreateDefault(tenantId), version: 0, locked)
            : ToDto(row.Preferences, row.Version, locked);
    }

    public async Task<TenantPreferencesDto> UpdateAsync(
        Guid tenantId, TenantPreferencesDto request, CancellationToken cancellationToken = default)
    {
        var values = Validate(request);

        var tracked = await repository.GetForUpdateAsync(tenantId, request.Version, cancellationToken);
        var isNew = tracked is null;
        if (isNew)
        {
            // ردیفی نیست؛ فقط کسی می‌تواند بسازدش که همان «پیش‌فرض با نسخهٔ ۰» را دیده باشد
            if (request.Version != 0)
                throw new ConcurrencyConflictException();
            tracked = TenantPreferences.CreateDefault(tenantId);
        }

        var locked = await repository.HasStoredMoneyAsync(tenantId, cancellationToken);
        if (values.MoneyScale != tracked!.MoneyScale && locked)
            throw new DomainConflictException(Messages.MoneyLocked.Text, "moneyScale");

        var before = Snapshot(tracked);
        Apply(values, tracked);
        var after = Snapshot(tracked);
        var changes = Diff(before, after);

        if (changes.Count == 0 && !isNew)
            return ToDto(tracked, repository.CurrentVersion(tracked), locked);

        if (isNew)
            await repository.AddAsync(tracked, cancellationToken);

        var terms = BaleTerms.For(tracked.BaleTerm);
        var labels = changes.Keys.Select(k => FieldLabel(k, terms));
        audit.Record(
            AuditAction.PreferencesChanged,
            nameof(TenantPreferences),
            tenantId,
            changes.Count == 0
                ? "ترجیحات کارگاه ثبت شد."
                : $"ترجیحات کارگاه عوض شد: {string.Join("، ", labels)}.",
            details: new { changes });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tracked, repository.CurrentVersion(tracked), locked);
    }

    // ═════════════════════════════════════════════════════════════════════
    // اعتبارسنجی
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>مقدارهای تمیزشده‌ای که روی ردیف نوشته می‌شوند.</summary>
    private sealed record Values(
        string SkuPrefix,
        SerialMode SerialMode,
        int? SerialStart,
        short MoneyScale,
        decimal MaxPlausibleSideM,
        decimal? DefaultUsdFx,
        AllocationMethod DefaultMethod,
        AreaBasis DefaultAreaBasis,
        string DeclaredValueKeyField,
        decimal? DeclaredValueFallbackUsd,
        decimal? DefaultBaleTargetAreaM2,
        short BaleTargetTolerancePercent,
        bool RequireFinalSizeToFinalize,
        BaleTerm BaleTerm,
        string? ExporterName,
        string? ExporterCode,
        string? ExporterAddress,
        string? ExporterPhone,
        string? CustomsOffice);

    /// <summary>
    /// همهٔ خانه‌ها را می‌سنجد و همهٔ خطاها را با هم برمی‌گرداند (نه فقط اولی)، تا تنظیم‌کننده در یک بار
    /// ذخیره همهٔ خانه‌های نادرست را ببیند.
    /// </summary>
    private static Values Validate(TenantPreferencesDto r)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var terms = BaleTerms.For(Enum.IsDefined(r.BaleTerm) ? r.BaleTerm : BaleTerm.Negleh);

        void Fail(string field, string message) => errors[field] = [message];
        string Label(string field) => FieldLabel(field, terms);
        void Range(string field, decimal min, decimal max)
            => Fail(field, Messages.OutOfRange.Format(("label", Label(field)), ("min", min), ("max", max)));
        void Invalid(string field) => Fail(field, Messages.InvalidValue.Format(("label", Label(field))));

        var skuPrefix = (r.SkuPrefix ?? string.Empty).Trim().ToUpperInvariant();
        if (!SkuPrefixPattern().IsMatch(skuPrefix))
            Fail("skuPrefix", Messages.SkuPrefixInvalid.Text);

        if (!Enum.IsDefined(r.SerialMode)) Invalid("serialMode");

        if (r.SerialStart is { } start && start is < 1 or > MaxSerialStart)
            Range("serialStart", 1, MaxSerialStart);

        if (r.MoneyScale is < TenantPreferences.MinMoneyScale or > TenantPreferences.MaxMoneyScale)
            Range("moneyScale", TenantPreferences.MinMoneyScale, TenantPreferences.MaxMoneyScale);

        if (r.MaxPlausibleSideM is < MinMaxPlausibleSideM or > MaxMaxPlausibleSideM)
            Range("maxPlausibleSideM", MinMaxPlausibleSideM, MaxMaxPlausibleSideM);
        else if (!Num.HasAtMostDecimals(r.MaxPlausibleSideM, 2))
            Invalid("maxPlausibleSideM");

        // نرخ ارز: بزرگ‌تر از صفر، حداکثر شش رقم اعشار (numeric(18,6))
        if (r.DefaultUsdFx is { } fx && (fx <= 0 || fx > MaxUsdFx || !Num.HasAtMostDecimals(fx, 6)))
            Invalid("defaultUsdFx");

        if (!Enum.IsDefined(r.DefaultMethod)) Invalid("defaultMethod");
        if (!Enum.IsDefined(r.DefaultAreaBasis)) Invalid("defaultAreaBasis");

        var keyField = (r.DeclaredValueKeyField ?? string.Empty).Trim();
        if (keyField.Length == 0)
            Fail("declaredValueKeyField", Messages.Required.Format(("label", Label("declaredValueKeyField"))));
        else if (keyField.Length > TenantPreferences.DeclaredValueKeyFieldMaxLength)
            Fail("declaredValueKeyField", Messages.TooLong.Format(
                ("label", Label("declaredValueKeyField")), ("n", TenantPreferences.DeclaredValueKeyFieldMaxLength)));
        else if (!FieldKeyPattern().IsMatch(keyField))
            Invalid("declaredValueKeyField");

        // دلار برای هر متر: صفر یا بیشتر، حداکثر دو رقم اعشار (numeric(14,2))
        if (r.DeclaredValueFallbackUsd is { } fallback
            && (fallback < 0 || fallback > MaxUsdPerM2 || !Num.HasAtMostDecimals(fallback, 2)))
            Invalid("declaredValueFallbackUsd");

        if (r.DefaultBaleTargetAreaM2 is { } target)
        {
            if (target is <= 0 or > MaxBaleTargetAreaM2) Range("defaultBaleTargetAreaM2", 0.01m, MaxBaleTargetAreaM2);
            else if (!Num.HasAtMostDecimals(target, 2)) Invalid("defaultBaleTargetAreaM2");
        }

        if (r.BaleTargetTolerancePercent is < 0 or > 100)
            Range("baleTargetTolerancePercent", 0, 100);

        if (!Enum.IsDefined(r.BaleTerm)) Invalid("baleTerm");

        string? Text(string field, string? value, int maxLength)
        {
            var clean = string.IsNullOrWhiteSpace(value) ? null : PersianText.Normalize(value);
            if (clean is { Length: var length } && length > maxLength)
                Fail(field, Messages.TooLong.Format(("label", Label(field)), ("n", maxLength)));
            return clean;
        }

        var exporterName = Text("exporterName", r.ExporterName, ExporterNameMaxLength);
        var exporterCode = Text("exporterCode", r.ExporterCode, ExporterCodeMaxLength);
        var exporterAddress = Text("exporterAddress", r.ExporterAddress, ExporterAddressMaxLength);
        var exporterPhone = Text("exporterPhone", r.ExporterPhone, ExporterPhoneMaxLength);
        var customsOffice = Text("customsOffice", r.CustomsOffice, CustomsOfficeMaxLength);

        if (errors.Count > 0)
            throw new AppValidationException(Messages.ValidationFailed.Text, errors);

        return new Values(
            skuPrefix, r.SerialMode, r.SerialStart, r.MoneyScale, r.MaxPlausibleSideM, r.DefaultUsdFx,
            r.DefaultMethod, r.DefaultAreaBasis, keyField, r.DeclaredValueFallbackUsd, r.DefaultBaleTargetAreaM2,
            r.BaleTargetTolerancePercent, r.RequireFinalSizeToFinalize, r.BaleTerm,
            exporterName, exporterCode, exporterAddress, exporterPhone, customsOffice);
    }

    /// <summary>حرف لاتین بزرگ یا رقم، ۱ تا ۱۲ (بعد از Trim و حروف بزرگ).</summary>
    [GeneratedRegex("^[A-Z0-9]{1,12}$")]
    private static partial Regex SkuPrefixPattern();

    /// <summary>کلید فیلد کاتالوگ: «rug.origin»، «rug.meta.bg_color» — حرف لاتین، رقم، «_» و «.».</summary>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z0-9_]+)*$")]
    private static partial Regex FieldKeyPattern();

    // ═════════════════════════════════════════════════════════════════════
    // نوشتن، مقایسه و تبدیل
    // ═════════════════════════════════════════════════════════════════════

    private static void Apply(Values v, TenantPreferences p)
    {
        p.SkuPrefix = v.SkuPrefix;
        p.SerialMode = v.SerialMode;
        p.SerialStart = v.SerialStart;
        p.MoneyScale = v.MoneyScale;
        p.MaxPlausibleSideM = v.MaxPlausibleSideM;
        p.DefaultUsdFx = v.DefaultUsdFx;
        p.DefaultMethod = v.DefaultMethod;
        p.DefaultAreaBasis = v.DefaultAreaBasis;
        p.DeclaredValueKeyField = v.DeclaredValueKeyField;
        p.DeclaredValueFallbackUsd = v.DeclaredValueFallbackUsd;
        p.DefaultBaleTargetAreaM2 = v.DefaultBaleTargetAreaM2;
        p.BaleTargetTolerancePercent = v.BaleTargetTolerancePercent;
        p.RequireFinalSizeToFinalize = v.RequireFinalSizeToFinalize;
        p.BaleTerm = v.BaleTerm;
        p.ExporterName = v.ExporterName;
        p.ExporterCode = v.ExporterCode;
        p.ExporterAddress = v.ExporterAddress;
        p.ExporterPhone = v.ExporterPhone;
        p.CustomsOffice = v.CustomsOffice;
    }

    /// <summary>مقدار هر خانه با نام JSON آن — برای «چه عوض شد» در تاریخچه (بدون مبلغ تومانی).</summary>
    private static Dictionary<string, object?> Snapshot(TenantPreferences p) => new(StringComparer.Ordinal)
    {
        ["skuPrefix"] = p.SkuPrefix,
        ["serialMode"] = p.SerialMode,
        ["serialStart"] = p.SerialStart,
        ["moneyScale"] = p.MoneyScale,
        ["maxPlausibleSideM"] = p.MaxPlausibleSideM,
        ["defaultUsdFx"] = p.DefaultUsdFx,
        ["defaultMethod"] = p.DefaultMethod,
        ["defaultAreaBasis"] = p.DefaultAreaBasis,
        ["declaredValueKeyField"] = p.DeclaredValueKeyField,
        ["declaredValueFallbackUsd"] = p.DeclaredValueFallbackUsd,
        ["defaultBaleTargetAreaM2"] = p.DefaultBaleTargetAreaM2,
        ["baleTargetTolerancePercent"] = p.BaleTargetTolerancePercent,
        ["requireFinalSizeToFinalize"] = p.RequireFinalSizeToFinalize,
        ["baleTerm"] = p.BaleTerm,
        ["exporterName"] = p.ExporterName,
        ["exporterCode"] = p.ExporterCode,
        ["exporterAddress"] = p.ExporterAddress,
        ["exporterPhone"] = p.ExporterPhone,
        ["customsOffice"] = p.CustomsOffice
    };

    /// <summary>خانه‌هایی که مقدارشان عوض شده: نام → {from, to}. اعشار با مقدار مقایسه می‌شود (۱۲ = ۱۲٫۰۰).</summary>
    private static Dictionary<string, ChangeDto> Diff(
        IReadOnlyDictionary<string, object?> before, IReadOnlyDictionary<string, object?> after)
    {
        var changes = new Dictionary<string, ChangeDto>(StringComparer.Ordinal);
        foreach (var (key, old) in before)
        {
            var now = after[key];
            if (!Equals(old, now)) changes[key] = new ChangeDto(old, now);
        }
        return changes;
    }

    /// <summary>یک تغییر در جزئیات تاریخچه.</summary>
    private sealed record ChangeDto(object? From, object? To);

    /// <summary>برچسب فارسی هر خانه — در پیام خطا و خلاصهٔ تاریخچه.</summary>
    internal static string FieldLabel(string field, BaleTermWords terms) => field switch
    {
        "skuPrefix" => "پیشوند کد فرش",
        "serialMode" => "شماره‌گذاری فرش",
        "serialStart" => "شمارهٔ شروع",
        "moneyScale" => "تعداد رقم اعشار مبلغ",
        "maxPlausibleSideM" => "سقف ضلع به متر",
        "defaultUsdFx" => "نرخ پیش‌فرض دلار",
        "defaultMethod" => "روش پیش‌فرض تقسیم هزینه",
        "defaultAreaBasis" => "پایهٔ متراژ هزینه",
        "declaredValueKeyField" => "ستون ارزش گمرکی",
        "declaredValueFallbackUsd" => "ارزش گمرکی پیش‌فرض",
        "defaultBaleTargetAreaM2" => $"متراژ هدف هر {terms.Bale}",
        "baleTargetTolerancePercent" => "درصد مجاز بیش از هدف",
        "requireFinalSizeToFinalize" => "اندازهٔ نهایی پیش از قطعی کردن",
        "baleTerm" => $"واژهٔ «{BaleTerms.Negleh.Bale}» یا «{BaleTerms.Adl.Bale}»",
        "exporterName" => "نام صادرکننده",
        "exporterCode" => "کد بازرگانی",
        "exporterAddress" => "نشانی صادرکننده",
        "exporterPhone" => "تلفن صادرکننده",
        "customsOffice" => "گمرک",
        _ => field
    };

    private static TenantPreferencesDto ToDto(TenantPreferences p, uint version, bool moneyScaleLocked) => new(
        p.SkuPrefix,
        p.SerialMode,
        p.SerialStart,
        p.MoneyScale,
        p.DefaultUsdFx,
        p.DefaultMethod,
        p.DefaultAreaBasis,
        p.DeclaredValueKeyField,
        p.DeclaredValueFallbackUsd,
        p.DefaultBaleTargetAreaM2,
        p.BaleTargetTolerancePercent,
        p.RequireFinalSizeToFinalize,
        p.BaleTerm,
        p.DefaultDeclarationTemplateId,
        p.ExporterName,
        p.ExporterCode,
        p.ExporterAddress,
        p.ExporterPhone,
        p.CustomsOffice,
        moneyScaleLocked,
        version,
        p.MaxPlausibleSideM);
}
