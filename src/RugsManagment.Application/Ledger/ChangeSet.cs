namespace RugsManagment.Application.Ledger;

/// <summary>نوع تغییر یک موجودیت در یک ذخیره.</summary>
public enum ChangeKind
{
    /// <summary>ردیف تازه.</summary>
    Added = 0,

    /// <summary>دست‌کم یک ستون واقعاً عوض شده (تفاوت مقدار، نه پرچم IsModified).</summary>
    Modified = 1,

    /// <summary>ردیف حذف می‌شود (حذف سخت؛ حذف نرمِ فرش یک <see cref="Modified"/> روی <c>DeletedAt</c> است).</summary>
    Deleted = 2,

    /// <summary>
    /// موجودیتی که بی بارگذاریِ ردیابی‌شده به DbContext وصل شده (<c>Update()</c>/<c>Attach</c> روی نمونهٔ جدا): مقدار
    /// اصلی‌اش معلوم نیست، پس تفاوت قابل اعتماد نیست. پاس آن را «همه‌چیز عوض شده» حساب می‌کند (اثر کامل برای فرشش) و
    /// قفل‌ها هر ستون قفل‌دار را با خواندن تازه از دیتابیس می‌سنجند (D.4 گام ۱).
    /// </summary>
    Unknown = 3
}

/// <summary>تغییر یک ستون: مقدار پیش از ذخیره (از دیتابیس) و مقدار تازه.</summary>
public sealed record PropertyChange(string Name, object? Original, object? Current);

/// <summary>
/// یک موجودیتِ عوض‌شده در <see cref="ChangeSet"/>. خودِ نمونه (<see cref="Entity"/>) هم هست تا contributorها مقدار
/// فعلی (مثلاً RugId یک مرحله) را بخوانند؛ برای ردیف حذف‌شده مقدارهای اصلی در <see cref="OriginalValues"/> است.
/// </summary>
public sealed class EntityChange
{
    private readonly IReadOnlyDictionary<string, PropertyChange> _changes;
    private readonly IReadOnlyDictionary<string, object?> _originals;

    public EntityChange(
        object entity,
        ChangeKind kind,
        Guid? tenantId,
        IReadOnlyDictionary<string, PropertyChange>? changes = null,
        IReadOnlyDictionary<string, object?>? originalValues = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Entity = entity;
        EntityType = entity.GetType();
        Kind = kind;
        TenantId = tenantId;
        _changes = changes ?? new Dictionary<string, PropertyChange>(StringComparer.Ordinal);
        _originals = originalValues ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    /// <summary>خودِ موجودیت (همان نمونه‌ای که در DbContext ردیابی می‌شود).</summary>
    public object Entity { get; }

    public Type EntityType { get; }

    public ChangeKind Kind { get; }

    /// <summary>کارگاه این ردیف؛ null برای موجودیت بی‌کارگاه (نوع مرحلهٔ سیستمی، ادمین سیستم).</summary>
    public Guid? TenantId { get; }

    /// <summary>ستون‌هایی که واقعاً عوض شده‌اند (فقط برای <see cref="ChangeKind.Modified"/>).</summary>
    public IReadOnlyDictionary<string, PropertyChange> Changes => _changes;

    /// <summary>مقدارهای اصلی همهٔ ستون‌ها (برای <see cref="ChangeKind.Deleted"/> و <see cref="ChangeKind.Modified"/>).</summary>
    public IReadOnlyDictionary<string, object?> OriginalValues => _originals;

    /// <summary>آیا ستون عوض شده است. برای ردیف تازه، حذف‌شده و «نامعلوم» همیشه true (همه‌چیز در نظر گرفته می‌شود).</summary>
    public bool Has(string property)
        => Kind is ChangeKind.Added or ChangeKind.Deleted or ChangeKind.Unknown || _changes.ContainsKey(property);

    /// <summary>آیا یکی از این ستون‌ها عوض شده است.</summary>
    public bool HasAny(params string[] properties) => properties.Any(Has);

    /// <summary>مقدار اصلی (پیش از این ذخیره) یک ستون؛ برای ردیف تازه default.</summary>
    public T? Original<T>(string property)
        => _originals.TryGetValue(property, out var value) && value is T typed ? typed : default;

    public T As<T>() where T : class => (T)Entity;

    public bool Is<T>() => Entity is T;

    public override string ToString() => $"{EntityType.Name} {Kind} [{string.Join(", ", _changes.Keys)}]";
}

/// <summary>
/// «تفاوت مقدار» یک ذخیره (spec D.4 گام ۱): برای هر موجودیت ردیابی‌شده مقدار اصلی با مقدار فعلی ستون به ستون مقایسه
/// می‌شود و <c>EntityState.Modified</c>/<c>IsModified</c> نادیده گرفته می‌شود، چون <c>Repository.Update()</c> قدیمی همهٔ
/// ستون‌های یک گراف بارگذاری‌شده را Modified می‌کند و آن‌وقت ویرایش یادداشت یک فرش همهٔ هزینه‌ها را از نو حساب می‌کرد.
/// decimal با مقدار مقایسه می‌شود (۲٫۱۰ = ۲٫۱)؛ <c>xmin</c>، <c>UpdatedAt</c> و ستون‌های تولیدشده کنار گذاشته می‌شوند؛
/// ردیف‌های مشتق‌شده (<c>RugCostTotal</c> و مشابه‌های فازهای بعد) اصلاً در آن نیستند تا پاس به خودش برنگردد.
/// این کلاس را <c>ChangeSetBuilder</c> در Infrastructure می‌سازد؛ Application فقط می‌خواند.
/// </summary>
public sealed class ChangeSet
{
    public static readonly ChangeSet Empty = new([]);

    public ChangeSet(IReadOnlyList<EntityChange> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries;
    }

    public IReadOnlyList<EntityChange> Entries { get; }

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>تغییرهای یک نوع موجودیت.</summary>
    public IEnumerable<EntityChange> Of<T>() => Entries.Where(e => e.Entity is T);

    /// <summary>کارگاه‌هایی که این ذخیره به آن‌ها می‌رسد (مرتب، برای گرفتن قفل‌ها به ترتیب ثابت و بی بن‌بست).</summary>
    public IReadOnlyList<Guid> TenantIds
        => Entries.Where(e => e.TenantId is not null).Select(e => e.TenantId!.Value).Distinct().Order().ToList();

    /// <summary>فقط تغییرهای یک کارگاه.</summary>
    public ChangeSet ForTenant(Guid tenantId) => new(Entries.Where(e => e.TenantId == tenantId).ToList());

    public override string ToString() => string.Join("; ", Entries);
}
