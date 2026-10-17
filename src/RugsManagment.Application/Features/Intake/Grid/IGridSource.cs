using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.DTOs.Rugs;

namespace RugsManagment.Application.Features.Intake.Grid;

/// <summary>بیننده و کارگاهِ یک جدول: ستون‌های پولی فقط برای Business، برچسب‌های نگله از واژه‌های کارگاه.</summary>
/// <param name="UserId">کاربر جاری (قاعدهٔ حذف انباردار)؛ null برای کار سیستمی.</param>
public sealed record GridViewer(Guid TenantId, Guid? UserId, bool CanSeeMoney, BaleTermWords Terms);

/// <summary>
/// پارامترهای <c>GET /api/grid/{gridId}</c>: روز و ورودی (جدول ورود)، محموله (جدول نگله‌ها)، برش ردیف‌ها و فیلترهای
/// فهرست فرش‌ها (جدول انبار). هر منبع فقط آنچه لازم دارد را می‌خواند.
/// </summary>
public sealed record GridLoadRequest(
    DateOnly? Date,
    Guid? BatchId,
    Guid? ShipmentId,
    int? Skip,
    int? Take,
    RugQuery? Query);

/// <summary>
/// یک جدول با داده‌اش (spec G.1، K.1 «درز فاز ۳»): <c>intake</c> و <c>rugs</c> در فاز ۳، <c>bales</c> را فاز نگله‌بندی با
/// یک کلاس تازه اضافه می‌کند (<c>BalesGridSource</c>). <c>GET /api/grid/{gridId}</c> و تنظیم ستون‌ها
/// (<c>GridLayoutService</c>) هر دو به همین شناسه به منبع می‌رسند؛ شناسهٔ ناشناخته → ۴۰۴.
///
/// قرارداد: منبع ستون‌های پیش‌فرض خودش را می‌دهد (G.2؛ برای غیر Business بی ستون پولی) و ردیف‌ها را با ستون‌های مؤثر
/// (بعد از چیدمان کارگاه) می‌سازد. نوشتن ردیف‌های فرش همیشه از <c>POST /api/grid/rugs/batch</c> و
/// <see cref="IRugGridService"/> است، هر منبعی که <see cref="EditsRugs"/> دارد.
/// </summary>
public interface IGridSource
{
    /// <summary>شناسهٔ جدول (<c>intake</c>، <c>rugs</c>، <c>bales</c>).</summary>
    string GridId { get; }

    /// <summary>برچسب فارسی جدول (صفحهٔ تنظیم ستون‌ها و تاریخچه).</summary>
    string Title { get; }

    /// <summary>ردیف‌هایش فرش‌اند و از <c>POST /api/grid/rugs/batch</c> نوشته می‌شوند.</summary>
    bool EditsRugs { get; }

    /// <summary>ستون‌های پیش‌فرض این جدول برای این بیننده، به ترتیب G.2.</summary>
    Task<IReadOnlyList<GridColumnDto>> DefaultColumnsAsync(GridViewer viewer, CancellationToken cancellationToken);

    /// <summary>
    /// داده‌های جدول با ستون‌های مؤثر (<paramref name="columns"/>؛ ترتیب و پنهان‌بودن از چیدمان کارگاه).
    /// <paramref name="allColumns"/> همهٔ ستون‌های پیش‌فرض است تا مقدار خانه‌های پنهان هم در ردیف بیاید.
    /// </summary>
    Task<GridDataDto> LoadAsync(
        GridViewer viewer, GridLoadRequest request, IReadOnlyList<GridColumnDto> columns,
        IReadOnlyList<GridColumnDto> allColumns, CancellationToken cancellationToken);
}
