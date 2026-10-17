using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments.Ledger;

/// <summary>
/// اثر تغییرهای محموله، نگله، خط و فرش‌های داخل نگله (spec D.5 ردیف‌های ۷، ۱۳–۱۸):
/// <list type="bullet">
/// <item>خط تازه، حذف‌شده، جابه‌جاشده (نگله یا محموله) یا با نرخ دستی تازه → ارزش همان خط (<see cref="LedgerImpact.ShipmentLines"/>)،
/// نگله‌ها و محموله‌های قدیم و تازه (ثبت‌های «این نگله»/«این محموله» فاز هزینه‌ها)، و عضویت فرش. حذف خط ارزشش را با cascade
/// می‌برد، پس فرشش مستقیم در «دلار خط عوض شد» می‌نشیند (ثبت‌های «به نسبت ارزش اظهاری»: فرش بی‌خط وزن صفر دارد).</item>
/// <item>«باز کردن دوباره» (قطعی ← باز) → همهٔ خط‌های محموله از منبع‌های فعلی دوباره ارزش‌گذاری می‌شوند (C.9، D.5 #16).
/// «قطعی» ارزش‌ها را منجمد می‌کند و فقط محموله را در اثر می‌گذارد.</item>
/// <item>نرخ دلار و درآمد محموله، حذف نگله یا محموله → محموله و نگله در اثر (سهم درآمد و هدف‌های cascadeشدهٔ فاز هزینه‌ها).</item>
/// <item>فرشی که اندازه، تعداد یا یکی از فیلدهای ممکنِ کلید (محل بافت، نقشه، مواد، عنوان، فیلدهای سفارشی) را عوض کرده و
/// در محمولهٔ باز است → ارزش خطش (D.5 #2b، #3، #7). یک پرس‌وجوی ایندکس‌دار بعد از قفل.</item>
/// </list>
/// شمارهٔ نگله، وزن، یادداشت، ترتیب و سربرگ محموله اثری ندارند (فقط قفل).
/// </summary>
public sealed class ShipmentImpactContributor(IShipmentLedgerRepository repository) : IChangeImpactContributor
{
    /// <summary>ستون‌های فرش که ارزش خط را عوض می‌کنند (اندازه، تعداد، فیلدهای ممکنِ کلید).</summary>
    public static readonly string[] ValueFields =
    [
        nameof(Rug.Quantity), nameof(Rug.IntakeLength), nameof(Rug.IntakeWidth), nameof(Rug.FinalLength), nameof(Rug.FinalWidth),
        nameof(Rug.LengthMeters), nameof(Rug.WidthMeters),
        nameof(Rug.Origin), nameof(Rug.Pattern), nameof(Rug.Material), nameof(Rug.Title), nameof(Rug.MetadataJson)
    ];

    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(ShipmentLine), typeof(Bale), typeof(Shipment), typeof(Rug)];

    public async Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var impact = context.Impact;

        foreach (var change in context.Changes.Of<ShipmentLine>())
        {
            var line = change.As<ShipmentLine>();
            switch (change.Kind)
            {
                case ChangeKind.Added:
                case ChangeKind.Unknown:
                    impact.ShipmentLines.Add(line.Id);
                    impact.Shipments.Add(line.ShipmentId);
                    impact.Bales.Add(line.BaleId);
                    impact.MemberRugs.Add(line.RugId);
                    if (change.Kind == ChangeKind.Unknown)
                        context.LineUsdChangedRugs.Add(line.RugId);
                    break;

                case ChangeKind.Deleted:
                    var rugId = change.Original<Guid>(nameof(ShipmentLine.RugId));
                    impact.Shipments.Add(change.Original<Guid>(nameof(ShipmentLine.ShipmentId)));
                    impact.Bales.Add(change.Original<Guid>(nameof(ShipmentLine.BaleId)));
                    impact.MemberRugs.Add(rugId);
                    context.LineUsdChangedRugs.Add(rugId);
                    break;

                case ChangeKind.Modified:
                    if (change.HasAny(nameof(ShipmentLine.BaleId), nameof(ShipmentLine.ShipmentId), nameof(ShipmentLine.RugId)))
                    {
                        impact.ShipmentLines.Add(line.Id);
                        impact.Bales.Add(change.Original<Guid>(nameof(ShipmentLine.BaleId)));
                        impact.Bales.Add(line.BaleId);
                        impact.Shipments.Add(change.Original<Guid>(nameof(ShipmentLine.ShipmentId)));
                        impact.Shipments.Add(line.ShipmentId);
                        impact.MemberRugs.Add(line.RugId);
                        if (change.Has(nameof(ShipmentLine.RugId)))
                        {
                            var oldRug = change.Original<Guid>(nameof(ShipmentLine.RugId));
                            impact.MemberRugs.Add(oldRug);
                            context.LineUsdChangedRugs.Add(oldRug);
                        }
                    }

                    if (change.Has(nameof(ShipmentLine.UsdPerM2Override)))
                    {
                        impact.ShipmentLines.Add(line.Id);
                        impact.Shipments.Add(line.ShipmentId);
                    }

                    break;
            }
        }

        foreach (var change in context.Changes.Of<Bale>())
        {
            if (change.Kind != ChangeKind.Deleted) continue;
            impact.Bales.Add(change.As<Bale>().Id);
            impact.Shipments.Add(change.Original<Guid>(nameof(Bale.ShipmentId)));
        }

        var reopened = new List<Guid>();
        foreach (var change in context.Changes.Of<Shipment>())
        {
            var shipment = change.As<Shipment>();
            switch (change.Kind)
            {
                case ChangeKind.Deleted:
                    impact.Shipments.Add(shipment.Id);
                    break;

                case ChangeKind.Unknown:
                    impact.Shipments.Add(shipment.Id);
                    if (shipment.Status == ShipmentStatus.Open) reopened.Add(shipment.Id);
                    break;

                case ChangeKind.Modified:
                    if (change.Has(nameof(Shipment.Status)))
                    {
                        impact.Shipments.Add(shipment.Id);
                        if (change.Original<ShipmentStatus>(nameof(Shipment.Status)) == ShipmentStatus.Finalized
                            && shipment.Status == ShipmentStatus.Open)
                            reopened.Add(shipment.Id);
                    }

                    if (change.HasAny(nameof(Shipment.UsdRate), nameof(Shipment.RevenueAmount),
                            nameof(Shipment.RevenueCurrency), nameof(Shipment.RevenueFx)))
                        impact.Shipments.Add(shipment.Id);
                    break;
            }
        }

        var valueRugs = context.Changes.Of<Rug>()
            .Where(c => c.Kind is ChangeKind.Modified or ChangeKind.Unknown && c.HasAny(ValueFields))
            .Select(c => c.As<Rug>().Id)
            .ToHashSet();

        if (reopened.Count == 0 && valueRugs.Count == 0)
            return;

        // همهٔ خواندن‌ها بعد از قفل و پیش از flush (D.4 گام ۴)؛ خط فرش‌ها در حافظهٔ پاس می‌ماند تا قفل همان را دوباره نپرسد
        var lines = new List<LinePlacement>();
        if (reopened.Count > 0)
        {
            var ofShipments = await repository.LinesAsync(context.TenantId, null, null, reopened, cancellationToken);
            RugLineCache.Remember(context, ofShipments);
            lines.AddRange(ofShipments);
        }

        if (valueRugs.Count > 0)
            lines.AddRange((await RugLineCache.LinesOfRugsAsync(context, repository, valueRugs, cancellationToken)).Values);

        foreach (var line in lines)
        {
            var isReopened = reopened.Contains(line.ShipmentId);
            // محمولهٔ قطعی منجمد است؛ قفل جلوی تغییر فرشش را می‌گیرد، این‌جا فقط چیزی برای حساب گذاشته نمی‌شود
            if (!isReopened && line.Status != ShipmentStatus.Open)
                continue;

            impact.ShipmentLines.Add(line.LineId);
            impact.Shipments.Add(line.ShipmentId);
        }
    }
}
