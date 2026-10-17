using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Documents;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Application.Features.Intake.Grid;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Application.Seed;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Application.Features.Documents;
/// <summary>بسته فقط چیزهای نبوده را اضافه می‌کند تا تنظیم‌های بازرگان در اجرای دوباره حفظ شوند.</summary>
public sealed class PresetService(IRepository<CustomFieldDefinition> custom, IRepository<CostCategory> categories, IRepository<LabelTemplate> labels,
    IRepository<DeclaredValueRate> rates, IRepository<ProcessStepType> steps, ITenantPreferencesRepository preferences, IDocumentTemplateRepository templates,
    IExportSetupRepository setup, IGridLayoutService grids, DocumentTemplateSeedContributor seed, IUnitOfWork uow, IAuditLog audit)
{
    public async Task<IReadOnlyList<string>> ApplyAsync(Guid tenantId, PresetRequest request, CancellationToken ct)
    {
        if (request.SerialStart is <= 0 || !Enum.IsDefined(request.BaleTerm)) throw new AppValidationException("شمارهٔ شروع و واژهٔ بسته را درست انتخاب کنید.");
        return await uow.ExecuteInTransactionAsync(async token => {
            var report = new List<string>();
            var fields = await custom.ListAsync(x => x.TenantId == tenantId,token);
            foreach (var (key,label) in new[] { ("bg_color","رنگ متن"),("border_color","رنگ حاشیه") })
            {
                var old = fields.FirstOrDefault(x => x.Key == key);
                if (old is null) { await custom.AddAsync(new CustomFieldDefinition { TenantId = tenantId, Key = key, Label = label, ShowInGrid = true },token); report.Add("اضافه شد: " + label); }
                else report.Add("موجود بود: " + old.Label);
            }
            var existing = await categories.ListAsync(x => x.TenantId == tenantId,token);
            var definitions = CostCategorySeedContributor.Defaults(tenantId).ToList();
            definitions.Add(new CostCategory { TenantId = tenantId,SeedKey = "purchase.lump",Name = "خرید یکجا",Group = CostGroup.Purchase,Icon = "coins",DefaultBasis = CostBasis.Total,DefaultMethod = AllocationMethod.ByArea,DefaultAreaBasis = AreaBasis.Intake,DefaultScope = CostScope.IntakeBatch });
            var term = BaleTerms.For(request.BaleTerm).Bale;
            var availableSteps = await steps.ListAsync(x => x.TenantId == null || x.TenantId == tenantId,token);
            var expenseDefs = new[] { ("washing","شستشو","droplet",CostBasis.PerM2,AllocationMethod.ByArea,AreaBasis.Intake),
                ("repair","رفوگری","needle",CostBasis.Total,AllocationMethod.ByArea,AreaBasis.Intake),
                ("freightIn","حمل داخلی","truck",CostBasis.Total,AllocationMethod.ByArea,AreaBasis.Effective),
                ("freightOut","کرایهٔ خارجی","ship",CostBasis.Total,AllocationMethod.ByArea,AreaBasis.Effective),
                ("packing","بسته‌بندی و " + term,"tape",CostBasis.PerPiece,AllocationMethod.ByArea,AreaBasis.Effective),
                ("customs","گمرک و ترخیص","stamp",CostBasis.Total,AllocationMethod.ByDeclaredValue,AreaBasis.Effective),
                ("storage","انبارداری","warehouse",CostBasis.Total,AllocationMethod.ByArea,AreaBasis.Effective),
                ("other","سایر","dots",CostBasis.Total,AllocationMethod.ByArea,AreaBasis.Effective) };
            foreach (var (key,label,icon,basis,method,area) in expenseDefs)
                definitions.Add(new CostCategory { TenantId = tenantId,SeedKey = "expense." + key,Name = label,Group = CostGroup.Expense,Icon = icon,DefaultBasis = basis,DefaultMethod = method,DefaultAreaBasis = area,
                    DefaultScope = key == "customs" ? CostScope.Shipment : null, StepTypeId = availableSteps.FirstOrDefault(x => x.Code == key)?.Id });
            foreach (var def in definitions)
            {
                var old = existing.FirstOrDefault(x => x.SeedKey == def.SeedKey);
                if (old is null) { await categories.AddAsync(def,token); report.Add("اضافه شد: " + def.Name); }
                else report.Add("موجود بود: " + old.Name);
            }
            var oldLabels = await labels.ListAsync(x => x.TenantId == tenantId,token);
            if (!oldLabels.Any(x => x.Name == "برچسب شماره"))
                await labels.AddAsync(new LabelTemplate { TenantId = tenantId,Name = "برچسب شماره",WidthMm = 50,HeightMm = 30,
                    ElementsJson = "{\"columns\":1,\"elements\":[{\"id\":\"serial\",\"type\":\"field\",\"field\":\"serial\",\"colSpan\":1,\"fontSize\":24,\"align\":\"center\"},{\"id\":\"serial-qr\",\"type\":\"qr\",\"field\":\"rug.serial\",\"source\":\"{{serial}}\",\"colSpan\":1,\"align\":\"center\"}]}" },token);
            if (request.ConfirmSampleRates)
            {
                var existingRates = await rates.ListAsync(x => x.TenantId == tenantId,token);
                foreach (var (name,rate) in new[] { ("گبه",45m),("خراسان تجاری",25m),("نائین 9 لا",80m) })
                    if (!existingRates.Any(x => x.KeyNormalized == DeclaredValueMath.KeyNormalize(name)))
                        await rates.AddAsync(new DeclaredValueRate { TenantId = tenantId,KeyValue = name,KeyNormalized = DeclaredValueMath.KeyNormalize(name),UsdPerM2 = rate },token);
            }
            await seed.SeedAsync(tenantId,token);
            await uow.SaveChangesAsync(token);
            var p = await preferences.GetAsync(tenantId,token) ?? throw new KeyNotFoundException("ترجیحات کارگاه پیدا نشد.");
            var pref = (await preferences.GetForUpdateAsync(tenantId,p.Version,token))!;
            pref.SerialStart ??= request.SerialStart; pref.DefaultBaleTargetAreaM2 ??= 13m;
            if (!(await setup.ReadAsync(tenantId,pref.DeclaredValueKeyField,token)).Preset) pref.BaleTerm = request.BaleTerm;
            pref.DefaultDeclarationTemplateId ??= (await templates.ListAsync(tenantId,token)).FirstOrDefault(x => x.Kind == DocumentKind.ShipmentDeclaration && x.IsDefault)?.Id;
            await uow.SaveChangesAsync(token);
            var viewer = new GridViewer(tenantId,null,true,BaleTerms.For(pref.BaleTerm));
            foreach (var source in grids.Sources)
                if (!await setup.HasGridAsync(tenantId,source.GridId,token))
                    await grids.SaveAsync(viewer,source.GridId,await grids.GetAsync(viewer,source.GridId,token),token);
            audit.Record(AuditAction.PresetApplied,"Preset",null,"بستهٔ صادرات فرش اعمال شد.");
            await uow.SaveChangesAsync(token); return (IReadOnlyList<string>)report;
        },ct);
    }
}
