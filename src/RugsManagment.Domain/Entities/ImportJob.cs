using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Domain.Entities;

/// <summary>سابقهٔ ورود اکسل؛ نتیجه و عکس پیشین برای برگشت اتمیک نگه داشته می‌شوند.</summary>
public class ImportJob : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public int Number { get; set; }
    public string FileName { get; set; } = "";
    public string FileSha256 { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public ImportStatus Status { get; set; }
    public string? Sheet { get; set; }
    public int? HeaderRow { get; set; }
    public string? MappingJson { get; set; }
    public string? OptionsJson { get; set; }
    public ImportMode Mode { get; set; }
    public ExistingSerialPolicy ExistingSerialPolicy { get; set; }
    public string? TitleText { get; set; }
    public DateOnly? TitleDate { get; set; }
    public string? PreviewJson { get; set; }
    public string? ResultJson { get; set; }
    public Guid? ShipmentId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? CommittedAt { get; set; }
}
