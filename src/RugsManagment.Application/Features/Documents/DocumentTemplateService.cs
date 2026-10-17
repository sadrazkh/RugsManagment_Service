using System.Text.Json;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Documents;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Application.Features.Documents;
/// <summary>اعتبارسنجی و تاریخچهٔ قالب در یک سرویس مشترک برای طراح و خروجی.</summary>
public sealed class DocumentTemplateService(IDocumentTemplateRepository repository, IFieldCatalog fields, IMoneyVisibility money,
    IConcurrencyGuard concurrency, IUnitOfWork uow, ICurrentUser user, ITenantPreferencesRepository preferences, IAuditLog audit)
{
    public async Task<IReadOnlyList<DocumentTemplateDto>> ListAsync(Guid tenantId, DocumentKind? kind, CancellationToken ct)
        => (await repository.ListAsync(tenantId, ct)).Where(x => (kind is null || x.Kind == kind) && (money.CanSeeMoney || !x.UsesMoneyFields)).Select(Dto).ToArray();
    public async Task<DocumentTemplate> RequireAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var item = await repository.GetTrackedAsync(tenantId, id, ct) ?? throw new KeyNotFoundException("قالب پیدا نشد.");
        if (item.UsesMoneyFields && !money.CanSeeMoney) throw new UnauthorizedAccessException("این قالب مبلغ دارد و دسترسی بازرگان لازم است.");
        return item;
    }
    public async Task<DocumentTemplateDto> GetAsync(Guid tenantId, Guid id, CancellationToken ct) => Dto(await RequireAsync(tenantId,id,ct));
    public async Task<LayoutValidationResult> ValidateAsync(Guid tenantId, string json, CancellationToken ct)
        => LayoutValidator.Validate(json, await fields.ListAsync(tenantId, FieldPurpose.Document, null, ct));
    public async Task<DocumentTemplateDto> SaveAsync(Guid tenantId, Guid? id, SaveDocumentTemplateRequest request, CancellationToken ct)
    {
        var validation = await ValidateAsync(tenantId, request.Layout.GetRawText(), ct);
        if (!validation.Ok) throw new AppValidationException("قالب را اصلاح کنید.", validation.Errors.GroupBy(x => x.Path).ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray()));
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200) throw AppValidationException.ForField("name", "نام قالب را حداکثر ۲۰۰ نویسه بنویسید.");
        var layout = LayoutJson.Parse(request.Layout.GetRawText()).Layout!;
        var item = id is { } templateId ? await RequireAsync(tenantId,templateId,ct) : new DocumentTemplate { TenantId = tenantId };
        if (id is not null)
        {
            if (request.Version is not { } v) throw AppValidationException.ForField("version", "نسخهٔ قالب لازم است.");
            concurrency.Expect(item,v); item.Revision++;
        }
        item.Name = request.Name.Trim(); item.LayoutJson = LayoutJson.Serialize(layout);
        var kind = Enum.Parse<DocumentKind>(layout.Kind);
        if (id is not null && item.Kind != kind) throw AppValidationException.ForField("layout.kind", "نوع قالب را نمی‌توان عوض کرد؛ قالب تازه بسازید.");
        item.Kind = kind; item.UsesMoneyFields = validation.UsesMoneyFields || item.Kind == DocumentKind.ShipmentCost;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) await repository.AddAsync(item,ct);
        await repository.AddRevisionAsync(new DocumentTemplateRevision { TemplateId = item.Id, Revision = item.Revision, LayoutJson = item.LayoutJson, SavedBy = user.UserId },ct);
        audit.Record(id is null ? AuditAction.TemplateCreated : AuditAction.TemplateUpdated,nameof(DocumentTemplate),item.Id,"قالب خروجی ذخیره شد.",item.Name);
        await uow.SaveChangesAsync(ct); return Dto(item);
    }
    public async Task<DocumentTemplateDto> DuplicateAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var item = await RequireAsync(tenantId,id,ct);
        return await SaveAsync(tenantId,null,new SaveDocumentTemplateRequest(item.Name + " (رونوشت)",JsonDocument.Parse(item.LayoutJson).RootElement),ct);
    }
    public async Task<DocumentTemplateDto> RestoreAsync(Guid tenantId, Guid id, int? revision, uint version, CancellationToken ct)
    {
        var item = await RequireAsync(tenantId,id,ct);
        var json = revision is { } r ? await repository.RevisionAsync(tenantId,id,r,ct) ?? throw new KeyNotFoundException("نسخه پیدا نشد.")
            : item.SeedKey is { } seed ? LayoutJson.Default(seed).Json : throw new AppValidationException("این قالب پیش‌فرض ندارد.");
        return await SaveAsync(tenantId,id,new SaveDocumentTemplateRequest(item.Name,JsonDocument.Parse(json).RootElement,version),ct);
    }
    public async Task<DocumentTemplateDto> SetDefaultAsync(Guid tenantId, Guid id, uint version, CancellationToken ct)
    {
        var item = await RequireAsync(tenantId,id,ct); concurrency.Expect(item,version);
        return await uow.ExecuteInTransactionAsync(async token => {
            foreach (var old in (await repository.ListAsync(tenantId,token)).Where(x => x.Kind == item.Kind && x.IsDefault && x.Id != id)) old.IsDefault = false;
            // یکتایی جزئی باید پیش از روشن کردن پیش‌فرض تازه آزاد شود؛ هر دو ذخیره یک تراکنش‌اند.
            await uow.SaveChangesAsync(token);
            item.IsDefault = true; item.UpdatedAt = DateTimeOffset.UtcNow;
            if (item.Kind == DocumentKind.ShipmentDeclaration)
            {
                var p = await preferences.GetAsync(tenantId,token);
                if (p is not null) (await preferences.GetForUpdateAsync(tenantId,p.Version,token))!.DefaultDeclarationTemplateId = id;
            }
            await uow.SaveChangesAsync(token); return Dto(item);
        },ct);
    }
    public async Task DeleteAsync(Guid tenantId, Guid id, uint version, CancellationToken ct)
    {
        var item = await RequireAsync(tenantId,id,ct); concurrency.Expect(item,version); repository.Remove(item);
        audit.Record(AuditAction.TemplateDeleted,nameof(DocumentTemplate),item.Id,"قالب خروجی حذف شد.",item.Name); await uow.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<RevisionDto>> RevisionsAsync(Guid tenantId, Guid id, CancellationToken ct)
    { await RequireAsync(tenantId,id,ct); return await repository.RevisionsAsync(tenantId,id,ct); }
    private DocumentTemplateDto Dto(DocumentTemplate t) => new(t.Id,t.Name,t.Kind,t.IsDefault,t.Revision,t.UsesMoneyFields,
        JsonDocument.Parse(t.LayoutJson).RootElement.Clone(),repository.Version(t));
}
