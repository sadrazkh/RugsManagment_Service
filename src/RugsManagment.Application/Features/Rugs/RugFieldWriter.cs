using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Rugs;

/// <summary>
/// زمینهٔ یک نوشتن: کارگاه، فرش تازه است یا نه، حالت شماره، رقم اعشار مبلغ‌ها، و «چه کسی، کِی» برای اندازهٔ نهایی.
/// </summary>
/// <param name="TenantId">کارگاه (فیلدهای سفارشی و نوع‌های خرید فقط از همین کارگاه).</param>
/// <param name="IsNew">فرش تازه است (طول و عرض اول لازم‌اند؛ در حالت «خودکار» شمارهٔ کاربر نادیده گرفته می‌شود).</param>
/// <param name="SerialMode">حالت شمارهٔ کارگاه.</param>
/// <param name="MoneyScale">رقم اعشار مبلغ‌های تومان/ریال (۰ تا ۲).</param>
/// <param name="Now">زمان ثبت (اندازهٔ نهایی).</param>
/// <param name="UserId">کاربر (اندازهٔ نهایی)؛ null برای کار سیستمی.</param>
public sealed record RugWriteContext(
    Guid TenantId,
    bool IsNew,
    SerialMode SerialMode,
    short MoneyScale,
    DateTimeOffset Now,
    Guid? UserId);

/// <summary>نتیجهٔ نوشتن.</summary>
/// <param name="ChangedKeys">کلیدهایی که واقعاً مقدارشان عوض شد (برای تاریخچه).</param>
/// <param name="NeedsAutoSerial">
/// فرش تازه در حالت «خودکار» است: شماره را سرویس زیر قفل کارگاه و پیش از flush می‌گذارد (E2)، نه این نویسنده.
/// </param>
/// <param name="PurchaseChanged">سطری از خرید افزوده، عوض یا حذف شد.</param>
public sealed record RugFieldWriteResult(
    IReadOnlyList<string> ChangedKeys,
    bool NeedsAutoSerial,
    bool PurchaseChanged);

/// <summary>نوشتن مقدار فیلدها (با کلید FieldCatalog) روی یک فرش — تنها راه نوشتن فرش از فاز ۲ (spec G.6، I.3).</summary>
public interface IRugFieldWriter
{
    /// <summary>
    /// کلید پولی (قیمت فروش هدف، هر خانهٔ خرید) از کسی که Business نیست → ۴۰۳ برای کل درخواست (A.5، G.6 #5)، حتی با
    /// مقدار null، چون پاک کردن قیمت خرید هم نوشتن پول است.
    /// </summary>
    void EnsureMayWrite(IReadOnlyDictionary<string, JsonElement?>? values);

