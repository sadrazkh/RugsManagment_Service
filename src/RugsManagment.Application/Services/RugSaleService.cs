using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Sales;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>ثبت و مدیریت فروش فرش، و گزارش فروش/سود واقعی.</summary>
public interface IRugSaleService
{
    Task<RugSaleDto?> GetForRugAsync(Guid tenantId, Guid rugId, CancellationToken ct = default);

    /// <summary>ثبت یا ویرایش فروش یک فرش. وضعیت فرش به «فروخته‌شده» می‌رود.</summary>
    Task<RugSaleDto> SaveAsync(Guid tenantId, Guid rugId, SaveRugSaleRequest request, CancellationToken ct = default);

    /// <summary>لغو فروش — رکورد حذف و فرش به «آمادهٔ فروش» برمی‌گردد.</summary>
    Task CancelAsync(Guid tenantId, Guid rugId, CancellationToken ct = default);

    /// <summary>
    /// «به‌روزرسانی بهای تمام‌شده» (spec D.6): عکس بهای لحظهٔ فروش (<c>CostAtSale</c>) با بهای امروز فرش جایگزین می‌شود.
    /// ویرایش عادی فروش عکس را نگه می‌دارد؛ فقط همین کار صریح آن را عوض می‌کند (مثلاً وقتی هزینه‌ای که پیش از فروش خرج شده
    /// بود دیرتر ثبت شد و چیپ «هزینه پس از فروش تغییر کرده» دیده می‌شود).
    /// </summary>
    Task<RugSaleDto> RefreshCostAtSaleAsync(Guid tenantId, Guid rugId, CancellationToken ct = default);

    Task<SalesReportDto> GetReportAsync(Guid tenantId, SalesQuery query, CancellationToken ct = default);
}

