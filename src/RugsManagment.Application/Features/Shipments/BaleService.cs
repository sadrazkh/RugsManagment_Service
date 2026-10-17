using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>نگله‌ها: فهرست با ردیف‌ها، ساخت، ویرایش (شماره، وزن، یادداشت)، حذف خالی و شماره‌گذاری دوباره (spec C.2، I.2).</summary>
public interface IBaleService
{
    /// <summary>نگله‌های محموله با ردیف‌هایشان به ترتیب شماره.</summary>
    Task<IReadOnlyList<BaleDto>> ListAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>نگلهٔ تازه (Floor): بی شماره = بزرگ‌ترین + ۱؛ شمارهٔ تکراری ۴۰۹. محمولهٔ قطعی ۴۰۹.</summary>
    Task<BaleDto> CreateAsync(Guid tenantId, Guid shipmentId, CreateBaleRequest request, CancellationToken cancellationToken = default);

    /// <summary>ویرایش نگله (Floor)؛ نسخهٔ کهنه ۴۰۹؛ شماره و وزن روی محمولهٔ قطعی ۴۰۹ (C.8).</summary>
    Task<BaleDto> UpdateAsync(Guid tenantId, Guid baleId, UpdateBaleRequest request, CancellationToken cancellationToken = default);

    /// <summary>حذف (Floor) فقط وقتی خالی است (۴۰۹ وگرنه).</summary>
    Task DeleteAsync(Guid tenantId, Guid baleId, uint version, CancellationToken cancellationToken = default);

