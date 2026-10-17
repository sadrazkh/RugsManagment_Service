using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments.Ledger;

/// <summary>
/// ارزش گمرکی خط‌ها (ترتیب ۳۰۰، spec B.9، C.5، D.4): برای خط‌های اثر (<see cref="LedgerImpact.ShipmentLines"/>) روی وضعیتِ
/// flushشده نرخ را با <see cref="DeclaredValueMath.Resolve"/> پیدا می‌کند (دستی ← جدول ← پیش‌فرض ← ندارد) و ارزش را با قاعدهٔ
/// دسته (<see cref="DeclaredValueMath.Value"/>) می‌سازد؛ فقط ردیف‌هایی که واقعاً عوض می‌شوند نوشته می‌شوند. فرش‌هایی که
/// دلار خطشان عوض شد در <see cref="LedgerContext.LineUsdChangedRugs"/> می‌نشینند تا مرحله‌های «به نسبت ارزش اظهاری» و سهم
/// درآمد (فاز هزینه‌ها) فقط همان‌ها را دوباره ببینند (قاعدهٔ انتشار D.4).
///
/// <para><b>محمولهٔ قطعی منجمد است:</b> خط‌هایش دوباره حساب نمی‌شوند (قفل هم جلوی تغییر ورودی‌هایشان را می‌گیرد)؛ «باز کردن
/// دوباره» اول وضعیت را باز می‌کند و بعد، در همان پاس، همین مرحله آن‌ها را از منبع‌های فعلی حساب می‌کند. در بازسازی، خط‌های
/// باز از نو و خط‌های قطعی فقط اگر ردیفشان نیست حساب می‌شوند.</para>
/// </summary>
public sealed class ShipmentLineValueStage(IShipmentLedgerRepository repository) : ILedgerStage
{
    public int Order => LedgerStageOrder.ShipmentLineValue;

    public async Task RunAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        if (!context.IsRebuild && context.Impact.ShipmentLines.Count == 0)
            return;

        var (settings, rates) = await repository.ValuationSourcesAsync(context.TenantId, cancellationToken);
        var inputs = await repository.LineInputsAsync(
            context.TenantId, context.IsRebuild ? null : context.Impact.ShipmentLines.ToList(), settings.KeyField, cancellationToken);
        if (inputs.Count == 0)
            return;

        var rows = new List<LineValueRow>();
        foreach (var input in inputs)
        {
            var frozen = input.ShipmentStatus == ShipmentStatus.Finalized;
            if (frozen && input.HasValue)
                continue;

            var row = Compute(input, rates, settings.FallbackUsd);
            if (input.HasValue && Same(input, row))
                continue;

            rows.Add(row);
            if (!input.HasValue || input.StoredLineUsd != row.LineUsd)
                context.LineUsdChangedRugs.Add(input.RugId);
        }

        if (rows.Count > 0)
            await repository.WriteValuesAsync(context.TenantId, rows, context.Now, cancellationToken);
        context.LinesRecomputed += rows.Count;
    }

    /// <summary>ارزش یک خط از ورودی‌هایش (خالص؛ همان که آزمون‌ها می‌سنجند).</summary>
    public static LineValueRow Compute(LineValueInput input, IReadOnlyDictionary<string, decimal> rates, decimal? fallback)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (rate, source) = DeclaredValueMath.Resolve(input.UsdPerM2Override, input.KeyValue, rates, fallback);
        var (lineArea, pieceUsd, lineUsd) = DeclaredValueMath.Value(input.PieceAreaM2, input.Quantity, rate);
        return new LineValueRow(input.LineId, input.ShipmentId, rate, source, pieceUsd, lineArea, lineUsd);
    }

    private static bool Same(LineValueInput stored, LineValueRow row)
        => stored.StoredShipmentId == row.ShipmentId
           && stored.StoredUsdPerM2 == row.UsdPerM2
           && stored.StoredSource == row.RateSource
           && stored.StoredPieceUsd == row.PieceUsd
           && stored.StoredLineAreaM2 == row.LineAreaM2
           && stored.StoredLineUsd == row.LineUsd;
}
