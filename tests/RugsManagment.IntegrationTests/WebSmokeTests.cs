using System.Net;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// دودآزمایی میزبان Web: برنامهٔ واقعی روی دیتابیس دورریختنی بالا می‌آید (migration + دادهٔ اولیه)،
/// سلامت بدون ورود جواب می‌دهد و قوانین دسترسی با هویتِ سربرگ تست اعمال می‌شوند.
/// </summary>
public sealed class WebSmokeTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task Health_IsAnonymous()
    {
        using var client = factory.CreateRawClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [PostgresFact]
    public async Task TenantAdmin_SeesRugList()
    {
        var (tenantId, adminId) = await factory.GetDemoTenantAsync();
        using var client = factory.CreateClientAs(TestPrincipal.TenantAdmin(adminId, tenantId));

        using var response = await client.GetAsync("/rugs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [PostgresFact]
    public async Task Operator_IsKeptOutOfTenantSettings()
    {
        var (tenantId, _) = await factory.GetDemoTenantAsync();
        var operatorUser = await factory.CreateUserAsync(tenantId, Domain.Enums.UserRole.Operator);
        using var client = factory.CreateClientAs(TestPrincipal.Operator(operatorUser.Id, tenantId));

        using var response = await client.GetAsync("/Settings/Tenant");
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Forbidden });
    }

    [PostgresFact]
    public async Task Anonymous_IsSentToLogin()
    {
        using var client = factory.CreateRawClient();
        using var response = await client.GetAsync("/rugs");
        HttpAssert.RedirectsToLogin(response);
    }
}
