using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Import;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Application.Features.Import;

/// <summary>فایل، بازبینی و ثبت اتمیک؛ برگشت فقط وقتی مجاز است که هیچ دادهٔ وابسته‌ای تغییر نکرده باشد.</summary>
public sealed class ImportService(IImportJobRepository repository, IWorkbookReader reader, IImportFileStorage files,
    IUnitOfWork uow, ITenantCounters counters, IConcurrencyGuard concurrency, ILedgerLock ledgerLock,
    ICurrentUser user, IAuditLog audit, IShipmentRepository shipments, IShipmentSettingsReader settingsReader)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<IReadOnlyList<ImportJobDto>> ListAsync(Guid tenantId, CancellationToken ct)
        => (await repository.ListAsync(tenantId, ct)).Select(j => Dto(j, [], null)).ToArray();

    public async Task<ImportJobDto> UploadAsync(Guid tenantId, string name, Stream input, CancellationToken ct)
    {
        if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) throw Error("E_NOT_XLSX");
        using var buffer = new MemoryStream();
        var block = new byte[81920]; int read;
        while ((read = await input.ReadAsync(block, ct)) > 0)
        {
            if (buffer.Length + read > XlsxSignature.MaxFileBytes) throw Error("E_FILE_TOO_LARGE");
            await buffer.WriteAsync(block.AsMemory(0, read), ct);
        }
        buffer.Position = 0;
        var sheets = await reader.ReadAsync(buffer, ct);
        var first = sheets.FirstOrDefault(s => HeaderDictionary.Detect(s) != null) ?? sheets[0];
        var header = HeaderDictionary.Detect(first);
        var mapping = header is { } h ? HeaderDictionary.Map(first, h) : new Dictionary<string, int>();
        if (header is { } headerRow)
        {
            var saved = await repository.MappingAsync(tenantId, HeaderDictionary.Signature(first, headerRow), ct);
            if (saved != null) mapping = Read<Dictionary<string, int>>(saved.MappingJson);
        }
        var title = header is { } hr ? ImportPlanner.Title(first, hr) : default;
        var sha = Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
        var job = new ImportJob
        {
            TenantId = tenantId, Number = checked((int)await counters.NextBlockAsync(tenantId, "import", 1, ct)),
            FileName = Path.GetFileName(name)[..Math.Min(Path.GetFileName(name).Length, 200)], FileSha256 = sha,
            Sheet = first.Name, HeaderRow = header, MappingJson = Serialize(mapping), TitleText = title.Item1,
            TitleDate = title.Item2, Mode = mapping.ContainsKey("bale") ? ImportMode.Declaration : ImportMode.StockOnly,
            CreatedBy = user.UserId
        };
        buffer.Position = 0;
        job.StoredPath = await files.SaveAsync(tenantId, job.Id, buffer, ct);
        try
        { await repository.AddAsync(job, ct); await uow.SaveChangesAsync(ct); }
        catch { await files.DeleteAsync(tenantId, job.Id, CancellationToken.None); throw; }
        var duplicate = await repository.DuplicateAsync(tenantId, sha, job.Id, ct);
        return Dto(job, sheets.Select(s => s.Name).ToArray(), duplicate);
    }
    public async Task<ImportJobDto> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var job = await Find(tenantId, id, ct);
        await using var input = await files.OpenAsync(tenantId, id, ct);
        var sheets = await reader.ReadAsync(input, ct);
        return Dto(job, sheets.Select(s => s.Name).ToArray(), await repository.DuplicateAsync(tenantId, job.FileSha256, job.Id, ct));
    }
    public Task<ImportPreviewDto> PreviewAsync(Guid tenantId, Guid id, PreviewImportRequest request, CancellationToken ct)
        => uow.ExecuteInTransactionAsync(async token =>
        {
            await ledgerLock.AcquireAsync(tenantId, token);
            var job = await Find(tenantId, id, token);
            if (job.Status is ImportStatus.Committed or ImportStatus.Undone) throw Conflict();
            await using var input = await files.OpenAsync(tenantId, id, token);
            var sheet = (await reader.ReadAsync(input, token)).FirstOrDefault(s => s.Name == request.Sheet)
                ?? throw new DomainValidationException("برگهٔ انتخاب‌شده در فایل نیست.", "sheet");
            var header = request.HeaderRow ?? HeaderDictionary.Detect(sheet) ?? throw Error("E_NO_HEADER");
            var mapping = request.Mapping ?? HeaderDictionary.Map(sheet, header);
            var options = request.Options ?? new ImportOptions(job.Mode);
            if (!Enum.IsDefined(options.Mode) || !Enum.IsDefined(options.ExistingSerialPolicy)) throw new DomainValidationException("روش ورود را انتخاب کنید.", "options");
            var settings = await settingsReader.GetAsync(tenantId, token);
            var rates = (await shipments.ListRatesAsync(tenantId, token)).ToDictionary(r => r.KeyNormalized, r => r.UsdPerM2);
            var preview = ImportPlanner.Plan(sheet, header, mapping, options, request.Overrides, rates, settings.KeyField, settings.FallbackUsd, request.SkippedRows);
            var rugs = await repository.RugsAsync(tenantId, token);
            var lines = await repository.LinesAsync(tenantId, rugs.Select(r => r.Id).ToArray(), token);
            var rows = preview.Rows.Select(row => Existing(row, rugs, lines, options)).ToArray();
            var originalRowIssues = preview.Rows.SelectMany(r => r.Issues).GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count());
            // تصمیم‌های کنارگذاری و اتصال باید در جمع قابل ورود هم دیده شوند، نه جمعِ قبل از تصمیم.
            var reconciliationIssues = new List<ImportIssueDto>();
            var reconciliation = Reconciler.Reconcile(sheet, rows, options.Mode, reconciliationIssues, mapping);
            var issues = rows.SelectMany(r => r.Issues).Concat(preview.IssuesByCode
                .Where(i => i.Code is not ("W_FOOTER_MISMATCH" or "W_SUMMARY_MISMATCH" or "I_VALUE_SNAPPED"))
                .SelectMany(i => Enumerable.Repeat(
                    new ImportIssueDto(i.Code, i.Severity, i.Message), Math.Max(0, i.Count - originalRowIssues.GetValueOrDefault(i.Code)))))
                .Concat(reconciliationIssues)
                .GroupBy(i => i.Code).Select(g => new IssueSummaryDto(g.Key, g.First().Severity, g.First().Message, g.Count())).ToList();
            if (await repository.DuplicateAsync(tenantId, job.FileSha256, job.Id, token) != null)
                issues.Add(new("W_DUPLICATE_FILE", "warning", ImportMessages.Text("W_DUPLICATE_FILE"), 1));
            var title = ImportPlanner.Title(sheet, header);
            job.Sheet = sheet.Name; job.HeaderRow = header; job.MappingJson = Serialize(mapping);
            job.OptionsJson = Serialize(new SavedPreviewOptions(options.Mode, options.ExistingSerialPolicy,
                options.ConvertCentimeters, request.Overrides, request.SkippedRows));
            job.Mode = options.Mode; job.ExistingSerialPolicy = options.ExistingSerialPolicy;
            job.TitleText = title.Title; job.TitleDate = title.Date; job.Status = ImportStatus.Previewed;
            preview = ImportPlanner.Build(rows, reconciliation, issues, preview.SuggestedRates, 0);
            job.PreviewJson = Serialize(preview); job.UpdatedAt = DateTimeOffset.UtcNow;
            await uow.SaveChangesAsync(token);
            return preview with { Version = concurrency.VersionOf(job) };
        }, ct);

    private ImportRowDto Existing(ImportRowDto row, IReadOnlyList<Rug> rugs, IReadOnlyList<ShipmentLine> lines, ImportOptions options)
    {
        if (row.Action != "create") return row;
        var serial = SerialNumber.Parse(row.Values.GetValueOrDefault("serial"));
        var existing = rugs.FirstOrDefault(r => r.DeletedAt == null && (r.SerialNo == serial?.Display || serial?.From != null && r.SerialFrom != null && serial.From <= r.SerialTo && r.SerialFrom <= serial.To));
        if (existing is null) return row;
        var issues = row.Issues.ToList();
        if (options.ExistingSerialPolicy == ExistingSerialPolicy.Skip) return row with { Action = "skip" };
        if (lines.Any(l => l.RugId == existing.Id && l.Shipment.IsFinalized)) issues.Add(ImportMessages.Issue("E_SERIAL_LOCKED", "serial"));
        else if (options.ExistingSerialPolicy == ExistingSerialPolicy.Error || existing.DeletedAt != null
            || existing.SerialNo != serial?.Display || existing.Quantity != ImportPlanner.Qty(row) || lines.Any(l => l.RugId == existing.Id))
            issues.Add(ImportMessages.Issue("E_SERIAL_EXISTS", "serial"));
        else if (options.Mode == ImportMode.StockOnly) return row with { Action = "skip" };
        return row with { Action = issues.Any(i => i.Severity == "error") ? "error" : "attach", Issues = issues,
            ExistingRugId = existing.Id, ExistingVersion = concurrency.VersionOf(existing) };
    }
    public Task<ImportResultDto> CommitAsync(Guid tenantId, Guid id, CommitImportRequest request, CancellationToken ct)
        => uow.ExecuteInTransactionAsync(async token =>
        {
            await ledgerLock.AcquireAsync(tenantId, token);
            var job = await Find(tenantId, id, token);
            if (job.Status != ImportStatus.Previewed || concurrency.VersionOf(job) != request.Version) throw Conflict();
            concurrency.Expect(job, request.Version);
            var preview = Read<ImportPreviewDto>(job.PreviewJson);
            if (preview.Counts.Errors != 0) throw new DomainValidationException("اول ردیف‌های خطادار را اصلاح کنید.", "rows");
            var included = preview.Rows.Where(r => r.Action is "create" or "attach").ToArray();
            if (included.Length == 0) throw new DomainValidationException("ردیفی برای ورود باقی نمانده است.", "rows");
            var rugs = await repository.RugsAsync(tenantId, token);
            var lines = await repository.LinesAsync(tenantId, included.Where(r => r.ExistingRugId != null).Select(r => r.ExistingRugId!.Value).ToArray(), token);
            if (lines.Count > 0) throw Conflict();
            var now = DateTimeOffset.UtcNow;
            var settings = await settingsReader.GetAsync(tenantId, token);
            var rates = (await shipments.ListRatesAsync(tenantId, token)).ToDictionary(r => r.KeyNormalized, r => r.UsdPerM2);
            Shipment? shipment = null; RugBatch? batch = null;
            var baleIds = new List<Guid>(); var lineIds = new List<Guid>(); var states = new List<ImportedRugState>();
            if (job.Mode == ImportMode.Declaration)
            {
                shipment = new Shipment { TenantId = tenantId, Number = checked((int)await counters.NextBlockAsync(tenantId, "shipment", 1, token)),
                    Title = job.TitleText ?? "ورود از اکسل", DocumentDate = job.TitleDate ?? IranDay.Today() };
                // عنوان فایل ممکن است طولانی‌تر از عنوان محموله باشد.
                shipment.Title = shipment.Title[..Math.Min(shipment.Title.Length, Shipment.TitleMaxLength)];
                await repository.AddAsync(shipment, token); job.ShipmentId = shipment.Id;
            }
            else
            {
                batch = new RugBatch { TenantId = tenantId, Number = checked((int)await counters.NextBlockAsync(tenantId, "batch", 1, token)),
                    Name = "ورود از اکسل " + PersianText.ToPersianDigits(IranDay.JalaliLatin(IranDay.Today())), ReceivedAt = now };
                await repository.AddAsync(batch, token); job.BatchId = batch.Id;
            }
            var bales = new Dictionary<int, Bale>(); var seq = new Dictionary<int, int>();
            foreach (var row in included)
            {
                Rug rug; var created = row.Action == "create";
                if (created)
                {
                    rug = new Rug { TenantId = tenantId, Sku = $"IMP-{job.Id:N}-{row.SheetRow}", Origin = row.Values.GetValueOrDefault("origin"),
                        Pattern = row.Values.GetValueOrDefault("pattern"), Material = row.Values.GetValueOrDefault("material"), BatchId = batch?.Id,
                        MetadataJson = Serialize(new Dictionary<string, string?> { ["bg_color"] = row.Values.GetValueOrDefault("bg_color"), ["border_color"] = row.Values.GetValueOrDefault("border_color") }) };
                    rug.SetSerialAndQuantity(SerialNumber.Parse(row.Values["serial"]), ImportPlanner.Qty(row));
                    rug.SetIntakeSize(ImportPlanner.Number(row.Values["length"])!.Value, ImportPlanner.Number(row.Values["width"])!.Value);
                    await repository.AddAsync(rug, token);
                }
                else
                {
                    rug = rugs.SingleOrDefault(r => r.Id == row.ExistingRugId) ?? throw Conflict();
                    if (concurrency.VersionOf(rug) != row.ExistingVersion || rug.DeletedAt != null) throw Conflict();
                    concurrency.Expect(rug, row.ExistingVersion!.Value);
                }
                states.Add(new(rug.Id, 0, created, rug.FinalLength, rug.FinalWidth, rug.FinalMeasuredAt, rug.FinalMeasuredBy, rug.UpdatedAt));
                if (shipment is not null)
                {
                    rug.SetFinalSize(ImportPlanner.Number(row.Values["length"])!.Value, ImportPlanner.Number(row.Values["width"])!.Value, now, user.UserId);
                    var number = row.Bale!.Value;
                    if (!bales.TryGetValue(number, out var bale))
                    { bale = new Bale { TenantId = tenantId, ShipmentId = shipment.Id, Number = number }; bales.Add(number, bale); baleIds.Add(bale.Id); await repository.AddAsync(bale, token); }
                    seq[number] = seq.GetValueOrDefault(number) + 1;
                    var rate = ImportPlanner.Number(row.Values.GetValueOrDefault("usdPerM2"));
                    var key = DeclaredValueMath.KeyValueOf(settings.KeyField, rug.Title, rug.Origin, rug.Pattern, rug.Material, rug.MetadataJson);
                    var tableRate = DeclaredValueMath.Resolve(null, key, rates, settings.FallbackUsd).Rate;
                    var line = new ShipmentLine { TenantId = tenantId, ShipmentId = shipment.Id, BaleId = bale.Id, RugId = rug.Id,
                        Seq = seq[number], UsdPerM2Override = rate == tableRate ? null : rate };
                    await repository.AddAsync(line, token); lineIds.Add(line.Id);
                }
                rug.UpdatedAt = now;
            }
            await uow.SaveChangesAsync(token);
            var all = await repository.RugsAsync(tenantId, token);
            states = states.Select(s => s with { Version = concurrency.VersionOf(all.Single(r => r.Id == s.Id)) }).ToList();
            var result = new ImportResultDto(states.Count(s => s.Created), states.Count(s => !s.Created), shipment?.Id, shipment?.Number, batch?.Id, preview.Counts.Skip);
            var related = await repository.RelatedAsync(tenantId, job, states.Select(s => s.Id).ToArray(), token);
            job.ResultJson = Serialize(new ImportUndoState(result, states, baleIds, lineIds, related));
            job.Status = ImportStatus.Committed; job.CommittedAt = now; job.UpdatedAt = now;
            if (request.SaveMapping)
            {
                await using var input = await files.OpenAsync(tenantId, id, token);
                var sheet = (await reader.ReadAsync(input, token)).Single(s => s.Name == job.Sheet);
                var signature = HeaderDictionary.Signature(sheet, job.HeaderRow!.Value);
                var preset = await repository.MappingAsync(tenantId, signature, token);
                if (preset is null) await repository.AddAsync(new ImportMappingPreset { TenantId = tenantId, HeaderSignature = signature, MappingJson = job.MappingJson!, Name = job.FileName }, token);
                else { preset.MappingJson = job.MappingJson!; preset.UpdatedAt = now; }
            }
            audit.Record(AuditAction.ImportCommitted, nameof(ImportJob), job.Id, "ورود از اکسل ثبت شد.", job.FileName);
            await uow.SaveChangesAsync(token);
            return result;
        }, ct);

    public Task<ImportUndoDto> UndoAsync(Guid tenantId, Guid id, CancellationToken ct)
        => uow.ExecuteInTransactionAsync(async token =>
        {
            await ledgerLock.AcquireAsync(tenantId, token);
            var job = await Find(tenantId, id, token);
            if (job.Status != ImportStatus.Committed) throw Conflict();
            var state = Read<ImportUndoState>(job.ResultJson);
            var ids = state.Rugs.Select(r => r.Id).ToArray();
            await repository.LockUndoAsync(tenantId, job, ids, token);
            var blockers = (await repository.UndoBlockersAsync(tenantId, job, ids, state.BaleIds, token)).ToList();
            var related = await repository.RelatedAsync(tenantId, job, ids, token);
            if (state.Related is not { } savedRelated || savedRelated.ShipmentVersion != related.ShipmentVersion
                || savedRelated.BatchVersion != related.BatchVersion || !Same(savedRelated.BaleVersions, related.BaleVersions)
                || !Same(savedRelated.LineVersions, related.LineVersions) || !Same(savedRelated.RugChildren, related.RugChildren))
                blockers.Add("پس از ورود، محموله، نگله، جای فرش یا مشخصات وابستهٔ آن تغییر کرده است؛ برای حفظ تغییرها، این ورود برگردانده نمی‌شود.");
            var rugs = await repository.RugsAsync(tenantId, token);
            foreach (var saved in state.Rugs)
            {
                var rug = rugs.SingleOrDefault(r => r.Id == saved.Id);
                if (rug is null || concurrency.VersionOf(rug) != saved.Version)
                    blockers.Add("فرش «" + (rug?.SerialNo ?? saved.Id.ToString()) + "» پس از ورود تغییر کرده است.");
            }
            var lines = await repository.LinesAsync(tenantId, ids, token);
            if (lines.Any(l => !state.LineIds.Contains(l.Id)) || state.LineIds.Any(l => !lines.Any(x => x.Id == l))) blockers.Add("جای فرش‌های این ورود تغییر کرده است.");
            if (blockers.Count > 0) return new ImportUndoDto(false, blockers);
            foreach (var line in lines) repository.Remove(line);
            await uow.SaveChangesAsync(token);
            foreach (var saved in state.Rugs)
            {
                var rug = rugs.Single(r => r.Id == saved.Id);
                if (saved.Created) repository.Remove(rug);
                else
                {
                    if (saved.FinalLength is { } l && saved.FinalWidth is { } w) rug.SetFinalSize(l, w, saved.FinalMeasuredAt ?? DateTimeOffset.UtcNow, saved.FinalMeasuredBy);
                    else rug.ClearFinalSize();
                    rug.UpdatedAt = saved.UpdatedAt;
                }
            }
            await uow.SaveChangesAsync(token);
            foreach (var bid in state.BaleIds)
                if (await repository.BaleAsync(tenantId, bid, token) is { } bale) repository.Remove(bale);
            if (job.ShipmentId is { } sid && await repository.ShipmentAsync(tenantId, sid, token) is { } shipment) repository.Remove(shipment);
            if (job.BatchId is { } batchId && await repository.BatchAsync(tenantId, batchId, token) is { } batch) repository.Remove(batch);
            job.Status = ImportStatus.Undone; job.ShipmentId = null; job.BatchId = null; job.UpdatedAt = DateTimeOffset.UtcNow;
            audit.Record(AuditAction.ImportUndone, nameof(ImportJob), job.Id, "ورود از اکسل برگردانده شد.", job.FileName);
            await uow.SaveChangesAsync(token);
            return new ImportUndoDto(true, []);
        }, ct);
    private async Task<ImportJob> Find(Guid tenantId, Guid id, CancellationToken ct)
        => await repository.FindAsync(tenantId, id, ct) ?? throw new KeyNotFoundException();
    private ImportJobDto Dto(ImportJob j, IReadOnlyList<string> sheets, ImportJob? duplicate)
        => new(j.Id, j.Number, j.FileName, j.Status, sheets, j.HeaderRow,
            j.MappingJson is null ? null : Read<Dictionary<string, int>>(j.MappingJson), j.Mode, j.TitleText, j.TitleDate,
            duplicate is null ? null : new(duplicate.Id, duplicate.Number), j.CreatedAt, concurrency.VersionOf(j), SavedRequest(j));
    private static PreviewImportRequest? SavedRequest(ImportJob job)
    {
        if (job.Status != ImportStatus.Previewed || job.OptionsJson is null || job.Sheet is null) return null;
        var saved = Read<SavedPreviewOptions>(job.OptionsJson);
        return new(job.Sheet, job.MappingJson is null ? null : Read<Dictionary<string, int>>(job.MappingJson),
            new(saved.Mode, saved.ExistingSerialPolicy, saved.ConvertCentimeters), saved.Overrides, job.HeaderRow, saved.SkippedRows);
    }
    private sealed record SavedPreviewOptions(ImportMode Mode, ExistingSerialPolicy ExistingSerialPolicy, bool ConvertCentimeters,
        IReadOnlyDictionary<int, Dictionary<string, string?>>? Overrides = null, IReadOnlyList<int>? SkippedRows = null);
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Read<T>(string? value) => JsonSerializer.Deserialize<T>(value ?? "null", Json) ?? throw new InvalidOperationException("Import state missing");
    private static DomainValidationException Error(string code) => new(ImportMessages.Text(code), "file");
    private static ConcurrencyConflictException Conflict() => new("این ورود تغییر کرده یا قبلاً ثبت شده است؛ دوباره بازبینی کنید.");
    private static bool Same<T>(IReadOnlyDictionary<Guid, T> left, IReadOnlyDictionary<Guid, T> right)
        => left.Count == right.Count && left.All(p => right.TryGetValue(p.Key, out var value) && EqualityComparer<T>.Default.Equals(p.Value, value));
}
