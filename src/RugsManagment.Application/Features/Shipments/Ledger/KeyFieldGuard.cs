using RugsManagment.Application.Common;
using RugsManagment.Application.Fields;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments.Ledger;

/// <summary>
/// فیلد سفارشی‌ای که «کلید ارزش گمرکی» کارگاه است غیرفعال، حذف، تغییر کلید یا دامنه نمی‌شود (spec D.5 #26) → ۴۰۹. بی این
/// قفل، با یک کلیک در صفحهٔ فیلدهای سفارشی همهٔ خط‌های باز «ندارد» می‌شدند و جلوی قطعی شدن را بی‌دلیلِ پیدا می‌گرفتند. اول
/// باید در ترجیحات کلید دیگری انتخاب شود (اگر همان ذخیره کلید را هم عوض کند، کلید تازه ملاک است).
/// </summary>
public sealed class KeyFieldGuard(IShipmentLedgerRepository repository) : ILedgerGuard
{
    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(CustomFieldDefinition)];

    public async Task CheckAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var risky = context.Changes.Of<CustomFieldDefinition>()
            .Where(c => c.Kind switch
            {
                ChangeKind.Deleted or ChangeKind.Unknown => true,
                ChangeKind.Modified => c.HasAny(nameof(CustomFieldDefinition.Key), nameof(CustomFieldDefinition.Scope))
                                       || (c.Has(nameof(CustomFieldDefinition.IsActive)) && !c.As<CustomFieldDefinition>().IsActive),
                _ => false
            })
            .ToList();
        if (risky.Count == 0)
            return;

        // کلید در همین ذخیره هم ممکن است عوض شود؛ آن‌وقت کلید تازه ملاک است
        var keyField = context.Changes.Of<TenantPreferences>()
            .Select(c => c.As<TenantPreferences>().DeclaredValueKeyField)
            .FirstOrDefault()
            ?? (await repository.SettingsAsync(context.TenantId, cancellationToken)).KeyField;
        if (DeclaredValueMath.CustomKeyOf(keyField) is not { } customKey)
            return;

        foreach (var change in risky)
        {
            var field = change.As<CustomFieldDefinition>();
            var originalKey = change.Kind is ChangeKind.Modified or ChangeKind.Deleted
                ? change.Original<string>(nameof(CustomFieldDefinition.Key)) ?? field.Key
                : field.Key;
            if (!string.Equals(originalKey, customKey, StringComparison.Ordinal))
                continue;

            // Unknown بی تغییر واقعی (همان کلید، فعال، دامنهٔ فرش) را رد نمی‌کنیم
            if (change.Kind == ChangeKind.Unknown && field.IsActive && field.Scope == CustomFieldScope.Rug
                && string.Equals(field.Key, customKey, StringComparison.Ordinal))
                continue;

            throw new DomainConflictException(Messages.DeclaredKeyFieldInUse.Format(("label", field.Label)), "isActive");
        }
    }

    /// <summary>کلید فیلد سفارشی به شکل کلید کاتالوگ (<c>rug.custom.{key}</c>).</summary>
    public static string CatalogKey(string customKey) => RugFieldKeys.ForCustomField(customKey);
}
