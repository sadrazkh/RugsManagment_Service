using RugsManagment.Application.DTOs.Rugs;

namespace RugsManagment.Application.Ledger;

/// <summary>
/// پیاده‌سازی <see cref="ILedgerVerifier"/>. هر ردیف مشتق‌شده را با همان CostSqlی که پاس می‌نویسد از نو حساب می‌کند و
/// فرق‌ها را با کدهای <see cref="CostMismatchCheck"/> برمی‌گرداند: ردیف جمع نیست، ستون جمع فرق دارد، مبلغ سطر خرید فرق
/// دارد، نسخهٔ قاعده‌ها کهنه است، یا جفت مؤثر با COALESCE(نهایی، اول) نمی‌خواند (B.4). این سنجش خطای فرمول را نمی‌گیرد (آن
/// کار آزمون برابری C#/SQL است)؛ «جاماندگی» را می‌گیرد: مسیری که از پاس رد نشد یا ردیفی که دستی عوض شد.
/// </summary>
public sealed class LedgerVerifier(
    ILedgerSession session,
    ILedgerRepository repository,
    ICostSourceProvider costSources) : ILedgerVerifier
{
    public async Task<CostConsistencyReportDto> VerifyAsync(
        Guid tenantId, int? sample = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await session.BeginAsync(cancellationToken);
        try
        {
            await session.LockTenantAsync(tenantId, cancellationToken);

            var settings = await repository.GetSettingsAsync(tenantId, cancellationToken);
            var rugIds = await repository.ListRugIdsAsync(tenantId, sample, cancellationToken);
            IReadOnlyList<LedgerMismatchRow> rows = [];
            if (rugIds.Count > 0)
            {
                var sources = await costSources.GetAsync(tenantId, rugIds, cancellationToken);
                rows = await repository.VerifyAsync(
                    tenantId, rugIds, sources, settings.MoneyScale, LedgerVersion.Current, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return new CostConsistencyReportDto(
                rugIds.Count,
                rows.Select(r => new CostMismatchDto(r.RugId, r.Sku, r.SerialNo, r.Check, r.Stored, r.Expected, r.Detail))
                    .ToList());
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
