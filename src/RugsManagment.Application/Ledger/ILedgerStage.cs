namespace RugsManagment.Application.Ledger;

/// <summary>
/// «مرحله» (stage): بعد از flush، روی وضعیتِ نوشته‌شده با SQL مجموعه‌ای ردیف‌های مشتق‌شده را می‌سازد (D.4 گام ۷). به ترتیب
/// <see cref="Order"/> اجرا می‌شود که باید یکی از ثابت‌های <see cref="LedgerStageOrder"/> باشد (آزمون معماری). با اثر خالی
/// هیچ مرحله‌ای اجرا نمی‌شود. پرتاب در این‌جا همه‌چیز را برمی‌گرداند (مثلاً شمارهٔ هم‌پوشان).
/// </summary>
public interface ILedgerStage
{
    /// <summary>یکی از ثابت‌های <see cref="LedgerStageOrder"/>.</summary>
    int Order { get; }

    Task RunAsync(LedgerContext context, CancellationToken cancellationToken);
}
