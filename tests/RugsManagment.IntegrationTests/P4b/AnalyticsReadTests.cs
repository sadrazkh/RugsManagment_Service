using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;

namespace RugsManagment.IntegrationTests.P4b;

public sealed class AnalyticsReadTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task Analytics_IsTenantScoped_AndReturnsEmptyJalaliMonthsWithoutLoadingEntities()
    {
        var own = await P2Tenant.CreateAsync(factory);
        var other = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, own.Merchant);
        using var stranger = P1Client.As(factory, other.Merchant);
        await CreateRugAsync(merchant, Values(("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)));
        var mine = await GetJsonAsync(merchant, "/api/analytics?months=3");
        Assert.Equal(3, mine.GetProperty("trend").GetArrayLength());
        Assert.Equal(1, mine.GetProperty("trend").EnumerateArray().Sum(x => x.GetProperty("rugsAdded").GetInt32()));
        var theirs = await GetJsonAsync(stranger, "/api/analytics?months=3");
        Assert.All(theirs.GetProperty("trend").EnumerateArray(), x => Assert.Equal(0, x.GetProperty("rugsAdded").GetInt32()));
        Assert.Empty(theirs.GetProperty("stepBreakdown").EnumerateArray());
        Assert.Empty(theirs.GetProperty("aging").GetProperty("items").EnumerateArray());
    }
}
