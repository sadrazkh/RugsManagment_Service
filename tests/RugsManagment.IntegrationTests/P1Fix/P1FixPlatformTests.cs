using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.Support;
using RugsManagment.Web.Models.Admin;

namespace RugsManagment.IntegrationTests.P1Fix;

/// <summary>
/// یافته‌های بازبینی فاز ۱ در مسیرهای وب: صفحه‌بندی تاریخچه با شمارهٔ صفحهٔ بزرگ، رمز اولین تنظیم‌کنندهٔ کارگاه، و
/// «بزرگ‌ترین ضلع معمول» کارگاه در app-context.
/// </summary>
public sealed partial class P1FixPlatformTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    // ── تاریخچهٔ فعالیت: صفحهٔ بزرگ ۵۰۰ نمی‌دهد ───────────────────────────

    [PostgresTheory]
    [InlineData("80000000")]
    [InlineData("2147483647")]
    [InlineData("-5")]
    [InlineData("0")]
    public async Task ActivityPage_WithAnyPageNumber_Is200(string page)
    {
        var (tenantId, adminId) = await factory.GetDemoTenantAsync();
        using var client = factory.CreateClientAs(TestPrincipal.TenantAdmin(adminId, tenantId));

        using var response = await client.GetAsync($"/Settings/Activity?page={page}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [PostgresFact]
    public async Task ActivityPage_PastTheLastPage_ShowsTheLastPage_NotAnEmptyOne()
    {
        var (tenantId, adminId) = await factory.GetDemoTenantAsync();
        await using (var db = factory.CreateDbContext())
        {
            // دست‌کم یک رویداد، تا «صفحهٔ آخر» چیزی برای نشان دادن داشته باشد
            db.AuditEntries.Add(new AuditEntry { TenantId = tenantId, Action = AuditAction.Updated, EntityType = "Rug", Summary = "p1fix paging" });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClientAs(TestPrincipal.TenantAdmin(adminId, tenantId));
        using var response = await client.GetAsync("/Settings/Activity?page=80000000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("رویدادی یافت نشد", await response.Content.ReadAsStringAsync());
    }

    // ── رمز اولین تنظیم‌کننده در ساخت کارگاه ───────────────────────────────

    [PostgresFact]
    public async Task CreateTenant_WithASixCharacterPassword_IsRefused_AndNothingIsCreated()
    {
        var slug = $"p1fix-{Guid.NewGuid():N}"[..20];
        using var response = await PostCreateTenantAsync(slug, "abc123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // همان فرم با پیام، نه ریدایرکت
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(WebUtility.HtmlEncode(PasswordPolicy.TooShortMessage), html);

        await using var db = factory.CreateDbContext();
        Assert.False(await db.Tenants.AnyAsync(t => t.Slug == slug));
    }

    [PostgresFact]
    public async Task CreateTenant_WithAPolicyPassword_Works()
    {
        var slug = $"p1fix-{Guid.NewGuid():N}"[..20];
        using var response = await PostCreateTenantAsync(slug, "Passw0rd-8");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var db = factory.CreateDbContext();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Slug == slug);
        Assert.True(await db.Users.AnyAsync(u => u.TenantId == tenant.Id && u.Role == UserRole.TenantAdmin));
    }

    [Fact]
    public void CreateTenantForm_UsesThePasswordPolicy()
    {
        var attribute = typeof(CreateTenantViewModel).GetProperty(nameof(CreateTenantViewModel.AdminPassword))!
            .GetCustomAttribute<MinLengthAttribute>()!;
        Assert.Equal(PasswordPolicy.MinLength, attribute.Length);
        Assert.Equal(PasswordPolicy.TooShortMessage, attribute.ErrorMessage);
    }

    private async Task<HttpResponseMessage> PostCreateTenantAsync(string slug, string password)
    {
        var systemAdmin = await factory.GetSystemAdminIdAsync();
        using var client = factory.CreateClientAs(TestPrincipal.SystemAdmin(systemAdmin));
        return await AntiforgeryHelper.PostFormAsync(client, "/Admin/Create", "/Admin/Create",
        [
            new("Name", "کارگاه آزمون رمز"),
            new("Slug", slug),
            new("AdminFullName", "مدیر آزمون"),
            new("AdminEmail", $"{slug}@it.test"),
            new("AdminPassword", password)
        ]);
    }

    // ── «بزرگ‌ترین ضلع معمول» در app-context ──────────────────────────────

    [PostgresFact]
    public async Task AppContext_CarriesTheTenantsMaxPlausibleSide()
    {
        var tenantId = await factory.CreateTenantAsync();
        await using (var db = factory.CreateDbContext())
        {
            var preferences = TenantPreferences.CreateDefault(tenantId);
            preferences.MaxPlausibleSideM = 8m;
            db.Set<TenantPreferences>().Add(preferences);
            await db.SaveChangesAsync();
        }

        var merchant = await factory.CreateUserAsync(tenantId, UserRole.Merchant);
        using var client = factory.CreateClientAs(new TestPrincipal(merchant.Id, UserRole.Merchant, tenantId));
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = AppContextJson().Match(await response.Content.ReadAsStringAsync());
        Assert.True(json.Success, "app-context در صفحه نیست.");
        using var doc = JsonDocument.Parse(WebUtility.HtmlDecode(json.Groups["json"].Value));
        Assert.Equal(8m, doc.RootElement.GetProperty("maxPlausibleSideM").GetDecimal());
    }

    [GeneratedRegex("""<script type="application/json" id="app-context">(?<json>.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex AppContextJson();
}