    /// <summary>
    /// شماره‌گذاری دوباره به ۱..n به ترتیب شمارهٔ فعلی، در یک flush (قید تعویق‌پذیر B.9) و یک تراکنش
    /// (<see cref="IUnitOfWork.ExecuteInTransactionAsync(Func{CancellationToken, Task}, CancellationToken)"/>).
    /// <paramref name="request"/>.Version نسخهٔ محموله است.
    /// </summary>
    Task<IReadOnlyList<BaleDto>> RenumberAsync(Guid tenantId, Guid shipmentId, RenumberBalesRequest request, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class BaleService(
    IShipmentRepository shipments,
    IShipmentDtoBuilder dtos,
    IShipmentSettingsReader settingsReader,
    IConcurrencyGuard concurrency,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    IMoneyVisibility money,
    TimeProvider? time = null,
    ILedgerLock? ledgerLock = null) : IBaleService
{
    /// <summary>بزرگ‌ترین وزن (numeric(8,2)).</summary>
    public const decimal MaxWeightKg = 999_999.99m;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public Task<IReadOnlyList<BaleDto>> ListAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
        => dtos.BalesAsync(tenantId, shipmentId, cancellationToken: cancellationToken);

    public async Task<BaleDto> CreateAsync(
        Guid tenantId, Guid shipmentId, CreateBaleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await unitOfWork.ExecuteInTransactionAsync(async cancellationToken =>
        {
        if (ledgerLock is not null) await ledgerLock.AcquireAsync(tenantId, cancellationToken);
        var shipment = await shipments.GetTrackedAsync(tenantId, shipmentId, cancellationToken)
                       ?? throw new KeyNotFoundException("محموله یافت نشد.");
        var terms = (await settingsReader.GetAsync(tenantId, cancellationToken)).Terms;

        int number;
        if (request.Number is { } typed)
        {
            EnsureNumber(typed, terms);
            if (await shipments.FindBaleByNumberTrackedAsync(tenantId, shipmentId, typed, cancellationToken) is not null)
                throw new DomainConflictException(Messages.BaleNumberTaken.Format(terms, ("n", typed)), "number");
            number = typed;
        }
        else
        {
            number = await shipments.MaxBaleNumberAsync(tenantId, shipmentId, cancellationToken) + 1;
            EnsureNumber(number, terms);
        }

        var bale = new Bale { TenantId = tenantId, ShipmentId = shipment.Id, Number = number, CreatedAt = _time.GetUtcNow() };
        await shipments.AddBaleAsync(bale, cancellationToken);
        audit.Record(AuditAction.BaleCreated, nameof(Bale), bale.Id,
            $"{terms.BaleOf} {Persian(number)} در محمولهٔ {Persian(shipment.Number)} ساخته شد.", shipment.Title,
            new { shipmentId = shipment.Id, number });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await dtos.BalesAsync(tenantId, shipment.Id, bale.Id, cancellationToken)).Single();
        }, cancellationToken);
    }

    public async Task<BaleDto> UpdateAsync(Guid tenantId, Guid baleId, UpdateBaleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await unitOfWork.ExecuteInTransactionAsync(async cancellationToken =>
        {
        if (ledgerLock is not null) await ledgerLock.AcquireAsync(tenantId, cancellationToken);
        var bale = await shipments.GetBaleTrackedAsync(tenantId, baleId, cancellationToken)
                   ?? throw new KeyNotFoundException("نگله یافت نشد.");
        var terms = (await settingsReader.GetAsync(tenantId, cancellationToken)).Terms;
        concurrency.Expect(bale, request.Version);

        if (request.WeightKg is { } weight && (weight <= 0 || weight > MaxWeightKg || !Num.HasAtMostDecimals(weight, 2)))
            throw AppValidationException.ForField("weightKg", Messages.BaleWeightInvalid.Format(terms));
        var notes = ShipmentService.Clean(request.Notes);
        if (notes is not null && notes.Length > Bale.NotesMaxLength)
            throw AppValidationException.ForField("notes", Messages.TooLong.Format(("label", "یادداشت"), ("n", Bale.NotesMaxLength)));

        var before = bale.Number;
        if (request.Number is { } number && number != bale.Number)
        {
            EnsureNumber(number, terms);
            if (await shipments.FindBaleByNumberTrackedAsync(tenantId, bale.ShipmentId, number, cancellationToken) is { } other
                && other.Id != bale.Id)
                throw new DomainConflictException(Messages.BaleNumberTaken.Format(terms, ("n", number)), "number");
            bale.Number = number;
        }

        bale.WeightKg = request.WeightKg;
        bale.Notes = notes;
        bale.UpdatedAt = _time.GetUtcNow();

        audit.Record(AuditAction.ShipmentUpdated, nameof(Bale), bale.Id,
            before == bale.Number
                ? $"{terms.BaleOf} {Persian(bale.Number)} ویرایش شد."
                : $"{terms.BaleOf} {Persian(before)} شد {terms.BaleOf} {Persian(bale.Number)}.",
            null, new { shipmentId = bale.ShipmentId, from = before, to = bale.Number, weightKg = bale.WeightKg });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await dtos.BalesAsync(tenantId, bale.ShipmentId, bale.Id, cancellationToken)).Single();
        }, cancellationToken);
    }

