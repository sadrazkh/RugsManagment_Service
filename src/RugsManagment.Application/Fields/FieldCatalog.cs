using RugsManagment.Application.Common;

namespace RugsManagment.Application.Fields;

/// <summary>
/// جمع همهٔ <see cref="IFieldCatalogContributor"/>ها (spec E.3).
///
/// پنهان‌سازی پول این‌جاست، نه در هر contributor: فیلدی با <see cref="FieldDefDto.Money"/> = true برای کسی که Business
/// نیست اصلاً در فهرست نمی‌آید (نه با برچسب و نه بی‌مقدار)، تا انباردار حتی نام ستون‌های پولی را هم نبیند و جدولش ستون
/// خالی نداشته باشد. کلید تکراری را contributor با Order کوچک‌تر نگه می‌دارد (فاز بعد نمی‌تواند معنای کلیدی را که
/// پیش‌تر تعریف شده بی‌صدا عوض کند).
/// </summary>
public sealed class FieldCatalog(
    IEnumerable<IFieldCatalogContributor> contributors,
    IMoneyVisibility money) : IFieldCatalog
{
    public async Task<IReadOnlyList<FieldDefDto>> ListAsync(
        Guid tenantId, string purpose, string? kind, CancellationToken cancellationToken = default)
    {
        var request = new FieldCatalogRequest(tenantId, purpose, string.IsNullOrWhiteSpace(kind) ? null : kind.Trim());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<FieldDefDto>();

        // پشت سر هم، نه موازی: contributorها یک DbContext اسکوپ‌شده را share می‌کنند
        foreach (var contributor in contributors.OrderBy(c => c.Order).ThenBy(c => c.GetType().FullName, StringComparer.Ordinal))
        {
            foreach (var field in await contributor.FieldsAsync(request, cancellationToken))
            {
                if (field.Money && !money.CanSeeMoney)
                    continue;
                if (seen.Add(field.Key))
                    result.Add(field);
            }
        }

        return result;
    }
}