public sealed class RugSaleService(
    IRepository<RugSale> sales,
    IRugRepository rugs,
    IWorkflowEngine workflowEngine,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    ILedgerLock ledgerLock,
    IRugCostTotalReader costTotals) : IRugSaleService
{
    public async Task<RugSaleDto?> GetForRugAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct);
        return rug?.Sale is null ? null : ToDto(rug, rug.Sale);
    }

    public async Task<RugSaleDto> SaveAsync(
        Guid tenantId, Guid rugId, SaveRugSaleRequest request, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        Validate(request);

        // «تازه بودن» را صریح نگه می‌داریم: BaseEntity شناسه را در سازنده می‌سازد،
        // پس Id هرگز خالی نیست و نمی‌شود از روی آن جدید بودن را تشخیص داد.
        var sale = rug.Sale;
        var isNew = sale is null;

        if (sale is null)
        {
            sale = new RugSale { TenantId = tenantId, RugId = rugId };
            rug.Sale = sale;
            await sales.AddAsync(sale, ct);
        }

        sale.BuyerName = request.BuyerName.Trim();
        sale.BuyerPhone = Clean(request.BuyerPhone);
        sale.SalePrice = request.SalePrice;
        sale.Discount = request.Discount;
        // در فروش نقدی معمولاً همهٔ مبلغ دریافت می‌شود؛ اگر چیزی نیامده بود همان را فرض می‌کنیم
        sale.ReceivedAmount = request.ReceivedAmount ?? (request.SalePrice - request.Discount);
        sale.PaymentMethod = request.PaymentMethod;
        sale.SoldAt = (request.SoldAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        sale.Reference = Clean(request.Reference);
        sale.Notes = Clean(request.Notes);
        sale.UpdatedAt = DateTimeOffset.UtcNow;

        // فروش موجود از rug.Sale ردیابی‌شده می‌آید، پس فقط ستون‌هایی که همین‌جا عوض شدند نوشته می‌شوند. Update() نمی‌زنیم:
        // همهٔ ستون‌ها را «عوض‌شده» می‌کرد و ویرایشی که پیش از یک «به‌روزرسانی بهای تمام‌شده» بارگذاری شده بود عکس بها
        // (CostAtSale/CostAtSaleAt) را با مقدار کهنه رونویسی می‌کرد — بی هیچ ۴۰۹ (فروش xmin ندارد). رکورد تازه هم Added است.

        // ثبت فروش یعنی فرش از چرخهٔ تولید خارج شده (تقدم «فروخته‌شده» در RugStatusResolver). عکس بهای لحظهٔ فروش
        // (CostAtSale) را برای فروش تازه پاس سازگاری در همین ذخیره می‌گیرد؛ ویرایش فروش آن را دست نمی‌زند (D.6).
        RugStatusResolver.ApplyWorkflowStatus(rug, workflowEngine.ResolveStatusFromWorkflow(rug));
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);

        audit.Record(
            AuditAction.SaleRecorded, nameof(Rug), rug.Id,
            isNew
                ? $"به «{sale.BuyerName}» فروخته شد."
                : $"اطلاعات فروش به «{sale.BuyerName}» ویرایش شد.",
            rug.Sku);

        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(rug, sale);
    }

    public async Task CancelAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        var sale = rug.Sale
            ?? throw new InvalidOperationException("برای این فرش فروشی ثبت نشده است.");

        sales.Remove(sale);
        rug.Sale = null;

        // وضعیت از روی مراحل بازمحاسبه می‌شود، نه ثابت «آمادهٔ فروش»:
        // فرشی که هنوز مسیر تولیدش تمام نشده بود نباید بعد از لغو فروش «آماده» به نظر برسد.
        rug.Status = RugStatusResolver.Resolve(hasSale: false, exported: false, workflowEngine.ResolveStatusFromWorkflow(rug));
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);

        audit.Record(AuditAction.SaleCancelled, nameof(Rug), rug.Id,
            $"فروش به «{sale.BuyerName}» لغو شد.", rug.Sku);

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// بهای امروز زیر قفل حساب‌های کارگاه خوانده می‌شود (همان الگوی شمارهٔ خودکار، E2): جمعی که هنگام بارگذاری فرش در حافظه
    /// آمد ممکن است کهنه باشد (مرحله‌ای که هم‌زمان تمام و commit شد)، و تا قفل در دست است هیچ ذخیرهٔ دیگری جمع این کارگاه
    /// را عوض نمی‌کند؛ پس عکس دقیقاً بهای همین لحظه است. فروش از rug.Sale ردیابی‌شده می‌آید؛ Update لازم نیست.
    /// </summary>
    public async Task<RugSaleDto> RefreshCostAtSaleAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        var sale = rug.Sale
            ?? throw new InvalidOperationException("برای این فرش فروشی ثبت نشده است.");

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await ledgerLock.AcquireAsync(tenantId, token);
            costTotals.Invalidate([rug.Id]);
            await costTotals.PreloadAsync([rug.Id], token);

            var now = DateTimeOffset.UtcNow;
            sale.CostAtSale = workflowEngine.CalculateRugCosts(rug).TotalInvestment;
            sale.CostAtSaleAt = now;
            sale.UpdatedAt = now;

            audit.Record(AuditAction.SaleRecorded, nameof(Rug), rug.Id,
                $"بهای تمام‌شدهٔ فروش به «{sale.BuyerName}» با بهای امروز به‌روز شد.", rug.Sku);

            await unitOfWork.SaveChangesAsync(token);
        }, ct);

        return ToDto(rug, sale);
    }

    public async Task<SalesReportDto> GetReportAsync(
        Guid tenantId, SalesQuery query, CancellationToken ct = default)
    {
        query = query.Sanitized();

        var rows = await sales.ListAsync(
            s => s.TenantId == tenantId
                 && (query.From == null || s.SoldAt >= query.From)
                 && (query.To == null || s.SoldAt <= query.To),
            ct);

        // سرمایه‌گذاری هر فرش از موتور هزینه می‌آید تا با بقیهٔ سامانه یکی باشد
        var items = new List<RugSaleDto>();
        foreach (var sale in rows.OrderByDescending(s => s.SoldAt))
        {
            var rug = await rugs.GetWithWorkflowAsync(sale.RugId, tenantId, ct);
            if (rug is null) continue;
            items.Add(ToDto(rug, sale));
        }

        if (query.OnlyOutstanding)
            items = items.Where(i => i.OutstandingAmount > 0).ToList();

        var summary = new SalesSummaryDto(
            items.Count,
            items.Sum(i => i.SalePrice),
            items.Sum(i => i.Discount),
            items.Sum(i => i.NetAmount),
            items.Sum(i => i.ReceivedAmount),
            items.Sum(i => i.OutstandingAmount),
            items.Sum(i => i.TotalInvestment),
            items.Sum(i => i.ActualProfit));

        return new SalesReportDto(summary, items);
    }

    // ─────────────────────────────────────────────────────────

    private static void Validate(SaveRugSaleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BuyerName))
            throw new InvalidOperationException("نام خریدار الزامی است.");

        if (request.SalePrice <= 0)
            throw new InvalidOperationException("مبلغ فروش باید بزرگ‌تر از صفر باشد.");

        if (request.Discount < 0)
            throw new InvalidOperationException("تخفیف نمی‌تواند منفی باشد.");

        if (request.Discount > request.SalePrice)
            throw new InvalidOperationException("تخفیف نمی‌تواند از مبلغ فروش بیشتر باشد.");

        if (request.ReceivedAmount is < 0)
            throw new InvalidOperationException("مبلغ دریافتی نمی‌تواند منفی باشد.");

        if (request.ReceivedAmount > request.SalePrice - request.Discount)
            throw new InvalidOperationException("مبلغ دریافتی نمی‌تواند از مبلغ خالص فروش بیشتر باشد.");
    }

    /// <summary>
    /// سود واقعی = خالص فروش − (عکس بهای لحظهٔ فروش ?? بهای امروز) (D.6). «سرمایه» در گزارش هم همان عددی است که سود از
    /// آن کم شده، تا ستون‌ها با هم بخوانند.
    /// </summary>
    private RugSaleDto ToDto(Rug rug, RugSale sale)
    {
        var costs = workflowEngine.CalculateRugCosts(rug);
        var cost = sale.CostAtSale ?? costs.TotalInvestment;
        return new RugSaleDto(
            sale.Id,
            rug.Id,
            rug.Sku,
            rug.Title,
            sale.BuyerName,
            sale.BuyerPhone,
            sale.SalePrice,
            sale.Discount,
            sale.NetAmount,
            sale.ReceivedAmount,
            sale.OutstandingAmount,
            sale.PaymentMethod,
            sale.SoldAt,
            sale.Reference,
            sale.Notes,
            cost,
            sale.ActualProfit(costs.TotalInvestment));
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : PersianText.Normalize(value);
}
