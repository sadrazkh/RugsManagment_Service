using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Ledger;

/// <summary>
/// «تفاوت مقدار» (spec D.4 گام ۱): برای هر موجودیت ردیابی‌شده مقدار اصلی و فعلیِ هر ستون با مقایسه‌گر خودِ EF (decimal با
/// مقدار: ۲٫۱۰ = ۲٫۱) سنجیده می‌شود و <c>EntityState.Modified</c>/<c>IsModified</c> کاملاً نادیده گرفته می‌شود؛ چون
/// <c>Repository.Update()</c> قدیمی روی گراف بارگذاری‌شده همه‌چیز را Modified می‌کند. کنار گذاشته می‌شوند: کلید، <c>xmin</c>
/// (توکن هم‌زمانیِ تولیدشده)، <c>UpdatedAt</c>، ستون‌های تولیدشده (<c>AreaM2</c>، …) و ردیف‌های مشتق‌شده
/// (<see cref="LedgerOptions.DerivedTypes"/>). موجودیتِ وصل‌شده بی بارگذاری (<see cref="LedgerTracking"/>) «نامعلوم» است.
///
/// کارگاه هر ردیف: <see cref="ITenantScoped"/>، شناسهٔ خودِ <see cref="Tenant"/>، و برای مرحلهٔ فرش (که کارگاه ندارد) کارگاه
/// فرشش — از فرشِ ردیابی‌شده، وگرنه با یک پرس‌وجو برای همهٔ آن‌ها.
/// </summary>
public sealed class ChangeSetBuilder(AppDbContext db, LedgerTracking tracking, IOptions<LedgerOptions> options)
{
    private static readonly HashSet<string> IgnoredProperties = new(StringComparer.Ordinal) { "xmin", nameof(BaseEntity.UpdatedAt) };

    public async Task<ChangeSet> BuildAsync(CancellationToken cancellationToken)
    {
        db.ChangeTracker.DetectChanges();
        var derived = options.Value.DerivedTypes;

        var pending = new List<(EntityEntry Entry, ChangeKind Kind, Dictionary<string, PropertyChange>? Changes, Dictionary<string, object?>? Originals)>();
        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Detached || IsDerived(entry.Metadata.ClrType, derived))
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    pending.Add((entry, ChangeKind.Added, null, null));
                    break;

                case EntityState.Deleted:
                    pending.Add((entry, ChangeKind.Deleted, null, Originals(entry)));
                    break;

                default:
                    if (tracking.IsUnknown(entry.Entity))
                    {
                        pending.Add((entry, ChangeKind.Unknown, null, Originals(entry)));
                        break;
                    }

                    var (changes, originals) = Diff(entry);
                    if (changes.Count > 0)
                        pending.Add((entry, ChangeKind.Modified, changes, originals));
                    break;
            }
        }

        if (pending.Count == 0)
            return ChangeSet.Empty;

        var stepTenants = await ResolveStepTenantsAsync(pending.Select(p => p.Entry), cancellationToken);
        var result = new List<EntityChange>(pending.Count);
        foreach (var (entry, kind, changes, originals) in pending)
            result.Add(new EntityChange(entry.Entity, kind, TenantOf(entry, stepTenants), changes, originals));

        return new ChangeSet(result);
    }

    private static bool IsDerived(Type type, HashSet<Type> derived) => derived.Any(d => d.IsAssignableFrom(type));

    private static (Dictionary<string, PropertyChange> Changes, Dictionary<string, object?> Originals) Diff(EntityEntry entry)
    {
        var changes = new Dictionary<string, PropertyChange>(StringComparer.Ordinal);
        var originals = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in entry.Properties)
        {
            var metadata = property.Metadata;
            if (IsIgnored(metadata))
                continue;

            var original = property.OriginalValue;
            var current = property.CurrentValue;
            originals[metadata.Name] = original;
            if (!metadata.GetValueComparer().Equals(original, current))
                changes[metadata.Name] = new PropertyChange(metadata.Name, original, current);
        }

        return (changes, originals);
    }

    private static Dictionary<string, object?> Originals(EntityEntry entry)
    {
        var originals = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in entry.Properties)
        {
            if (!IsIgnored(property.Metadata))
                originals[property.Metadata.Name] = property.OriginalValue;
        }

        return originals;
    }

    /// <summary>ستون‌هایی که هرگز «تغییر» به حساب نمی‌آیند.</summary>
    private static bool IsIgnored(IProperty property)
        => property.IsPrimaryKey()
           || IgnoredProperties.Contains(property.Name)
           || property.GetComputedColumnSql() is not null
           || (property.IsConcurrencyToken && property.ValueGenerated == ValueGenerated.OnAddOrUpdate);

    private static Guid? TenantOf(EntityEntry entry, IReadOnlyDictionary<Guid, Guid> stepTenants)
        => entry.Entity switch
        {
            Tenant tenant => tenant.Id,
            ITenantScoped scoped => scoped.TenantId,
            RugWorkflowStep step => stepTenants.TryGetValue(StepRugId(entry, step), out var tenantId) ? tenantId : null,
            _ => null
        };

    /// <summary>برای مرحلهٔ حذف‌شده، RugId از مقدار اصلی (ممکن است فرش هم حذف شده باشد).</summary>
    private static Guid StepRugId(EntityEntry entry, RugWorkflowStep step)
        => entry.State == EntityState.Deleted
            ? (Guid)entry.Property(nameof(RugWorkflowStep.RugId)).OriginalValue!
            : step.RugId;

    /// <summary>کارگاه فرش هر مرحله: از فرش ردیابی‌شده، وگرنه یک پرس‌وجو برای همه (سطل زباله هم).</summary>
    private async Task<IReadOnlyDictionary<Guid, Guid>> ResolveStepTenantsAsync(
        IEnumerable<EntityEntry> entries, CancellationToken cancellationToken)
    {
        var stepRugIds = entries
            .Where(e => e.Entity is RugWorkflowStep)
            .Select(e => StepRugId(e, (RugWorkflowStep)e.Entity))
            .Where(id => id != Guid.Empty)
            .ToHashSet();
        if (stepRugIds.Count == 0)
            return new Dictionary<Guid, Guid>();

        var known = new Dictionary<Guid, Guid>();
        foreach (var rugEntry in db.ChangeTracker.Entries<Rug>())
        {
            if (stepRugIds.Contains(rugEntry.Entity.Id))
                known[rugEntry.Entity.Id] = rugEntry.Entity.TenantId;
        }

        var missing = stepRugIds.Where(id => !known.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            var rows = await db.Rugs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => missing.Contains(r.Id))
                .Select(r => new { r.Id, r.TenantId })
                .ToListAsync(cancellationToken);
            foreach (var row in rows)
                known[row.Id] = row.TenantId;
        }

        return known;
    }
}
