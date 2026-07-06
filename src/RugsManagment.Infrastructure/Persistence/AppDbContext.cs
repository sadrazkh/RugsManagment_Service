using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence;

/// <summary>
/// زمینهٔ Entity Framework — نقطهٔ اتصال به PostgreSQL.
/// جداول از روی Entityهای Domain و فایل‌های Configuration ساخته می‌شوند.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ProcessStepType> ProcessStepTypes => Set<ProcessStepType>();
    public DbSet<ServiceProvider> ServiceProviders => Set<ServiceProvider>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowTemplateStep> WorkflowTemplateSteps => Set<WorkflowTemplateStep>();
    public DbSet<Rug> Rugs => Set<Rug>();
    public DbSet<RugBatch> RugBatches => Set<RugBatch>();
    public DbSet<RugWorkflowStep> RugWorkflowSteps => Set<RugWorkflowStep>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<LabelTemplate> LabelTemplates => Set<LabelTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // همهٔ کلاس‌های IEntityTypeConfiguration در همین اسمبلی اعمال می‌شوند
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