    /// <summary>
    /// مقدارها را روی <paramref name="rug"/> می‌نویسد. اول همه را می‌خواند و می‌سنجد و فقط اگر همه درست بودند چیزی را
    /// عوض می‌کند؛ پس فرش هرگز نیمه‌نوشته نمی‌ماند (جدول ردیف نادرست را کنار می‌گذارد و بقیه را ذخیره می‌کند). خطاها همه با
    /// هم در یک <see cref="AppValidationException"/> (۴۰۰، کلید هر خطا همان کلید فیلد) می‌آیند.
    /// </summary>
    Task<RugFieldWriteResult> ApplyAsync(
        Rug rug, IReadOnlyDictionary<string, JsonElement?> values, RugWriteContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// قاعدهٔ قدیمیِ <c>UpdateRugRequest.PurchaseCost</c> (I.3) برای فرم قدیمی: فرش بی‌سطر → یک سطر «کل» «قیمت خرید»؛
    /// دقیقاً یک سطر «کل» → نرخ همان (مگر همان نرخ یا همان مبلغ سطر آمده باشد: رفت‌وبرگشت بی‌تغییر)؛ وگرنه برابر جمع
    /// سطرها → نادیده؛ وگرنه ۴۰۹ <c>E_PURCHASE_MULTILINE</c>.
    /// </summary>
    Task<bool> ApplyLegacyPurchaseCostAsync(Rug rug, decimal purchaseCost, RugWriteContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// نویسندهٔ فیلدهای فرش. چرا یک جا: فرم فرش، جدول ورود (فاز ۳)، برش دسته و ورود از اکسل همه همین قاعده‌ها را لازم
/// دارند — شماره و تعداد با هم (بازه هرگز کوتاه‌تر از تعداد نیست)، اندازهٔ اول و نهایی فقط از راه متدهای خود فرش، متن
/// یکسان‌سازی‌شده، فیلد سفارشی با نوع خودش، و خانهٔ خرید که سطر همان نوع را می‌سازد یا عوض یا پاک می‌کند (G.2). اگر هر
/// صفحه این‌ها را خودش می‌نوشت، اولین تفاوت کوچک یعنی دو فرش با یک شماره یا قیمت خریدی که دوبار حساب شده.
///
/// <para>این کلاس هیچ مبلغی حساب نمی‌کند: <c>Amount</c> سطرهای خرید و بهای فرش را پاس سازگاری بعد از ذخیره می‌نویسد
/// (D.4). همپوشانی شماره با فرش‌های دیگر هم این‌جا دیده نمی‌شود؛ <c>SerialOverlapStage</c> زیر قفل کارگاه می‌سنجد.</para>
/// </summary>
public sealed class RugFieldWriter(
    IRepository<CustomFieldDefinition> customFields,
    IRepository<CostCategory> categories,
    IRepository<RugPurchaseLine> purchaseLines,
    IMoneyVisibility money) : IRugFieldWriter
{
    /// <summary>بیشترین طول متن عنوان، محل بافت، نقشه و مواد.</summary>
    public const int ShortTextMaxLength = 200;

    /// <summary>بیشترین طول یادداشت.</summary>
    public const int NotesMaxLength = 2000;

    /// <summary>بیشترین طول مقدار فیلد سفارشی متنی.</summary>
    public const int CustomTextMaxLength = 500;

    /// <summary>بیشترین تعداد سطر خرید یک فرش (ویرایشگر و خانه‌های جدول).</summary>
    public const int MaxPurchaseLines = 30;

    /// <summary>بیشترین تعداد تخته در یک ردیف (همان سقف طول دسته).</summary>
    public const int MaxQuantity = SerialNumber.MaxRangeLength;

    /// <summary>بزرگ‌ترین نرخ/مبلغ (ستون numeric(18,4): ۱۴ رقم صحیح).</summary>
    public const decimal MaxRate = 99_999_999_999_999m;

    private static readonly Dictionary<string, string> Labels =
        RugFieldContributor.StaticFields.Concat(RugMoneyFieldContributor.Fields)
            .ToDictionary(f => f.Key, f => f.Label, StringComparer.Ordinal);

    public void EnsureMayWrite(IReadOnlyDictionary<string, JsonElement?>? values)
    {
        if (values is null || money.CanSeeMoney) return;
        if (values.Keys.Any(RugFieldKeys.IsMoney))
            throw new UnauthorizedAccessException(Messages.MoneyWriteForbidden.Text);
    }

    public async Task<RugFieldWriteResult> ApplyAsync(
        Rug rug, IReadOnlyDictionary<string, JsonElement?> values, RugWriteContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rug);
        ArgumentNullException.ThrowIfNull(values);
        EnsureMayWrite(values);

        var errors = new FieldErrors();
        var plan = new Plan();

        foreach (var (rawKey, value) in values)
        {
            var key = rawKey.Trim();
            switch (key)
            {
                case RugFieldKeys.Serial: ReadSerial(value, plan, errors); break;
                case RugFieldKeys.Quantity: ReadQuantity(value, plan, errors); break;
                case RugFieldKeys.Title: plan.Title = ReadShortText(key, value, errors); break;
                case RugFieldKeys.Origin: plan.Origin = ReadShortText(key, value, errors); break;
                case RugFieldKeys.Pattern: plan.Pattern = ReadShortText(key, value, errors); break;
                case RugFieldKeys.Material: plan.Material = ReadShortText(key, value, errors); break;
                case RugFieldKeys.Notes: plan.Notes = ReadNotes(value, errors); break;
                case RugFieldKeys.KnotDensity: plan.KnotDensity = ReadKnotDensity(value, errors); break;
                case RugFieldKeys.IntakeLength: plan.IntakeLength = ReadDimension(key, value, errors); break;
                case RugFieldKeys.IntakeWidth: plan.IntakeWidth = ReadDimension(key, value, errors); break;
                case RugFieldKeys.FinalLength: plan.FinalLength = ReadOptionalDimension(key, value, errors); break;
                case RugFieldKeys.FinalWidth: plan.FinalWidth = ReadOptionalDimension(key, value, errors); break;
                case RugFieldKeys.TargetSalePrice: plan.TargetSalePrice = ReadTargetSalePrice(value, context, errors); break;
                default:
                    if (key.StartsWith(RugFieldKeys.CustomPrefix, StringComparison.Ordinal))
                        plan.Custom[key[RugFieldKeys.CustomPrefix.Length..]] = value;
                    else if (key.StartsWith(RugFieldKeys.PurchasePrefix, StringComparison.Ordinal))
                        plan.Purchase[key] = value;
                    else if (Labels.TryGetValue(key, out var label))
                        errors.Add(key, Messages.FieldReadOnly.Format(("label", label)));
                    else
                        errors.Add(key, Messages.FieldUnknown.Format(("key", key)));
                    break;
            }
        }

        // ── قاعده‌های میان چند خانه ─────────────────────────────────────
        var needsAutoSerial = context.IsNew && context.SerialMode == SerialMode.Auto;
        if (needsAutoSerial)
            plan.SerialGiven = false; // حالت خودکار: متن کاربر برای فرش تازه نادیده گرفته می‌شود (G.4)

        var serial = plan.SerialGiven ? plan.Serial : rug.Serial;
        var quantity = plan.Quantity ?? rug.Quantity;
        if (!needsAutoSerial && (plan.SerialGiven || plan.Quantity is not null) && serial is not null
            && !errors.Has(RugFieldKeys.Serial) && !errors.Has(RugFieldKeys.Quantity) && !serial.Covers(quantity))
        {
            errors.Add(RugFieldKeys.Serial, serial.QuantityError(quantity).Message);
        }

        var intake = PlanIntake(rug, plan, context, errors);
        var final = PlanFinal(rug, plan, errors);

        var customPlan = plan.Custom.Count > 0
            ? await PlanCustomFieldsAsync(rug, plan.Custom, context, errors, cancellationToken)
            : null;
        var purchasePlan = plan.Purchase.Count > 0
            ? await PlanPurchaseAsync(rug, plan.Purchase, context, errors, cancellationToken)
            : null;

        errors.ThrowIfAny();

        // ── نوشتن (همه‌چیز سنجیده شده؛ متدهای فرش دیگر رد نمی‌کنند) ─────────
        var changed = new List<string>();

        if (plan.SerialGiven || plan.Quantity is not null)
        {
            var serialChanged = plan.SerialGiven && !Equals(serial?.Display, rug.SerialNo);
            var quantityChanged = quantity != rug.Quantity;
            if (serialChanged || quantityChanged)
            {
                rug.SetSerialAndQuantity(needsAutoSerial ? rug.Serial : serial, quantity);
                if (serialChanged) changed.Add(RugFieldKeys.Serial);
                if (quantityChanged) changed.Add(RugFieldKeys.Quantity);
            }
        }

        SetText(plan.Title, () => rug.Title, v => rug.Title = v, RugFieldKeys.Title, changed);
        SetText(plan.Origin, () => rug.Origin, v => rug.Origin = v, RugFieldKeys.Origin, changed);
        SetText(plan.Pattern, () => rug.Pattern, v => rug.Pattern = v, RugFieldKeys.Pattern, changed);
        SetText(plan.Material, () => rug.Material, v => rug.Material = v, RugFieldKeys.Material, changed);
        SetText(plan.Notes, () => rug.Notes, v => rug.Notes = v, RugFieldKeys.Notes, changed);

        if (plan.KnotDensity is { } knot && knot.Value != rug.KnotDensity)
        {
            rug.KnotDensity = knot.Value;
            changed.Add(RugFieldKeys.KnotDensity);
        }

        if (intake is { } size && (context.IsNew || size.Length != rug.IntakeLength || size.Width != rug.IntakeWidth))
        {
            rug.SetIntakeSize(size.Length, size.Width);
            changed.Add(RugFieldKeys.IntakeLength);
        }

        switch (final)
        {
            case { Clear: true } when rug.HasFinalSize:
                rug.ClearFinalSize();
                changed.Add(RugFieldKeys.FinalLength);
                break;
            case { Clear: false, Length: { } fl, Width: { } fw } when fl != rug.FinalLength || fw != rug.FinalWidth:
                rug.SetFinalSize(fl, fw, context.Now, context.UserId);
                changed.Add(RugFieldKeys.FinalLength);
                break;
        }

        if (plan.TargetSalePrice is { } target && target.Value != rug.TargetSalePrice)
        {
            rug.TargetSalePrice = target.Value;
            changed.Add(RugFieldKeys.TargetSalePrice);
        }

        if (customPlan is not null && customPlan.Changed)
        {
            rug.MetadataJson = customPlan.Json;
            changed.AddRange(customPlan.ChangedKeys);
        }

        var purchaseChanged = false;
        if (purchasePlan is not null)
        {
            purchaseChanged = await ApplyPurchaseAsync(rug, purchasePlan, context, cancellationToken);
            if (purchaseChanged) changed.AddRange(purchasePlan.Keys);
        }

        return new RugFieldWriteResult(changed, needsAutoSerial, purchaseChanged);
    }

    // ═════════════════════════════════════════════════════════════════════
    // قیمت خرید قدیمی (I.3)
    // ═════════════════════════════════════════════════════════════════════

    public async Task<bool> ApplyLegacyPurchaseCostAsync(
        Rug rug, decimal purchaseCost, RugWriteContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rug);
        if (!money.CanSeeMoney)
            throw new UnauthorizedAccessException(Messages.MoneyWriteForbidden.Text);
        if (!Num.HasAtMostDecimals(purchaseCost, 4) || Math.Abs(purchaseCost) > MaxRate)
            throw AppValidationException.ForField("purchaseCost", Messages.InvalidValue.Format(("label", "قیمت خرید")));

        var lines = context.IsNew
            ? []
            : await purchaseLines.ListAsync(l => l.RugId == rug.Id && l.TenantId == context.TenantId, cancellationToken);

        if (lines.Count == 0)
        {
            // فرم قدیمی صفر را برای فرش بی‌خرید هم می‌فرستد؛ سطر صفر چیزی را حساب نمی‌کند و فقط خانهٔ جدول را پر می‌کرد
            if (purchaseCost == 0) return false;

            // همان سطری که M2 از ستون قدیمی می‌ساخت: «کل»، «قیمت خرید»، نوع purchase.price اگر هست
            var priceCategory = (await categories.ListAsync(
                c => c.TenantId == context.TenantId && c.SeedKey == Seed.CostCategorySeedContributor.PriceKey,
                cancellationToken)).FirstOrDefault();

            await purchaseLines.AddAsync(new RugPurchaseLine
            {
                TenantId = context.TenantId,
                RugId = rug.Id,
                CategoryId = priceCategory?.Id,
                Label = RugPurchaseLine.LegacyPurchaseLabel,
                Basis = CostBasis.Total,
                Rate = purchaseCost,
                SortOrder = priceCategory?.SortOrder ?? 0
            }, cancellationToken);
            return true;
        }

        if (lines.Count == 1 && lines[0].Basis == CostBasis.Total)
        {
            // رفت‌وبرگشت بی‌تغییر: فرم قدیمی مبلغ سطر (ROUND(rate، s)) را پس می‌فرستد، نه نرخ را؛ نرخ ۱۰۰۰٫۵ با مبلغ ۱۰۰۱
            // در رقم اعشار ۰ نباید با همان ۱۰۰۱ بی‌صدا عوض شود (LM-02: سطرها بی‌تغییر می‌مانند)
            if (lines[0].Rate == purchaseCost || lines[0].Amount == purchaseCost) return false;
            var tracked = await purchaseLines.GetByIdAsync(lines[0].Id, cancellationToken)
                          ?? throw new KeyNotFoundException(Messages.PurchaseLineUnknown.Text);
            tracked.Rate = purchaseCost;
            tracked.UpdatedAt = context.Now;
            return true;
        }

        // فرم قدیمی همان جمع را دوباره فرستاده (رفت‌وبرگشت بی‌تغییر): هیچ سطری دست نمی‌خورد
        if (lines.Sum(l => l.Amount) == purchaseCost)
            return false;

        throw new DomainConflictException(Messages.PurchaseMultiLine.Text, "purchaseCost");
    }

    // ═════════════════════════════════════════════════════════════════════
    // خواندن و سنجیدن هر خانه
    // ═════════════════════════════════════════════════════════════════════

    private static void ReadSerial(JsonElement? value, Plan plan, FieldErrors errors)
    {
        plan.SerialGiven = true;
        if (!TryReadText(value, out var text))
        {
            errors.Add(RugFieldKeys.Serial, Messages.SerialFormat.Text);
            return;
        }

        if (SerialNumber.TryParse(text, out var serial, out var error))
            plan.Serial = serial;
        else
            errors.Add(RugFieldKeys.Serial, error!.Message);
    }

    private static void ReadQuantity(JsonElement? value, Plan plan, FieldErrors errors)
    {
        if (!TryReadInt(value, out var quantity) || quantity is null)
        {
            errors.Add(RugFieldKeys.Quantity, Messages.RugQuantity.Text);
            return;
        }

        if (quantity < 1)
            errors.Add(RugFieldKeys.Quantity, Messages.RugQuantity.Text);
        else if (quantity > MaxQuantity)
            errors.Add(RugFieldKeys.Quantity, Messages.OutOfRange.Format(("label", "تعداد"), ("min", 1), ("max", MaxQuantity)));
        else
            plan.Quantity = quantity;
    }

    private static Optional<string?> ReadShortText(string key, JsonElement? value, FieldErrors errors)
    {
        if (!TryReadText(value, out var text))
        {
            errors.Add(key, Messages.InvalidValue.Format(("label", Labels[key])));
            return default;
        }

        var normalized = string.IsNullOrWhiteSpace(text) ? null : PersianText.Normalize(text);
        if (normalized is { Length: > ShortTextMaxLength })
        {
            errors.Add(key, Messages.TooLong.Format(("label", Labels[key]), ("n", ShortTextMaxLength)));
            return default;
        }

        return new Optional<string?>(normalized);
    }

    private static Optional<string?> ReadNotes(JsonElement? value, FieldErrors errors)
    {
        if (!TryReadText(value, out var text))
        {
            errors.Add(RugFieldKeys.Notes, Messages.InvalidValue.Format(("label", Labels[RugFieldKeys.Notes])));
            return default;
        }

        // یادداشت همان‌طور که نوشته شد می‌ماند (فقط فاصلهٔ دو سر)؛ رقم‌ها و حرف‌ها را دست نمی‌زنیم
        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (trimmed is { Length: > NotesMaxLength })
        {
            errors.Add(RugFieldKeys.Notes, Messages.TooLong.Format(("label", Labels[RugFieldKeys.Notes]), ("n", NotesMaxLength)));
            return default;
        }

        return new Optional<string?>(trimmed);
    }

    private static Optional<int?> ReadKnotDensity(JsonElement? value, FieldErrors errors)
    {
        if (!TryReadInt(value, out var knot) || knot is < 0 or > 1_000_000)
        {
            errors.Add(RugFieldKeys.KnotDensity, Messages.InvalidValue.Format(("label", Labels[RugFieldKeys.KnotDensity])));
            return default;
        }

        return new Optional<int?>(knot);
    }

    private static decimal? ReadDimension(string key, JsonElement? value, FieldErrors errors)
    {
        if (!TryReadDecimal(value, NumberKind.Dimension, out var number) || number is null || !Dimension.IsValid(number.Value))
        {
            errors.Add(key, Messages.Dimension.Text);
            return null;
        }

        return number;
    }

    private static Optional<decimal?> ReadOptionalDimension(string key, JsonElement? value, FieldErrors errors)
    {
        if (IsNull(value)) return new Optional<decimal?>(null);
        var dimension = ReadDimension(key, value, errors);
        return dimension is null ? default : new Optional<decimal?>(dimension);
    }

    private static Optional<decimal?> ReadTargetSalePrice(JsonElement? value, RugWriteContext context, FieldErrors errors)
    {
        if (!TryReadDecimal(value, NumberKind.Money, out var price)
            || price is < 0 || (price is { } p && (!Num.HasAtMostDecimals(p, context.MoneyScale) || p > MaxRate)))
        {
            errors.Add(RugFieldKeys.TargetSalePrice, Messages.InvalidValue.Format(("label", Labels[RugFieldKeys.TargetSalePrice])));
            return default;
        }

        return new Optional<decimal?>(price);
    }

    /// <summary>اندازهٔ اول نهایی پس از این نوشتن؛ فرش تازه هر دو ضلع را لازم دارد.</summary>
    private static (decimal Length, decimal Width)? PlanIntake(Rug rug, Plan plan, RugWriteContext context, FieldErrors errors)
    {
        var lengthGiven = plan.IntakeLength is not null || errors.Has(RugFieldKeys.IntakeLength);
        var widthGiven = plan.IntakeWidth is not null || errors.Has(RugFieldKeys.IntakeWidth);

        if (context.IsNew)
        {
            if (!lengthGiven) errors.Add(RugFieldKeys.IntakeLength, Messages.Required.Format(("label", "طول")));
            if (!widthGiven) errors.Add(RugFieldKeys.IntakeWidth, Messages.Required.Format(("label", "عرض")));
        }

        if (plan.IntakeLength is null && plan.IntakeWidth is null)
            return null;
        if (errors.Has(RugFieldKeys.IntakeLength) || errors.Has(RugFieldKeys.IntakeWidth))
            return null;

        return (plan.IntakeLength ?? rug.IntakeLength, plan.IntakeWidth ?? rug.IntakeWidth);
    }

    private sealed record FinalPlan(bool Clear, decimal? Length, decimal? Width);

    /// <summary>
    /// اندازهٔ نهایی: هر دو null یعنی «پاک کن»؛ یک ضلع با فرشی که اندازهٔ نهایی دارد ضلع دیگر را نگه می‌دارد؛ یک ضلع روی
    /// فرشی که اندازهٔ نهایی ندارد، یا یکی null و دیگری عدد → «طول و عرض را با هم بنویسید».
    /// </summary>
    private static FinalPlan? PlanFinal(Rug rug, Plan plan, FieldErrors errors)
    {
        if (!plan.FinalLength.HasValue && !plan.FinalWidth.HasValue)
            return null;
        if (errors.Has(RugFieldKeys.FinalLength) || errors.Has(RugFieldKeys.FinalWidth))
            return null;

        var length = plan.FinalLength.HasValue ? plan.FinalLength.Value : rug.FinalLength;
        var width = plan.FinalWidth.HasValue ? plan.FinalWidth.Value : rug.FinalWidth;

        if (length is null && width is null)
            return new FinalPlan(true, null, null);

        if (length is null || width is null)
        {
            errors.Add(plan.FinalLength.HasValue ? RugFieldKeys.FinalLength : RugFieldKeys.FinalWidth, Messages.SizePairRequired.Text);
            return null;
        }

        return new FinalPlan(false, length, width);
    }

    // ═════════════════════════════════════════════════════════════════════
    // فیلدهای سفارشی (Rug.MetadataJson)
    // ═════════════════════════════════════════════════════════════════════

    private sealed record CustomPlan(string? Json, bool Changed, IReadOnlyList<string> ChangedKeys);

    private async Task<CustomPlan> PlanCustomFieldsAsync(
        Rug rug, IReadOnlyDictionary<string, JsonElement?> values, RugWriteContext context, FieldErrors errors,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var definitions = (await customFields.ListAsync(
                f => f.TenantId == tenantId && f.IsActive && f.Scope == CustomFieldScope.Rug, cancellationToken))
            .ToDictionary(f => f.Key, StringComparer.Ordinal);

        var metadata = ParseMetadata(rug.MetadataJson);
        var changedKeys = new List<string>();

        foreach (var (customKey, value) in values)
        {
            var fieldKey = RugFieldKeys.ForCustomField(customKey);
            if (!definitions.TryGetValue(customKey, out var definition))
            {
                errors.Add(fieldKey, Messages.FieldUnknown.Format(("key", fieldKey)));
                continue;
            }

            if (!TryConvertCustom(definition, value, out var stored, out var error))
            {
                errors.Add(fieldKey, error!);
                continue;
            }

            if (stored is null && definition.IsRequired)
            {
                errors.Add(fieldKey, Messages.Required.Format(("label", definition.Label)));
                continue;
            }

            var current = metadata.TryGetPropertyValue(customKey, out var node) ? NodeText(node) : null;
            if (string.Equals(current, stored, StringComparison.Ordinal))
                continue;

            if (stored is null) metadata.Remove(customKey);
            else metadata[customKey] = stored;
            changedKeys.Add(fieldKey);
        }

        var json = metadata.Count == 0 ? null : metadata.ToJsonString();
        return new CustomPlan(json, changedKeys.Count > 0, changedKeys);
    }

    /// <summary>
    /// مقدار یک فیلد سفارشی به شکلی که در متادیتا ذخیره می‌شود (همه رشته، مثل فرم قدیمی): متن یکسان‌سازی‌شده، عدد
    /// ثابت (Invariant)، تاریخ yyyy-MM-dd، گزینه از فهرست، و «true»/«false». خالی → null (کلید حذف می‌شود).
    /// </summary>
    private static bool TryConvertCustom(CustomFieldDefinition definition, JsonElement? value, out string? stored, out string? error)
    {
        stored = null;
        error = null;
        var invalid = Messages.InvalidValue.Format(("label", definition.Label));

        if (IsNull(value) || (value!.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.Value.GetString())))
            return true;

        switch (definition.FieldType)
        {
            case CustomFieldType.Number:
                if (!TryReadDecimal(value, NumberKind.Plain, out var number) || number is null)
                {
                    error = invalid;
                    return false;
                }
                stored = number.Value.ToString(CultureInfo.InvariantCulture);
                return true;

            case CustomFieldType.Boolean:
                if (value.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    stored = value.Value.GetBoolean() ? "true" : "false";
                    return true;
                }
                if (value.Value.ValueKind == JsonValueKind.String && bool.TryParse(value.Value.GetString(), out var flag))
                {
                    stored = flag ? "true" : "false";
                    return true;
                }
                error = invalid;
                return false;

            case CustomFieldType.Date:
                if (value.Value.ValueKind == JsonValueKind.String
                    && DateOnly.TryParseExact(PersianText.Normalize(value.Value.GetString()), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    stored = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    return true;
                }
                error = invalid;
                return false;

            case CustomFieldType.Select:
                if (!TryReadText(value, out var option) || string.IsNullOrWhiteSpace(option))
                {
                    error = invalid;
                    return false;
                }
                var options = ParseOptions(definition.OptionsJson);
                var match = options.FirstOrDefault(o =>
                    string.Equals(PersianText.Normalize(o), PersianText.Normalize(option), StringComparison.Ordinal));
                if (match is null)
                {
                    error = Messages.FieldOption.Format(("value", option.Trim()), ("label", definition.Label));
                    return false;
                }
                stored = match;
                return true;

            default:
                if (!TryReadText(value, out var text))
                {
                    error = invalid;
                    return false;
                }
                var normalized = string.IsNullOrWhiteSpace(text) ? null : PersianText.Normalize(text);
                if (normalized is { Length: > CustomTextMaxLength })
                {
                    error = Messages.TooLong.Format(("label", definition.Label), ("n", CustomTextMaxLength));
                    return false;
                }
                stored = normalized;
                return true;
        }
    }

    private static JsonObject ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            // jsonb پستگرس هرگز JSON خراب ذخیره نمی‌کند؛ این فقط برای دادهٔ دستیِ ناجور است
            return [];
        }
    }

    private static string? NodeText(JsonNode? node) => node switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        _ => node.ToJsonString()
    };

    private static IReadOnlyList<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // خانه‌های خرید (G.2 «Purchase columns»)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>یک تغییر در خانهٔ خرید: سطر همان نوع ساخته یا عوض می‌شود (نرخ و مبنا)، یا پاک می‌شود.</summary>
    private sealed record PurchaseOp(string Key, CostCategory Category, RugPurchaseLine? Existing, bool Delete, decimal Rate, CostBasis Basis);

    private sealed record PurchasePlan(IReadOnlyList<PurchaseOp> Ops)
    {
        public IEnumerable<string> Keys => Ops.Select(o => o.Key);
    }

    private async Task<PurchasePlan> PlanPurchaseAsync(
        Rug rug, IReadOnlyDictionary<string, JsonElement?> values, RugWriteContext context, FieldErrors errors,
        CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var purchaseCategories = await categories.ListAsync(
            c => c.TenantId == tenantId && c.Group == CostGroup.Purchase, cancellationToken);
        var lines = context.IsNew
            ? []
            : await purchaseLines.ListAsync(l => l.RugId == rug.Id && l.TenantId == tenantId, cancellationToken);

        var ops = new List<PurchaseOp>();
        foreach (var (key, value) in values)
        {
            var category = purchaseCategories.FirstOrDefault(c => RugFieldKeys.ForPurchaseCategory(c.Id, c.SeedKey) == key);
            if (category is null || !category.IsActive)
            {
                errors.Add(key, category is null
                    ? Messages.FieldUnknown.Format(("key", key))
                    : Messages.PurchaseCategoryInvalid.Text);
                continue;
            }

            var ofCategory = lines.Where(l => l.CategoryId == category.Id).ToList();
            if (ofCategory.Count > 1)
            {
                // دو سطر یا بیشتر از یک نوع: خانه فقط‌خواندنی است، چون معلوم نیست کدام سطر عوض شود
                errors.Add(key, Messages.PurchaseCellMultiLine.Format(("label", category.Name)));
                continue;
            }

            var existing = ofCategory.SingleOrDefault();
            if (IsNull(value))
            {
                if (existing is not null)
                    ops.Add(new PurchaseOp(key, category, existing, true, 0, existing.Basis));
                continue;
            }

            if (!TryReadPurchaseCell(value, category, existing, out var rate, out var basis))
            {
                errors.Add(key, Messages.InvalidValue.Format(("label", category.Name)));
                continue;
            }

            var problem = CheckRate(rate, basis);
            if (problem is not null)
            {
                errors.Add(key, problem);
                continue;
            }

            ops.Add(new PurchaseOp(key, category, existing, false, rate, basis));
        }

        var added = ops.Count(o => !o.Delete && o.Existing is null);
        var removed = ops.Count(o => o.Delete);
        if (lines.Count + added - removed > MaxPurchaseLines)
            errors.Add(ops.FirstOrDefault()?.Key ?? RugFieldKeys.PurchasePrefix,
                Messages.PurchaseTooManyLines.Format(("n", MaxPurchaseLines)));

        return new PurchasePlan(ops);
    }

    /// <summary>
    /// مقدار خانهٔ خرید: عدد (نرخ؛ مبنا همان سطر فعلی یا پیش‌فرض نوع) یا <c>{rate, basis}</c> (چیپ واحد). نوع درصدی
    /// همیشه درصد است؛ نوع پولی فقط «کل»، «هر متر» یا «هر تخته».
    /// </summary>
    private static bool TryReadPurchaseCell(
        JsonElement? value, CostCategory category, RugPurchaseLine? existing, out decimal rate, out CostBasis basis)
    {
        rate = 0;
        basis = category.DefaultBasis == CostBasis.Percent
            ? CostBasis.Percent
            : existing?.Basis is { } current && current != CostBasis.Percent ? current : category.DefaultBasis;

        JsonElement? rateElement = value;
        if (value is { ValueKind: JsonValueKind.Object } obj)
        {
            rateElement = obj.TryGetProperty("rate", out var r) ? r : null;
            if (obj.TryGetProperty("basis", out var b) && b.ValueKind != JsonValueKind.Null)
            {
                if (!TryReadBasis(b, out var chosen))
                    return false;
                if (category.DefaultBasis == CostBasis.Percent ? chosen != CostBasis.Percent : chosen == CostBasis.Percent)
                    return false;
                basis = chosen;
            }
        }

        if (!TryReadDecimal(rateElement, NumberKind.Money, out var parsed) || parsed is null)
            return false;

        rate = parsed.Value;
        return true;
    }

    private static bool TryReadBasis(JsonElement element, out CostBasis basis)
    {
        basis = CostBasis.Total;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var n) && Enum.IsDefined((CostBasis)n))
        {
            basis = (CostBasis)n;
            return true;
        }

        return element.ValueKind == JsonValueKind.String
               && Enum.TryParse(element.GetString(), ignoreCase: true, out basis)
               && Enum.IsDefined(basis);
    }

    /// <summary>درصد ۰ تا ۱۰۰؛ نرخ متری و دانه‌ای منفی نمی‌شود (سطر «کل» منفی مجاز است، D.3)؛ حداکثر ۴ رقم اعشار.</summary>
    public static string? CheckRate(decimal rate, CostBasis basis)
    {
        if (!Num.HasAtMostDecimals(rate, 4))
            return Messages.PurchaseRateDecimals.Text;
        if (Math.Abs(rate) > MaxRate)
            return Messages.NumberInvalid.Text;
        return basis switch
        {
            CostBasis.Percent when rate is < 0 or > 100 => Messages.PurchasePercentRange.Text,
            CostBasis.PerM2 or CostBasis.PerPiece when rate < 0 => Messages.NegativeNotAllowed.Text,
            _ => null
        };
    }

    private async Task<bool> ApplyPurchaseAsync(
        Rug rug, PurchasePlan plan, RugWriteContext context, CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var op in plan.Ops)
        {
            if (op.Delete)
            {
                var doomed = await TrackedAsync(op.Existing!, cancellationToken);
                purchaseLines.Remove(doomed);
                changed = true;
                continue;
            }

            if (op.Existing is null)
            {
                await purchaseLines.AddAsync(new RugPurchaseLine
                {
                    TenantId = context.TenantId,
                    RugId = rug.Id,
                    CategoryId = op.Category.Id,
                    Label = op.Category.Name,
                    Basis = op.Basis,
                    Rate = op.Rate,
                    SortOrder = op.Category.SortOrder
                }, cancellationToken);
                changed = true;
                continue;
            }

            if (op.Existing.Rate == op.Rate && op.Existing.Basis == op.Basis
                && op.Existing.Label == op.Category.Name && op.Existing.SortOrder == op.Category.SortOrder)
                continue;

            var line = await TrackedAsync(op.Existing, cancellationToken);
            line.Rate = op.Rate;
            line.Basis = op.Basis;
            line.Label = op.Category.Name;
            line.SortOrder = op.Category.SortOrder;
            line.UpdatedAt = context.Now;
            changed = true;
        }

        return changed;
    }

    /// <summary>نسخهٔ ردیابی‌شدهٔ یک سطر (اگر گراف فرش آن را لود کرده، همان نمونه)، تا پاس فقط تغییر واقعی را ببیند.</summary>
    private async Task<RugPurchaseLine> TrackedAsync(RugPurchaseLine snapshot, CancellationToken cancellationToken)
        => await purchaseLines.GetByIdAsync(snapshot.Id, cancellationToken)
           ?? throw new KeyNotFoundException(Messages.PurchaseLineUnknown.Text);

    // ═════════════════════════════════════════════════════════════════════
    // خواندن JSON
    // ═════════════════════════════════════════════════════════════════════

    private enum NumberKind
    {
        /// <summary>«,» ممیز است («2,08»)</summary>
        Dimension,

        /// <summary>«,» و «٬» جداکنندهٔ هزارگان‌اند</summary>
        Money,

        /// <summary>عدد آزاد؛ مثل مبلغ</summary>
        Plain
    }

    internal static bool IsNull(JsonElement? value)
        => value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

    /// <summary>متن یک مقدار: رشته همان، عدد با شکل خامش («3778»)، null همان null. شیء و آرایه نه.</summary>
    internal static bool TryReadText(JsonElement? value, out string? text)
    {
        text = null;
        if (IsNull(value)) return true;
        switch (value!.Value.ValueKind)
        {
            case JsonValueKind.String:
                text = value.Value.GetString();
                return true;
            case JsonValueKind.Number:
                text = value.Value.GetRawText();
                return true;
            default:
                return false;
        }
    }

    private static bool TryReadInt(JsonElement? value, out int? number)
    {
        number = null;
        if (IsNull(value)) return true;
        if (!TryReadDecimal(value, NumberKind.Plain, out var d) || d is null) return false;
        if (decimal.Truncate(d.Value) != d.Value || d.Value is < int.MinValue or > int.MaxValue) return false;
        number = (int)d.Value;
        return true;
    }

    /// <summary>
    /// عدد از JSON (عدد، یا رشته با ارقام فارسی/عربی، «٫» و «/» میان دو رقم به‌جای ممیز). جدول و فرم عدد را خودشان
    /// می‌خوانند (<c>lib/money.ts</c>)؛ این فقط برای کلاینتی است که متن خام می‌فرستد.
    /// </summary>
    private static bool TryReadDecimal(JsonElement? value, NumberKind kind, out decimal? number)
    {
        number = null;
        if (IsNull(value)) return true;
        var element = value!.Value;
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (!element.TryGetDecimal(out var d)) return false;
            number = d;
            return true;
        }

        if (element.ValueKind != JsonValueKind.String) return false;
        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text)) return true;

        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var c in PersianText.Normalize(text))
        {
            switch (c)
            {
                case ' ' or '٬' or '‏' or '‎':
                    continue;
                case ',':
                    if (kind == NumberKind.Dimension) builder.Append('.');
                    continue;
                case '٫' or '/':
                    builder.Append('.');
                    continue;
                default:
                    builder.Append(c);
                    continue;
            }
        }

        if (!decimal.TryParse(builder.ToString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var parsed))
            return false;

        number = parsed;
        return true;
    }

    private static void SetText(
        Optional<string?> planned, Func<string?> current, Action<string?> set, string key, List<string> changed)
    {
        if (!planned.HasValue || string.Equals(planned.Value, current(), StringComparison.Ordinal)) return;
        set(planned.Value);
        changed.Add(key);
    }

    // ═════════════════════════════════════════════════════════════════════
    // ابزار
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>مقداری که «آمده یا نیامده» دارد (null خودش یک مقدار است: «خالی کن»).</summary>
    private readonly record struct Optional<T>(T Value)
    {
        public bool HasValue { get; } = true;
    }

    private sealed class Plan
    {
        public bool SerialGiven { get; set; }
        public SerialNumber? Serial { get; set; }
        public int? Quantity { get; set; }
        public Optional<string?> Title { get; set; }
        public Optional<string?> Origin { get; set; }
        public Optional<string?> Pattern { get; set; }
        public Optional<string?> Material { get; set; }
        public Optional<string?> Notes { get; set; }
        public Optional<int?>? KnotDensity { get; set; }
        public decimal? IntakeLength { get; set; }
        public decimal? IntakeWidth { get; set; }
        public Optional<decimal?> FinalLength { get; set; }
        public Optional<decimal?> FinalWidth { get; set; }
        public Optional<decimal?>? TargetSalePrice { get; set; }
        public Dictionary<string, JsonElement?> Custom { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, JsonElement?> Purchase { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>خطاهای خانه‌ها، به کلید فیلد؛ اولین خطای هر خانه کافی است.</summary>
    private sealed class FieldErrors
    {
        private readonly Dictionary<string, string[]> _errors = new(StringComparer.Ordinal);

        public void Add(string key, string message) => _errors.TryAdd(key, [message]);

        public bool Has(string key) => _errors.ContainsKey(key);

        public void ThrowIfAny()
        {
            if (_errors.Count == 0) return;
            var message = _errors.Count == 1 ? _errors.Values.First()[0] : Messages.ValidationFailed.Text;
            throw new AppValidationException(message, _errors);
        }
    }
}
