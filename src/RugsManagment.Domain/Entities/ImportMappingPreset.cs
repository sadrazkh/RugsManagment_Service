using RugsManagment.Domain.Common;
namespace RugsManagment.Domain.Entities;
public class ImportMappingPreset : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string HeaderSignature { get; set; } = "";
    public string MappingJson { get; set; } = "{}";
}
