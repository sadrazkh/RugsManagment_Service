using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Configurations;

public sealed class CostEntryConfiguration : IEntityTypeConfiguration<CostEntry>
{
    public void Configure(EntityTypeBuilder<CostEntry> b)
    {
        b.ToTable("CostEntries");
        b.Ignore(e => e.IsLive); b.Ignore(e => e.IsFrozen); b.Ignore(e => e.IsReceipt);
        b.Property(e => e.Description).HasMaxLength(CostEntry.DescriptionMaxLength);
        b.Property(e => e.ReceiptNo).HasMaxLength(CostEntry.ReceiptNoMaxLength);
        b.Property(e => e.ScopeNote).HasMaxLength(CostEntry.ScopeNoteMaxLength);
        b.Property(e => e.Rate).HasPrecision(18, 4);
        b.Property(e => e.Amount).HasPrecision(18, 2);
        b.Property(e => e.BaseAmount).HasPrecision(18, 2);
        b.Property(e => e.FxRate).HasPrecision(18, 6);
        b.Property<uint>("xmin").IsRowVersion();
        b.HasIndex(e => new { e.TenantId, e.Number }).IsUnique();
        b.HasIndex(e => new { e.TenantId, e.ClientKey }).IsUnique().HasFilter("\"ClientKey\" IS NOT NULL");
        b.HasIndex(e => new { e.TenantId, e.Date });
        b.HasIndex(e => e.ShipmentId); b.HasIndex(e => e.BatchId);
        b.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.ClientNoAction);
        b.HasOne<ServiceProvider>().WithMany().HasForeignKey(e => e.ServiceProviderId).OnDelete(DeleteBehavior.ClientNoAction);
        b.HasOne<Shipment>().WithMany().HasForeignKey(e => e.ShipmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<RugBatch>().WithMany().HasForeignKey(e => e.BatchId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class CostEntryTargetConfiguration : IEntityTypeConfiguration<CostEntryTarget>
{
    public void Configure(EntityTypeBuilder<CostEntryTarget> b)
    {
        b.ToTable("CostEntryTargets", t => t.HasCheckConstraint("CK_CostEntryTargets_OneTarget", "(\"RugId\" IS NOT NULL) <> (\"BaleId\" IS NOT NULL)"));
        b.HasOne(t => t.Entry).WithMany(e => e.Targets).HasForeignKey(t => t.EntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Rug>().WithMany().HasForeignKey(t => t.RugId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Bale>().WithMany().HasForeignKey(t => t.BaleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Tenant>().WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(t => new { t.EntryId, t.RugId }).IsUnique().HasFilter("\"RugId\" IS NOT NULL");
        b.HasIndex(t => new { t.EntryId, t.BaleId }).IsUnique().HasFilter("\"BaleId\" IS NOT NULL");
        b.HasIndex(t => t.RugId); b.HasIndex(t => t.BaleId);
    }
}

public sealed class CostEntryPinConfiguration : IEntityTypeConfiguration<CostEntryPin>
{
    public void Configure(EntityTypeBuilder<CostEntryPin> b)
    {
        b.ToTable("CostEntryPins");
        b.Property(p => p.Amount).HasPrecision(18, 2);
        b.HasOne(p => p.Entry).WithMany(e => e.Pins).HasForeignKey(p => p.EntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Rug>().WithMany().HasForeignKey(p => p.RugId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Tenant>().WithMany().HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(p => new { p.EntryId, p.RugId }).IsUnique();
    }
}

public sealed class CostAllocationConfiguration : IEntityTypeConfiguration<CostAllocation>
{
    public void Configure(EntityTypeBuilder<CostAllocation> b)
    {
        b.ToTable("CostAllocations"); b.HasKey(a => new { a.EntryId, a.RugId });
        b.Property(a => a.Amount).HasPrecision(18, 2);
        b.HasOne(a => a.Entry).WithMany().HasForeignKey(a => a.EntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.Rug).WithMany().HasForeignKey(a => a.RugId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(a => new { a.TenantId, a.RugId });
        b.HasQueryFilter(a => a.Rug.DeletedAt == null);
    }
}

public sealed class CostEntryResultConfiguration : IEntityTypeConfiguration<CostEntryResult>
{
    public void Configure(EntityTypeBuilder<CostEntryResult> b)
    {
        b.ToTable("CostEntryResults"); b.HasKey(r => r.EntryId);
        b.Property(r => r.EntryId).ValueGeneratedNever();
        b.Property(r => r.AreaM2).HasPrecision(12, 2);
        b.Property(r => r.BaseAmount).HasPrecision(18, 2);
        b.Property(r => r.Allocated).HasPrecision(18, 2);
        b.Property(r => r.Unallocated).HasPrecision(18, 2);
        b.Property(r => r.WarningsJson).HasColumnType("jsonb");
        b.HasOne(r => r.Entry).WithOne().HasForeignKey<CostEntryResult>(r => r.EntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Tenant>().WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(r => new { r.TenantId, r.State });
    }
}
