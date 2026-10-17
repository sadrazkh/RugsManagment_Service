using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>جدول ارزش گمرکی (spec C.5، I.2).</summary>
public interface IDeclaredValueService
{
    /// <summary>جدول با مهر (Floor).</summary>
    Task<DeclaredValuesDto> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// افزودن کلیدی که نیست (Business): کلید موجود (بعد از KeyNormalize) ۴۰۹، نرخ منفی ۴۰۰، مهر کهنه ۴۰۹. خط‌های بازِ
    /// بی نرخ دستی با همین کلید در همان ذخیره دوباره ارزش‌گذاری می‌شوند.
    /// </summary>
    Task<DeclaredValuesWriteResultDto> AddAsync(Guid tenantId, AddDeclaredValueRequest request, CancellationToken cancellationToken = default);

    /// <summary>جایگزینی کامل (Configure)؛ مهر کهنه → <see cref="StaleStampException"/> (۴۰۹).</summary>
    Task<DeclaredValuesWriteResultDto> ReplaceAsync(Guid tenantId, ReplaceDeclaredValuesRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// جدول ارزش گمرکی. هر نوشتن مهر کارگاه (<c>TenantPreferences.DeclaredValuesStamp</c>) را یکی بالا می‌برد؛ ردیف ترجیحات
/// xmin دارد، پس دو نوشتنِ هم‌زمان با یک مهر دقیقاً یک ۴۰۹ می‌گیرند حتی اگر هر دو مهر درست را دیده باشند. ارزش‌گذاری
/// دوبارهٔ خط‌ها کار <c>DeclaredValueImpactContributor</c> و مرحلهٔ ارزش خط در همان پاس است؛ این‌جا فقط شمردن برای پاسخ.
/// </summary>
public sealed class DeclaredValueService(
    IShipmentRepository shipments,
    ITenantPreferencesRepository preferences,
    IShipmentSettingsReader settingsReader,
    IUnitOfWork unitOfWork,
    IAuditLog audit) : IDeclaredValueService
{
    /// <summary>بزرگ‌ترین نرخ (numeric(14,2)).</summary>
    public const decimal MaxUsdPerM2 = 999_999_999_999.99m;

    public async Task<DeclaredValuesDto> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var settings = await settingsReader.GetAsync(tenantId, cancellationToken);
        var rates = await shipments.ListRatesAsync(tenantId, cancellationToken);
        return new DeclaredValuesDto(
            rates.Select(r => new DeclaredValueRateDto(r.Id, r.KeyValue, r.UsdPerM2)).ToList(),
            settings.Preferences.DeclaredValuesStamp,
            settings.KeyField,
            settings.FallbackUsd);
    }

    public async Task<DeclaredValuesWriteResultDto> AddAsync(
        Guid tenantId, AddDeclaredValueRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (keyValue, normalized) = ValidateKey(request.KeyValue, "keyValue");
        ValidateRate(request.UsdPerM2, "usdPerM2");

        var (tracked, settings) = await LoadStampedAsync(tenantId, request.Stamp, cancellationToken);
        var rates = await shipments.ListRatesAsync(tenantId, cancellationToken);
        if (rates.Any(r => string.Equals(r.KeyNormalized, normalized, StringComparison.Ordinal)))
            throw new DomainConflictException(Messages.DeclaredKeyExists.Format(("key", keyValue)), "keyValue");

        await shipments.AddRateAsync(new DeclaredValueRate
        {
            TenantId = tenantId, KeyValue = keyValue, KeyNormalized = normalized, UsdPerM2 = request.UsdPerM2
        }, cancellationToken);
        tracked.DeclaredValuesStamp++;

        var affected = await CountAffectedAsync(tenantId, settings.KeyField, [normalized], cancellationToken);
        audit.Record(AuditAction.DeclaredValuesChanged, nameof(DeclaredValueRate), null,
            Messages.DeclaredAdded.Format(("key", keyValue), ("n", affected)), keyValue,
            new { added = new { key = keyValue, usdPerM2 = request.UsdPerM2 }, affectedOpenLines = affected });

        await SaveAsync(tracked, cancellationToken);
        return new DeclaredValuesWriteResultDto(affected, tracked.DeclaredValuesStamp);
    }

    public async Task<DeclaredValuesWriteResultDto> ReplaceAsync(
        Guid tenantId, ReplaceDeclaredValuesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var incoming = ValidateTable(request.Rates ?? []);

        var (tracked, settings) = await LoadStampedAsync(tenantId, request.Stamp, cancellationToken);
        var existing = await shipments.ListRatesTrackedAsync(tenantId, cancellationToken);
        var byKey = existing.ToDictionary(r => r.KeyNormalized, StringComparer.Ordinal);
        var changedKeys = new HashSet<string>(StringComparer.Ordinal);
        var added = 0;
        var updated = 0;
        var removed = 0;

        foreach (var (keyValue, normalized, rate) in incoming)
        {
            if (byKey.Remove(normalized, out var row))
            {
                if (row.UsdPerM2 != rate || !string.Equals(row.KeyValue, keyValue, StringComparison.Ordinal))
                {
                    if (row.UsdPerM2 != rate) changedKeys.Add(normalized);
                    row.KeyValue = keyValue;
                    row.UsdPerM2 = rate;
                    row.UpdatedAt = DateTimeOffset.UtcNow;
                    updated++;
                }

                continue;
            }

            await shipments.AddRateAsync(new DeclaredValueRate
            {
                TenantId = tenantId, KeyValue = keyValue, KeyNormalized = normalized, UsdPerM2 = rate
            }, cancellationToken);
            changedKeys.Add(normalized);
            added++;
        }

        foreach (var gone in byKey.Values)
        {
            shipments.RemoveRate(gone);
            changedKeys.Add(gone.KeyNormalized);
            removed++;
        }

        tracked.DeclaredValuesStamp++;
        var affected = changedKeys.Count == 0
            ? 0
            : await CountAffectedAsync(tenantId, settings.KeyField, changedKeys, cancellationToken);
        audit.Record(AuditAction.DeclaredValuesChanged, nameof(DeclaredValueRate), null,
            $"جدول ارزش گمرکی ذخیره شد: {Messages.FormatNumber(added)} تازه، {Messages.FormatNumber(updated)} ویرایش، " +
            $"{Messages.FormatNumber(removed)} حذف؛ {Messages.FormatNumber(affected)} ردیف دوباره حساب شد.",
            null, new { added, updated, removed, affectedOpenLines = affected });

        await SaveAsync(tracked, cancellationToken);
        return new DeclaredValuesWriteResultDto(affected, tracked.DeclaredValuesStamp);
    }

    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ردیف ترجیحات ردیابی‌شده با نسخهٔ همان که مهرش سنجیده شد (xmin مسلح)؛ مهر کهنه → <see cref="StaleStampException"/>.
    /// </summary>
    private async Task<(TenantPreferences Tracked, ShipmentSettings Settings)> LoadStampedAsync(
        Guid tenantId, int stamp, CancellationToken cancellationToken)
    {
        var settings = await settingsReader.GetAsync(tenantId, cancellationToken);
        if (settings.Preferences.DeclaredValuesStamp != stamp)
            throw new StaleStampException(settings.Preferences.DeclaredValuesStamp);

        var tracked = await preferences.GetForUpdateAsync(tenantId, settings.Version, cancellationToken);
        if (tracked is null)
        {
            // کارگاهِ بی‌ردیف ترجیحات (نباید پیش بیاید؛ seed می‌سازد): همین‌جا با پیش‌فرض‌ها ساخته می‌شود
            tracked = TenantPreferences.CreateDefault(tenantId);
            await preferences.AddAsync(tracked, cancellationToken);
        }
        else if (tracked.DeclaredValuesStamp != stamp)
        {
            throw new StaleStampException(tracked.DeclaredValuesStamp);
        }

        return (tracked, settings);
    }

    /// <summary>ذخیره؛ تعارض نسخهٔ ردیف ترجیحات یعنی کس دیگری همین حالا جدول یا ترجیحات را نوشت → مهر کهنه.</summary>
    private async Task SaveAsync(TenantPreferences tracked, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            throw new StaleStampException(tracked.DeclaredValuesStamp - 1);
        }
        catch (DuplicateKeyException)
        {
            // کلید یکتا (TenantId, KeyNormalized): افزودنِ هم‌زمانِ همان کلید
            throw new StaleStampException(tracked.DeclaredValuesStamp - 1);
        }
    }

