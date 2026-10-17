using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Application.Fields;
namespace RugsManagment.Application.Features.Documents;
/// <summary>پیش‌پرواز از همان موتور چاپ استفاده می‌کند تا خالی بودن عبارت و فیلد دو تعریف متفاوت نداشته باشد.</summary>
public sealed class DocumentReadinessContributor(DocumentTemplateService templates, IDocumentDataRepository data, IFieldCatalog fields, IMoneyVisibility money) : IShipmentReadinessContributor
{
    public async Task<IReadOnlyList<ReadinessIssueDto>> IssuesAsync(Guid tenantId, Guid shipmentId, Guid templateId, CancellationToken cancellationToken)
    {
        var template = await templates.RequireAsync(tenantId,templateId,cancellationToken);
        var source = await data.ShipmentAsync(tenantId,shipmentId,money.CanSeeMoney,cancellationToken);
        var catalogue = await fields.ListAsync(tenantId,FieldPurpose.Document,template.Kind.ToString(),cancellationToken);
        var layout = LayoutJson.Parse(template.LayoutJson).Layout;
        if (layout is null || !LayoutValidator.Validate(layout,catalogue).Ok) return [];
        var render = DocumentComposer.Compose(layout,source.Data,new ComposeOptions(Fields:catalogue));
        var issues = new List<ReadinessIssueDto>();
        if (template.LayoutJson.Contains("shipment.usdRate",StringComparison.Ordinal) && source.Data.Shipment?.UsdRate is null)
            issues.Add(new ReadinessIssueDto("W_NO_USD_RATE","نرخ دلار این محموله نوشته نشده.",1,[],false));
        var sheet = render.Sheets.First();
        foreach (var col in sheet.Columns)
        {
            var blanks = sheet.Rows.Where(r => r.Kind == RenderRowKinds.Data).Count(r => r.Cells.Any(c => c.Col == col.Index && string.IsNullOrEmpty(c.Display)));
            if (blanks > 0) issues.Add(new ReadinessIssueDto("W_TEMPLATE_FIELD_EMPTY",$"ستون «{col.Label}» در {Messages.FormatNumber(blanks)} ردیف خالی است.",blanks,[],false,Key:col.Key));
        }
        return issues;
    }
}
