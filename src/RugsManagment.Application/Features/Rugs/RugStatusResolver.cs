using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Rugs;

/// <summary>
/// وضعیت فرش (spec C.10): <b>فروخته‌شده &gt; صادر شده &gt; وضعیتِ گردش کار</b>. تنها جای این ترتیب.
///
/// چرا: پیش از این، پیش بردن یک مرحله روی فرشِ فروخته‌شده وضعیتش را به «آمادهٔ فروش» برمی‌گرداند و لغو فروش هر وضعیتی را
/// با وضعیت گردش کار می‌پوشاند؛ فرشی که در محمولهٔ قطعی صادر شده هم با همین راه «در جریان» می‌شد. حالا موتور گردش کار،
/// لغو فروش و (فاز محموله) قطعی/بازکردن محموله همه وضعیت را از همین‌جا می‌گیرند؛ «صادر شده» را گردش کار هرگز
/// بازنویسی نمی‌کند. «بایگانی» دستی است و گردش کار همچنان آن را با وضعیت خودش جایگزین می‌کند (رفتار پیشین).
/// </summary>
public static class RugStatusResolver
{
    /// <summary>
    /// وضعیت نهایی: فروش → «فروخته‌شده»؛ وگرنه صادرشده → «صادر شده»؛ وگرنه وضعیتی که گردش کار می‌گوید.
    /// </summary>
    /// <param name="hasSale">فرش فروش ثبت‌شده دارد.</param>
    /// <param name="exported">فرش در محمولهٔ قطعی است (فاز محموله از خط محموله؛ پیش از آن «وضعیت فعلی صادر شده است»).</param>
    /// <param name="workflowStatus">وضعیتی که مراحل می‌گویند (<see cref="FromWorkflow"/>).</param>
    public static RugStatus Resolve(bool hasSale, bool exported, RugStatus workflowStatus)
    {
        if (hasSale) return RugStatus.Sold;
        if (exported) return RugStatus.Exported;
        return workflowStatus;
    }

    /// <summary>
    /// وضعیتی که فقط از مراحل می‌آید: بی‌مرحله → «ثبت‌شده/پیش‌نویس»، همه بسته (انجام، رد، لغو) → «آمادهٔ فروش»، وگرنه
    /// «در جریان».
    /// </summary>
    public static RugStatus FromWorkflow(Rug rug)
    {
        ArgumentNullException.ThrowIfNull(rug);
        if (rug.WorkflowSteps.Count == 0)
            return RugStatus.Draft;

        var allSettled = rug.WorkflowSteps.All(s =>
            s.Status is WorkflowStepStatus.Completed or WorkflowStepStatus.Skipped or WorkflowStepStatus.Cancelled);
        return allSettled ? RugStatus.ReadyForSale : RugStatus.InProgress;
    }

    /// <summary>
    /// وضعیت فرش را با وضعیتِ پیشنهادیِ گردش کار به‌روز می‌کند، با رعایت تقدم: فرش فروخته‌شده «فروخته‌شده» و فرش
    /// صادرشده «صادر شده» می‌ماند. تا فاز محموله «صادر شده» یعنی وضعیت فعلی همین است (جز محموله کسی آن را نمی‌نویسد).
    /// </summary>
    public static void ApplyWorkflowStatus(Rug rug, RugStatus workflowStatus)
    {
        ArgumentNullException.ThrowIfNull(rug);
        rug.Status = Resolve(rug.Sale is not null, rug.Status == RugStatus.Exported, workflowStatus);
    }
}