    /// <summary>چند خط محمولهٔ باز بی نرخ دستی کلیدی از این مجموعه دارند (همان که پاس دوباره حساب می‌کند).</summary>
    private async Task<int> CountAffectedAsync(
        Guid tenantId, string keyField, IReadOnlyCollection<string> normalizedKeys, CancellationToken cancellationToken)
    {
        var keys = normalizedKeys as IReadOnlySet<string> ?? normalizedKeys.ToHashSet(StringComparer.Ordinal);
        var values = await shipments.OpenLineKeyValuesAsync(tenantId, keyField, cancellationToken);
        return values.Count(v => keys.Contains(DeclaredValueMath.KeyNormalize(v)));
    }

    private static (string KeyValue, string Normalized) ValidateKey(string? keyValue, string field)
    {
        var display = ShipmentService.Clean(keyValue is null ? null : PersianText.Normalize(keyValue));
        if (display is null)
            throw AppValidationException.ForField(field, Messages.DeclaredKeyRequired.Text);
        if (display.Length > DeclaredValueRate.KeyMaxLength)
            throw AppValidationException.ForField(field, Messages.DeclaredKeyTooLong.Format(("n", DeclaredValueRate.KeyMaxLength)));
        return (display, DeclaredValueMath.KeyNormalize(display));
    }

    private static void ValidateRate(decimal rate, string field)
    {
        if (rate < 0)
            throw AppValidationException.ForField(field, Messages.DeclaredNegative.Text);
        if (rate > MaxUsdPerM2 || !Num.HasAtMostDecimals(rate, 2))
            throw AppValidationException.ForField(field,
                Messages.OutOfRange.Format(("label", "دلار هر متر"), ("min", 0), ("max", MaxUsdPerM2)));
    }

    /// <summary>همهٔ ردیف‌ها سنجیده می‌شوند و همهٔ خطاها با هم برمی‌گردند (<c>rates[2].usdPerM2</c>).</summary>
    private static IReadOnlyList<(string KeyValue, string Normalized, decimal Rate)> ValidateTable(IReadOnlyList<DeclaredValueRateDto> rates)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var result = new List<(string, string, decimal)>(rates.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < rates.Count; i++)
        {
            var rate = rates[i];
            try
            {
                var (display, normalized) = ValidateKey(rate.KeyValue, $"rates[{i}].keyValue");
                ValidateRate(rate.UsdPerM2, $"rates[{i}].usdPerM2");
                if (!seen.Add(normalized))
                {
                    errors[$"rates[{i}].keyValue"] = [Messages.DeclaredKeyDuplicate.Format(("key", display))];
                    continue;
                }

                result.Add((display, normalized, rate.UsdPerM2));
            }
            catch (AppValidationException ex)
            {
                foreach (var (key, messages) in ex.Errors)
                    errors[key] = messages;
            }
        }

        if (errors.Count > 0)
            throw new AppValidationException(errors.Values.First()[0], errors);
        return result;
    }
}
