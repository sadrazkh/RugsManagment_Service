using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using static RugsManagment.Tests.P2App.P2Data;

namespace RugsManagment.Tests.P2App;

/// <summary>
/// «شمارهٔ بعدی» (spec G.4، L.1 «SerialSuggester»): <c>max(SerialStart ?? 1, MAX(COALESCE(SerialTo, SerialFrom)) + 1)</c>؛
/// بعد از «3985 - 4003» ← ۴۰۰۴؛ شماره‌های اشتباه کوچک‌تر پایینش نمی‌کشند؛ کف SerialStart؛ حالت «دستی» پیشنهادی ندارد.
/// (اینکه سطل زباله حساب نمی‌شود و صادرشده و فروخته‌شده می‌شوند قرارداد <c>GetMaxSerialAsync</c> است و آزمون یکپارچه دارد.)
/// </summary>
public class SerialSuggesterTests
{
    [Theory]
    [InlineData(null, null, 1)]
    [InlineData(null, 4003, 4004)] // بعد از «3985 - 4003»
    [InlineData(5000, 4003, 5000)] // کف SerialStart
    [InlineData(3000, 4003, 4004)]
    [InlineData(0, null, 1)]
    [InlineData(null, int.MaxValue, int.MaxValue)]
    public void Next_FollowsTheServerRule(int? serialStart, int? maxInUse, int expected)
        => Assert.Equal(expected, SerialSuggester.Next(serialStart, maxInUse));

    [Fact]
    public void NextAfter_ALot_IsItsLastNumberPlusOne_NotFromPlusQuantity()
    {
        Assert.Equal(4004, SerialSuggester.NextAfter(SerialNumber.Parse("3985 - 4003")));
        Assert.Equal(3779, SerialSuggester.NextAfter(SerialNumber.Parse("3778")));
        Assert.Null(SerialSuggester.NextAfter(SerialNumber.Parse("A-12")));
        Assert.Null(SerialSuggester.NextAfter(null));
    }

    [Fact]
    public void ForNewRow_GivesARangeForALot()
    {
        Assert.Equal("4004", SerialSuggester.ForNewRow(4004, 1).Display);
        Assert.Equal("3911 - 3960", SerialSuggester.ForNewRow(3911, 50).Display);
    }

    [Fact]
    public async Task Suggest_UsesTheMaximum_AndManualModeSuggestsNothing()
    {
        var rugs = new MaxSerialRugs(4003); // مثلاً «3985 - 4003» و «2026 - 2028» (اشتباه تایپ، کوچک‌تر)
        var prefs = new SinglePreferences(new TenantPreferences { TenantId = TenantId, SerialStart = 1000 });
        var suggester = new SerialSuggester(rugs, prefs);

        Assert.Equal(4004, await suggester.SuggestAsync(TenantId));
        Assert.Equal(4004, await suggester.NextFreeAsync(TenantId));

        prefs.Row!.SerialMode = SerialMode.Manual;
        Assert.Null(await suggester.SuggestAsync(TenantId));
        Assert.Equal(4004, await suggester.NextFreeAsync(TenantId)); // nextFree و شمارهٔ خودکار حالت را نمی‌بینند
        Assert.Equal(SerialMode.Manual, await suggester.ModeAsync(TenantId));
    }

    [Fact]
    public async Task Suggest_WithoutAPreferencesRow_UsesTheDefaults()
    {
        var suggester = new SerialSuggester(new MaxSerialRugs(null), new SinglePreferences(null));
        Assert.Equal(1, await suggester.SuggestAsync(TenantId));
        Assert.Equal(SerialMode.SuggestNext, await suggester.ModeAsync(TenantId));
    }

    private sealed class MaxSerialRugs(int? max) : NotSupportedRugRepository
    {
        public override Task<int?> GetMaxSerialAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(max);
    }

    private sealed class SinglePreferences(TenantPreferences? row) : ITenantPreferencesRepository
    {
        public TenantPreferences? Row { get; } = row;

        public Task<VersionedTenantPreferences?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(Row is null ? null : new VersionedTenantPreferences(Row, 1));

        public Task<TenantPreferences?> GetForUpdateAsync(Guid tenantId, uint expectedVersion, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public uint CurrentVersion(TenantPreferences tracked) => 1;

        public Task<bool> ExistsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(Row is not null);

        public Task AddAsync(TenantPreferences preferences, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> HasStoredMoneyAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