    public async Task DeleteAsync(Guid tenantId, Guid baleId, uint version, CancellationToken cancellationToken = default)
    {
        var bale = await shipments.GetBaleTrackedAsync(tenantId, baleId, cancellationToken)
                   ?? throw new KeyNotFoundException("نگله یافت نشد.");
        var terms = (await settingsReader.GetAsync(tenantId, cancellationToken)).Terms;
        concurrency.Expect(bale, version);
        await EnsureOpenAsync(tenantId, bale.ShipmentId, cancellationToken);

        // پیام پیش‌دستانه؛ BaleMembershipGuard همین را برای هر مسیری می‌سنجد و کلید خارجی NO ACTION پشتوانهٔ آخر است
        if ((await shipments.ListLinesOfBaleTrackedAsync(tenantId, bale.Id, cancellationToken)).Count > 0)
            throw new DomainConflictException(Messages.BaleNotEmpty.Format(terms, ("n", bale.Number)));

        shipments.RemoveBale(bale);
        audit.Record(AuditAction.BaleDeleted, nameof(Bale), bale.Id,
            $"{terms.BaleOf} {Persian(bale.Number)} حذف شد.", null, new { shipmentId = bale.ShipmentId, number = bale.Number });
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BaleDto>> RenumberAsync(
        Guid tenantId, Guid shipmentId, RenumberBalesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (ledgerLock is not null) await ledgerLock.AcquireAsync(tenantId, ct);
            var shipment = await shipments.GetTrackedAsync(tenantId, shipmentId, ct)
                           ?? throw new KeyNotFoundException("محموله یافت نشد.");
            concurrency.Expect(shipment, request.Version);

            // شماره‌گذاری دوباره روی محمولهٔ قطعی ۴۰۹ است حتی وقتی نگله‌ها از پیش پشت سر هم‌اند: آن‌وقت هیچ ستونی واقعاً
            // عوض نمی‌شود و قفل پاس (که فقط تغییر واقعی را می‌بیند) چیزی برای رد کردن نمی‌یابد (C.8)
            if (shipment.IsFinalized)
                throw Locked(shipment.Id, shipment.Number);

            var bales = await shipments.ListBalesTrackedAsync(tenantId, shipmentId, ct);
            var changes = new List<object>();
            var next = 1;
            foreach (var bale in bales.OrderBy(b => b.Number).ThenBy(b => b.CreatedAt).ThenBy(b => b.Id))
            {
                if (bale.Number != next)
                {
                    changes.Add(new { from = bale.Number, to = next });
                    bale.Number = next;
                    bale.UpdatedAt = _time.GetUtcNow();
                }

                next++;
            }

            // نسخهٔ محموله همیشه سنجیده و بالا برده می‌شود: دو «شماره‌گذاری دوباره» با یک نسخه دقیقاً یک ۴۰۹ می‌گیرند
            shipment.UpdatedAt = _time.GetUtcNow();
            var terms = (await settingsReader.GetAsync(tenantId, ct)).Terms;
            audit.Record(AuditAction.BalesRenumbered, nameof(Shipment), shipment.Id,
                changes.Count == 0
                    ? $"{terms.Bales} محمولهٔ {Persian(shipment.Number)} از پیش پشت سر هم بودند."
                    : $"{terms.Bales} محمولهٔ {Persian(shipment.Number)} از ۱ تا {Persian(bales.Count)} شماره‌گذاری شد.",
                shipment.Title, new { changes });
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        return await dtos.BalesAsync(tenantId, shipmentId, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// نگلهٔ محمولهٔ قطعی حذف نمی‌شود (C.8). پیش از پیام «خالی نیست» سنجیده می‌شود، چون نگله‌های محمولهٔ قطعی همه فرش دارند
    /// و آن پیام راه درست («باز کردن دوباره») را نمی‌گفت؛ <c>ShipmentLockGuard</c> همین را برای هر مسیر دیگری می‌سنجد.
    /// </summary>
    private async Task EnsureOpenAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken)
    {
        var header = await shipments.GetHeaderAsync(tenantId, shipmentId, cancellationToken)
                     ?? throw new KeyNotFoundException("محموله یافت نشد.");
        if (header.Status == ShipmentStatus.Finalized)
            throw Locked(header.Id, header.Number);
    }

    /// <summary>۴۰۹ قفل با متن هر نقش: بازرگان «باز کردن دوباره» را می‌بیند و انباردار نام بازرگان را.</summary>
    private ShipmentLockedException Locked(Guid shipmentId, int number)
        => new(money.CanSeeMoney ? Messages.ShipmentLockedHeader.Format(("number", number)) : Messages.ShipmentLockedOperator.Text,
            shipmentId, number, "bale");

    private static void EnsureNumber(int number, BaleTermWords terms)
    {
        if (number < 1 || number > Bale.MaxNumber)
            throw AppValidationException.ForField("number", Messages.BaleNumberInvalid.Format(terms, ("max", Bale.MaxNumber)));
    }

    private static string Persian(int number) => PersianText.ToPersianDigits(number.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
