namespace RugsManagment.Application.Abstractions;

/// <summary>
/// دادهٔ پیش‌فرضی که هر کارگاه باید داشته باشد (ردیف ترجیحات، دسته‌های هزینهٔ پایه، قالب پیش‌فرض، …).
///
/// چرا این الگو: seed قدیمی «همه یا هیچ» بود (فقط اگر جدول خالی بود) و کارگاه تازه هیچ چیزی نمی‌گرفت.
/// حالا هر ماژول یک contributor ثبت می‌کند و <see cref="ITenantSeedRunner"/> آن را هم هنگام ساخت کارگاه و
/// هم در هر استارت برای همهٔ کارگاه‌ها اجرا می‌کند؛ پس پیش‌فرضِ یک فاز تازه به کارگاه‌های قدیمی هم می‌رسد
/// بی‌آنکه کسی Program.cs یا سرویس کارگاه را ویرایش کند.
///
/// قرارداد:
/// <list type="bullet">
/// <item>idempotent: اگر داده هست (با کلید یا SeedKey) هیچ چیز را بازنویسی نمی‌کند؛ اجرای دوباره بی‌اثر است.</item>
/// <item>فقط کار را در UnitOfWork می‌گذارد (<c>AddAsync</c>) و خودش <c>SaveChangesAsync</c> صدا نمی‌زند؛
/// runner بعد از همهٔ contributorهای یک کارگاه یک بار ذخیره می‌کند.</item>
/// <item>هرگز داده‌ای از کارگاه دیگر نمی‌خواند یا نمی‌نویسد.</item>
/// </list>
/// </summary>
public interface ITenantSeedContributor
{
    /// <summary>ترتیب اجرا میان contributorها؛ کوچک‌تر زودتر (ترجیحات کارگاه: ۱۰۰).</summary>
    int Order { get; }

    /// <summary>داده‌های پیش‌فرضِ نبودهٔ یک کارگاه را اضافه می‌کند.</summary>
    Task SeedAsync(Guid tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// اجراکنندهٔ همهٔ <see cref="ITenantSeedContributor"/>ها. پیاده‌سازی (Infrastructure) هم یک
/// <see cref="IStartupTask"/> با Order = 100 است که در هر استارت همهٔ کارگاه‌ها را پر می‌کند.
/// </summary>
public interface ITenantSeedRunner
{
    /// <summary>
    /// همهٔ contributorها را به ترتیب برای یک کارگاه اجرا و ذخیره می‌کند — از ساخت کارگاه صدا زده می‌شود،
    /// بعد از اینکه ردیف کارگاه ذخیره شد.
    /// </summary>
    Task SeedTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
