namespace RugsManagment.Domain.Entities;
public class DocumentTemplateRevision
{
    public Guid TemplateId { get; set; }
    public int Revision { get; set; }
    public string LayoutJson { get; set; } = "{}";
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? SavedBy { get; set; }
}
