using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// خلاصهٔ آخرین تقسیم یک ثبت هزینه (spec B.10، مشتق‌شده): چند ردیف و تخته و متر، جمع وزن، چقدر تقسیم شد و چقدر ماند، وضع
/// و هشدارها. فقط مرحله‌های پاس سازگاری می‌نویسند؛ هر اجرا همهٔ هشدارها را از نو حساب می‌کند (D.4)، پس
/// <see cref="WarningsJson"/> هیچ‌وقت کهنه نیست. xmin ندارد.
///
/// <para><see cref="BaseAmount"/> (افزوده به B.10): کلی که همین اجرا تقسیم کرد. برای «کل» و رسید منجمد همان
/// <c>CostEntry.BaseAmount</c> است؛ برای ثبت زندهٔ «هر متر»/«هر تخته» کلِ فعلی Σ روی فرش‌های امروز — بی آن، ناوردای
/// «تقسیم‌شده + تقسیم‌نشده = کل» برای ثبت زنده بعد از تغییر اندازه سنجیدنی نبود (و نوشتن کل زنده در خودِ ثبت، نسخهٔ
/// ثبت را با هر تغییر اندازهٔ فرش عوض می‌کرد).</para>
/// </summary>
public class CostEntryResult : ITenantScoped
{
    public Guid EntryId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>شمار فرش‌های عضو (ردیف‌ها؛ یک دسته یک ردیف).</summary>
    public int Lines { get; set; }

    /// <summary>جمع تعداد تخته‌ها.</summary>
    public int Pieces { get; set; }

    /// <summary>جمع متراژ در پایهٔ متراژ ثبت (numeric(12,2)).</summary>
    public decimal AreaM2 { get; set; }

    /// <summary>جمع وزن فرش‌های بی مبلغ ثابت (bigint).</summary>
    public long TotalWeightUnits { get; set; }

    /// <summary>کلی که این اجرا تقسیم کرد، به ارز کارگاه (numeric(18,2)).</summary>
    public decimal BaseAmount { get; set; }

    /// <summary>جمع سهم‌ها (numeric(18,2)).</summary>
    public decimal Allocated { get; set; }

    /// <summary>باقی‌ماندهٔ بی‌صاحب = BaseAmount − Allocated (numeric(18,2)).</summary>
    public decimal Unallocated { get; set; }

    public AllocationState State { get; set; }

    /// <summary>هشدارها: آرایهٔ JSON از <c>{code, count, amount?, serial?}</c> (jsonb)؛ پیام فارسی هنگام خواندن ساخته می‌شود.</summary>
    public string WarningsJson { get; set; } = "[]";

    public DateTimeOffset ComputedAt { get; set; }

    public CostEntry Entry { get; set; } = null!;
}
