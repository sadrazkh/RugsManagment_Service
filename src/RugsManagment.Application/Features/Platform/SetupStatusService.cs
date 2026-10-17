using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Documents;
using RugsManagment.Application.Features.Documents;
using RugsManagment.Domain.Entities;
namespace RugsManagment.Application.Features.Platform;
public sealed class SetupStatusService(IExportSetupRepository repository, ITenantPreferencesRepository preferences, DocumentTemplateService templates)
{
    public async Task<SetupStatusDto> GetAsync(Guid tenantId, CancellationToken ct)
    {
        var prefs = (await preferences.GetAsync(tenantId,ct))?.Preferences ?? TenantPreferences.CreateDefault(tenantId);
        var state = await repository.ReadAsync(tenantId,prefs.DeclaredValueKeyField,ct);
        var templateValid = false;
        if (prefs.DefaultDeclarationTemplateId is { } id)
        {
            try { var t = await templates.GetAsync(tenantId,id,ct); templateValid = (await templates.ValidateAsync(tenantId,t.Layout.GetRawText(),ct)).Ok; }
            catch (KeyNotFoundException) { }
        }
        SetupStepDto[] steps = [
            new("preset","بستهٔ صادرات فرش",true,state.Preset,"/settings/setup"),
            new("serials","شماره‌گذاری فرش",true,prefs.SerialStart is not null || state.Serials,"/settings/preferences"),
            new("declaredValues","جدول ارزش گمرکی",true,state.HasRates && state.MissingRates.Count == 0,"/settings/declared-values",state.MissingRates),
            new("baleTarget","متراژ هدف هر بسته",true,prefs.DefaultBaleTargetAreaM2 is > 0,"/settings/preferences"),
            new("merchantUser","کاربر بازرگان با نوشتهٔ بزرگ",true,state.Merchant,"/Users"),
            new("declarationTemplate","قالب معتبر اظهار",true,templateValid,"/settings/documents"),
            new("testExport","آزمون اکسل و چاپ",false,state.TestExport,"/Shipments"),
            new("serialLabel","برچسب شماره و QR",false,state.SerialLabel,"/Labels"),
            new("exporter","مشخصات صادرکننده",false,!string.IsNullOrWhiteSpace(prefs.ExporterName) && !string.IsNullOrWhiteSpace(prefs.ExporterCode),"/settings/preferences") ];
        var remaining = steps.Count(x => x.Required && !x.Done);
        return new SetupStatusDto(remaining == 0,remaining,steps);
    }
}
