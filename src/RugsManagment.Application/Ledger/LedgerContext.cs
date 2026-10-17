using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Ledger;

/// <summary>
/// وضعیت یک پاس برای یک کارگاه (spec D.4): تغییرهای همین کارگاه، اثرشان، و مقدارهایی که مرحله‌ها لازم دارند
/// (تعداد رقم اعشار مبلغ، زمان، نسخهٔ قاعده‌ها). یک ذخیره که به چند کارگاه برسد (seed) چند context دارد؛ قفل هر کارگاه
/// جدا گرفته می‌شود.
///
/// contributorها <see cref="Impact"/> را پر می‌کنند، قفل‌ها (guard) فقط می‌خوانند، و مرحله‌ها هم می‌خوانند و هم
/// مجموعه‌های «انتشار» (<see cref="PurchaseCostChangedRugs"/>) را برای مرحلهٔ بعدی پر می‌کنند.
/// </summary>
public sealed class LedgerContext
{
    public LedgerContext(Guid tenantId, ChangeSet changes, DateTimeOffset now, bool isRebuild = false)
    {
        TenantId = tenantId;
        Changes = changes;
        Now = now;
        IsRebuild = isRebuild;
    }

    public Guid TenantId { get; }

    /// <summary>فقط تغییرهای همین کارگاه.</summary>
    public ChangeSet Changes { get; }

    public LedgerImpact Impact { get; } = new();

    /// <summary>زمان یکسان برای همهٔ ردیف‌های مشتق‌شدهٔ این پاس (<c>ComputedAt</c>، <c>CostAtSaleAt</c>).</summary>
    public DateTimeOffset Now { get; }

    /// <summary>
    /// بازسازی کامل (<c>ILedgerMaintenance.RebuildAsync</c>، بررسی استارت): همهٔ فرش‌ها در اثر هستند، شماره‌ها سنجیده
    /// نمی‌شوند (دادهٔ قدیمی نباید بازسازی را بخواباند) و عکس بهای فروش گرفته نمی‌شود.
    /// </summary>
    public bool IsRebuild { get; }

    /// <summary>تعداد رقم اعشار مبلغ‌های کارگاه (TenantPreferences.MoneyScale؛ بی ردیف ترجیحات ۰). بعد از قفل خوانده می‌شود.</summary>
    public int MoneyScale { get; set; }

    /// <summary>واحد پول کارگاه.</summary>
    public CurrencyUnit Currency { get; set; }

    /// <summary>کف قاعدهٔ «شمارهٔ بعدی» (TenantPreferences.SerialStart)؛ برای <c>nextFree</c> خطای همپوشانی.</summary>
    public int? SerialStart { get; set; }

    /// <summary>شمار ثبت‌های هزینه‌ای که مرحله‌های فاز هزینه‌ها از نو تقسیم کردند (نتیجهٔ بازسازی).</summary>
    public int EntriesRecomputed { get; set; }

    /// <summary>شمار خط‌های محموله‌ای که مرحلهٔ فاز محموله از نو حساب کرد (نتیجهٔ بازسازی).</summary>
    public int LinesRecomputed { get; set; }

    /// <summary>تنظیم‌های خوانده‌شده از دیتابیس را در context می‌گذارد.</summary>
    public void Apply(LedgerTenantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        MoneyScale = settings.MoneyScale;
        Currency = settings.Currency;
        SerialStart = settings.SerialStart;
    }

    /// <summary>
    /// فرش‌هایی که «قیمت خرید» (سطرها + سهم خرید یکجا) در این پاس عوض شد — <c>PurchaseRollupStage</c> پر می‌کند و
    /// مرحلهٔ ثبت‌های «به نسبت قیمت خرید» (فاز هزینه‌ها) می‌خواند (قاعدهٔ انتشار D.4).
    /// </summary>
    public HashSet<Guid> PurchaseCostChangedRugs { get; } = [];

    /// <summary>فرش‌هایی که دلار خطشان عوض شد (فاز محموله پر می‌کند؛ ثبت‌های «به نسبت ارزش اظهاری» می‌خوانند).</summary>
    public HashSet<Guid> LineUsdChangedRugs { get; } = [];

    /// <summary>جای دادهٔ مشترک بین مرحله‌های فازهای بعد، با کلید نوع.</summary>
    public Dictionary<Type, object> Items { get; } = [];

    public override string ToString() => $"tenant {TenantId}: {Impact}";
}
