using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Configurations;

/// <summary>قوانین جدول کارگاه — ایندکس یکتا روی Slug</summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId);
    }
}

public class ProcessStepTypeConfiguration : IEntityTypeConfiguration<ProcessStepType>
{
    public void Configure(EntityTypeBuilder<ProcessStepType> builder)
    {
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.Property(s => s.NameFa).HasMaxLength(100).IsRequired();
        builder.Property(s => s.NameEn).HasMaxLength(100).IsRequired();
    }
}

public class WorkflowTemplateConfiguration : IEntityTypeConfiguration<WorkflowTemplate>
{
    public void Configure(EntityTypeBuilder<WorkflowTemplate> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(t => t.Tenant).WithMany(x => x.WorkflowTemplates).HasForeignKey(t => t.TenantId);
        builder.HasMany(t => t.Steps).WithOne(s => s.WorkflowTemplate).HasForeignKey(s => s.WorkflowTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RugConfiguration : IEntityTypeConfiguration<Rug>
{
    public void Configure(EntityTypeBuilder<Rug> builder)
    {
        builder.HasIndex(r => new { r.TenantId, r.Sku }).IsUnique();
        builder.Property(r => r.Sku).HasMaxLength(50).IsRequired();
        builder.HasOne(r => r.Tenant).WithMany(t => t.Rugs).HasForeignKey(r => r.TenantId);
        builder.HasOne(r => r.Batch).WithMany(b => b.Rugs).HasForeignKey(r => r.BatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(r => r.WorkflowSteps).WithOne(s => s.Rug).HasForeignKey(s => s.RugId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(r => r.MetadataJson).HasColumnType("jsonb"); // متادیتای انعطاف‌پذیر
        builder.Ignore(r => r.AreaSquareMeters); // فقط در کد محاسبه می‌شود
    }
}

/// <summary>فیلدهای سفارشی هر کارگاه — کلید یکتا در محدودهٔ همان کارگاه</summary>
public class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.HasIndex(f => new { f.TenantId, f.Key }).IsUnique();
        builder.Property(f => f.Key).HasMaxLength(60).IsRequired();
        builder.Property(f => f.Label).HasMaxLength(120).IsRequired();
        builder.Property(f => f.OptionsJson).HasColumnType("jsonb");
        builder.HasOne(f => f.Tenant).WithMany().HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RugBatchConfiguration : IEntityTypeConfiguration<RugBatch>
{
    public void Configure(EntityTypeBuilder<RugBatch> builder)
    {
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(b => b.Tenant).WithMany().HasForeignKey(b => b.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ServiceProviderConfiguration : IEntityTypeConfiguration<ServiceProvider>
{
    public void Configure(EntityTypeBuilder<ServiceProvider> builder)
    {
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(p => p.Tenant).WithMany().HasForeignKey(p => p.TenantId);
    }
}
