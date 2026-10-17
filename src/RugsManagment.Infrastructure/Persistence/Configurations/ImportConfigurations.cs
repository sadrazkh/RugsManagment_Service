using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RugsManagment.Domain.Entities;
namespace RugsManagment.Infrastructure.Persistence.Configurations;
public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> b)
    {
        b.ToTable("ImportJobs");
        b.Property(x => x.FileName).HasMaxLength(200);
        b.Property(x => x.FileSha256).HasMaxLength(64);
        b.Property(x => x.StoredPath).HasMaxLength(1000);
        b.Property(x => x.Sheet).HasMaxLength(200);
        b.Property(x => x.TitleText).HasMaxLength(1000);
        b.Property(x => x.MappingJson).HasColumnType("jsonb");
        b.Property(x => x.OptionsJson).HasColumnType("jsonb");
        b.Property(x => x.PreviewJson).HasColumnType("jsonb");
        b.Property(x => x.ResultJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.FileSha256 });
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<RugBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.SetNull);
        b.Property<uint>("xmin").IsRowVersion();
    }
}
public sealed class ImportMappingPresetConfiguration : IEntityTypeConfiguration<ImportMappingPreset>
{
    public void Configure(EntityTypeBuilder<ImportMappingPreset> b)
    {
        b.ToTable("ImportMappingPresets");
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.HeaderSignature).HasMaxLength(64);
        b.Property(x => x.MappingJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.TenantId, x.HeaderSignature }).IsUnique();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
