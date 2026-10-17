using System.Security.Cryptography;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Application.Features.Documents;
public sealed record DocumentExport(RenderModel Model, byte[]? File, int Number, DateOnly Date);
public sealed class ExportService(IDocumentDataRepository data, IDocumentTemplateRepository templates, DocumentTemplateService templateService,
    IFieldCatalog fields, IMoneyVisibility money, IXlsxDocumentWriter writer, IUnitOfWork uow, ICurrentUser user, IAuditLog audit, ITenantPreferencesRepository preferences)
{
    public async Task<DocumentExport> ShipmentAsync(Guid tenantId, Guid shipmentId, Guid? templateId, ExportFormat format, int? maxGroups, CancellationToken ct)
    {
        var source = await data.ShipmentAsync(tenantId,shipmentId,money.CanSeeMoney,ct);
        var template = await ResolveAsync(tenantId,templateId,DocumentKind.ShipmentDeclaration,ct);
        if (template.Kind == DocumentKind.RugList) throw new AppValidationException("برای محموله، قالب اظهار یا هزینه را انتخاب کنید.");
        return await ComposeAsync(tenantId,source,template,shipmentId,format,maxGroups,ct);
    }
    public async Task<DocumentExport> RugsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, Guid? templateId, CancellationToken ct)
    {
        var source = await data.RugsAsync(tenantId,rugIds,money.CanSeeMoney,ct);
        var template = await ResolveAsync(tenantId,templateId,DocumentKind.RugList,ct);
        if (template.Kind != DocumentKind.RugList) throw new AppValidationException("قالب فهرست فرش را انتخاب کنید.");
        return await ComposeAsync(tenantId,source,template,null,ExportFormat.Xlsx,null,ct);
    }
    public async Task<IReadOnlyList<RugsManagment.Application.DTOs.Documents.ExportRunDto>> RunsAsync(Guid tenantId, Guid shipmentId, CancellationToken ct)
    { await data.ShipmentAsync(tenantId,shipmentId,false,ct); return await templates.RunsAsync(tenantId,shipmentId,money.CanSeeMoney,ct); }
    private async Task<DocumentTemplate> ResolveAsync(Guid tenantId, Guid? id, DocumentKind kind, CancellationToken ct)
    {
        if (id is { } value) return await templateService.RequireAsync(tenantId,value,ct);
        if (kind == DocumentKind.ShipmentDeclaration && (await preferences.GetAsync(tenantId,ct))?.Preferences.DefaultDeclarationTemplateId is { } preferred)
        {
            var selectedPreference = await templateService.RequireAsync(tenantId,preferred,ct);
            if (selectedPreference.Kind != DocumentKind.ShipmentDeclaration) throw new AppValidationException("قالب پیش‌فرض باید از نوع اظهار باشد.");
            return selectedPreference;
        }
        var list = await templates.ListAsync(tenantId,ct);
        var selected = list.Where(x => x.Kind == kind && (money.CanSeeMoney || !x.UsesMoneyFields)).OrderByDescending(x => x.IsDefault).FirstOrDefault()
            ?? throw new AppValidationException("قالب خروجی موجود نیست؛ بستهٔ صادرات فرش را از تنظیمات اجرا کنید.");
        return await templateService.RequireAsync(tenantId,selected.Id,ct);
    }
    private async Task<DocumentExport> ComposeAsync(Guid tenantId, DocumentSource source, DocumentTemplate template, Guid? shipmentId, ExportFormat format, int? maxGroups, CancellationToken ct)
    {
        var catalogue = await fields.ListAsync(tenantId,FieldPurpose.Document,template.Kind.ToString(),ct);
        var validation = LayoutValidator.Validate(template.LayoutJson,catalogue);
        if (!validation.Ok) throw new AppValidationException("قالب معتبر نیست؛ از تنظیم‌کننده کمک بگیرید.");
        var model = DocumentComposer.Compose(LayoutJson.Parse(template.LayoutJson).Layout!,source.Data,
            new ComposeOptions(format == ExportFormat.Preview ? Math.Clamp(maxGroups ?? 3,1,100) : null, Fields:catalogue));
        byte[]? file = null;
        if (format == ExportFormat.Xlsx) { using var stream = new MemoryStream(); await writer.WriteAsync(model,stream,ct); file = stream.ToArray(); }
        await templates.AddRunAsync(new ExportRun { TenantId = tenantId, ShipmentId = shipmentId, TemplateId = template.Id,
            TemplateRevision = template.Revision, Format = format, IsDraft = source.Data.IsDraft, DataHash = source.DataHash,
            FileSha256 = file is null ? null : Convert.ToHexStringLower(SHA256.HashData(file)), UserId = user.UserId, RugCount = source.Data.AllLines().Count() },ct);
        if (format != ExportFormat.Preview) audit.Record(AuditAction.Exported,"Document",shipmentId,"خروجی سند گرفته شد.",template.Name);
        await uow.SaveChangesAsync(ct);
        return new DocumentExport(model,file,source.Data.Shipment?.Number ?? 0,source.Data.Shipment?.DocumentDate ?? DateOnly.FromDateTime(DateTime.UtcNow));
    }
}
