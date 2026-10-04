using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.P2;

/// <summary>
/// یک کارگاه تازه برای آزمون‌های فاز ۲ فرش، با سه نقش و seed کامل (ردیف ترجیحات و سه نوع خرید)، تا شماره‌ها و
/// مبلغ‌های هر آزمون از دادهٔ آزمون‌های دیگر جدا بماند.
/// </summary>
internal sealed record P2Tenant(Guid TenantId, TestPrincipal Admin, TestPrincipal Merchant, TestPrincipal Operator)
{
    public static async Task<P2Tenant> CreateAsync(RugsWebFactory factory)
    {
        var tenantId = await factory.CreateTenantAsync();
        var admin = await factory.CreateUserAsync(tenantId, UserRole.TenantAdmin);
        var merchant = await factory.CreateUserAsync(tenantId, UserRole.Merchant);
        var op = await factory.CreateUserAsync(tenantId, UserRole.Operator);

        // کارگاه بعد از استارت ساخته شد، پس seed استارت به آن نرسیده؛ همان کاری که ساخت کارگاه می‌کند
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ITenantSeedRunner>().SeedTenantAsync(tenantId);

        return new P2Tenant(
            tenantId,
            TestPrincipal.TenantAdmin(admin.Id, tenantId),
            new TestPrincipal(merchant.Id, UserRole.Merchant, tenantId),
            TestPrincipal.Operator(op.Id, tenantId));
    }
}

/// <summary>کمک‌های HTTP آزمون‌های فاز ۲ فرش.</summary>
internal static class P2Http
{
    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} → {(int)response.StatusCode} (انتظار {(int)expected}): {text}");
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<JsonElement> GetJsonAsync(P1Client client, string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.GetAsync(url);
        return await JsonAsync(response, expected);
    }

    public static async Task<JsonElement> SendAsync(
        P1Client client, HttpMethod method, string url, object? body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.SendJsonAsync(method, url, body);
        return await JsonAsync(response, expected);
    }

    /// <summary>ثبت فرش به شکل فاز ۲ (فقط values) و برگرداندن DTO.</summary>
    public static Task<JsonElement> CreateRugAsync(P1Client client, Dictionary<string, object?> values, HttpStatusCode expected = HttpStatusCode.OK)
        => SendAsync(client, HttpMethod.Post, "/api/rugs", new { values }, expected);

    public static Dictionary<string, object?> Values(params (string Key, object? Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    public static decimal Dec(JsonElement element, string property) => element.GetProperty(property).GetDecimal();

    public static uint Version(JsonElement rug) => rug.GetProperty("version").GetUInt32();

    public static Guid Id(JsonElement rug) => rug.GetProperty("id").GetGuid();
}
