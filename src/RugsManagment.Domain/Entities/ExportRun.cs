using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Domain.Entities;
public class ExportRun : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? ShipmentId { get; set; }
    public Guid? TemplateId { get; set; }
    public int TemplateRevision { get; set; }
    public ExportFormat Format { get; set; }
    public bool IsDraft { get; set; }
    public string DataHash { get; set; } = "";
    public string? FileSha256 { get; set; }
    public Guid? UserId { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public int? RugCount { get; set; }
}
