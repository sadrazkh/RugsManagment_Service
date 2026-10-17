using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>
/// پرسش‌هایی که فرش‌های سطل زباله را هم باید ببینند. چرا جدا: <see cref="IRepository{T}"/> فیلتر حذف نرم را دارد
/// (مرحله و سطر خریدِ فرش حذف‌شده دیده نمی‌شود)، و این دو جا دقیقاً همان ردیف‌ها را لازم دارند — فرش سطل زباله با یک
/// کلیک برمی‌گردد و باید همان مراحل و همان قیمت خرید را داشته باشد.
/// </summary>
public interface ITrashAwareQueries
{
    /// <summary>
    /// آیا مرحله‌ای از این نوع روی فرشی هست، حتی فرش سطل زباله؟ حذف فیزیکی نوع مرحله با کلید خارجی cascade مراحل آن
    /// فرش را بی پاس سازگاری پاک می‌کرد و بهای ذخیره‌شده‌اش کهنه می‌ماند؛ پس نوعِ در حال استفاده فقط غیرفعال می‌شود.
    /// </summary>
    Task<bool> IsStepTypeUsedAsync(Guid processStepTypeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// سطرهای «قیمت خرید» بی‌نوعِ ساختهٔ M2 (<see cref="RugPurchaseLine.LegacyPurchaseLabel"/>، <c>CategoryId</c> null) در
    /// این کارگاه، فرش‌های سطل زباله هم، <b>ردیابی‌شده</b> تا وصل کردنشان به نوع از پاس سازگاری بگذرد.
    /// </summary>
    Task<IReadOnlyList<RugPurchaseLine>> TrackUnlinkedLegacyPurchaseLinesAsync(
        Guid tenantId, CancellationToken cancellationToken = default);
}
