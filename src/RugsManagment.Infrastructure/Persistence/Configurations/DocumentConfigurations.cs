using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RugsManagment.Domain.Entities;
namespace RugsManagment.Infrastructure.Persistence.Configurations;
public sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> b)
    {
        b.ToTable("DocumentTemplates");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.SeedKey).HasMaxLength(100);
        b.Property(x => x.LayoutJson).HasColumnType("jsonb");
        b.Property<uint>("xmin").IsRowVersion();
        b.HasIndex(x => new { x.TenantId, x.Kind }).IsUnique().HasFilter("\"IsDefault\"");
        b.HasIndex(x => new { x.TenantId, x.SeedKey }).IsUnique().HasFilter("\"SeedKey\" IS NOT NULL");
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany<DocumentTemplateRevision>().WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class DocumentTemplateRevisionConfiguration : IEntityTypeConfiguration<DocumentTemplateRevision>
{
    public void Configure(EntityTypeBuilder<DocumentTemplateRevision> b)
    {
        b.ToTable("DocumentTemplateRevisions"); b.HasKey(x => new { x.TemplateId, x.Revision });
        b.Property(x => x.LayoutJson).HasColumnType("jsonb");
    }
}
public sealed class ExportRunConfiguration : IEntityTypeConfiguration<ExportRun>
{
    public void Configure(EntityTypeBuilder<ExportRun> b)
    {
        b.ToTable("ExportRuns");
        b.Property(x => x.DataHash).HasColumnType("character(64)");
        b.Property(x => x.FileSha256).HasColumnType("character(64)");
        b.HasIndex(x => new { x.TenantId, x.ShipmentId, x.At });
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<DocumentTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
    }
}
public sealed class DocumentPreferencesConfiguration : IEntityTypeConfiguration<TenantPreferences>
{
    public void Configure(EntityTypeBuilder<TenantPreferences> b)
        => b.HasOne<DocumentTemplate>().WithMany().HasForeignKey(x => x.DefaultDeclarationTemplateId).OnDelete(DeleteBehavior.SetNull);
}
