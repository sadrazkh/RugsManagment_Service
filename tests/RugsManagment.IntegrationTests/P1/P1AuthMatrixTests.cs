using System.Net;
using Microsoft.EntityFrameworkCore;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.P1;

/// <summary>
/// ماتریس «نقش × مسیر» فاز ۱ (L.2، K.2/P1): سه سیاست Floor / Business / Configure روی همهٔ درها.
///
/// دو راه ورود: سربرگ هویت تست برای جدول بزرگ (forbid این طرح ۴۰۳ است، هم صفحه هم API)، و کوکی واقعی برای
/// قاعدهٔ صفحه‌ها که باید ۳۰۲ به <c>/account/denied</c> بدهند در حالی که API همان‌جا ۴۰۳ JSON می‌دهد.
/// </summary>
public sealed class P1AuthMatrixTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    // ── پیکربندی (Configure): بازرگان و انباردار نه ─────────────────────────

    public static TheoryData<string> ConfigurePages => new()
    {
        "/settings", "/settings/preferences", "/Settings/Tenant", "/Settings/CustomFields",
        "/Settings/StepTypes", "/Settings/Activity", "/Users", "/Users/Create",
        "/Workflows/Create", "/Labels/Design"
    };

    public static TheoryData<string> ConfigureApis => new()
    {
        "/api/settings/preferences", "/api/custom-fields"
    };

    [PostgresTheory]
    [MemberData(nameof(ConfigurePages))]
    public async Task ConfigurePages_AreForbidden_ForMerchantAndOperator_AndOpen_ForTenantAdmin(string url)
    {
        var people = await P1Data.PeopleAsync(factory);

        using (var admin = factory.CreateClientAs(people.Admin))
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);

        foreach (var principal in new[] { people.Merchant, people.Operator })
        {
            using var client = factory.CreateClientAs(principal);
            using var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [PostgresTheory]
    [MemberData(nameof(ConfigureApis))]
    public async Task ConfigureApis_Are403_ForMerchantAndOperator(string url)
    {
        var people = await P1Data.PeopleAsync(factory);

        using (var admin = factory.CreateClientAs(people.Admin))
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);

        foreach (var principal in new[] { people.Merchant, people.Operator })
        {
            using var client = factory.CreateClientAs(principal);
            using var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [PostgresFact]
    public async Task SettingsPreferencesPut_Is403_ForMerchant()
    {
        var people = await P1Data.PeopleAsync(factory);
        using var merchant = P1Client.As(factory, people.Merchant);

        using var response = await merchant.SendRawJsonAsync(HttpMethod.Put, "/api/settings/preferences", "{}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── پول و تصمیم تجاری (Business): انباردار نه ──────────────────────────

    public static TheoryData<string> BusinessPages => new()
    {
        "/Reports", "/Sales", "/Providers", "/Rugs/Trash"
    };

    public static TheoryData<string> BusinessApis => new()
    {
        "/api/analytics", "/api/sales/report", "/api/providers", "/api/providers/balances"
    };

    [PostgresTheory]
    [MemberData(nameof(BusinessPages))]
    public async Task BusinessPages_AreOpen_ForMerchant_AndForbidden_ForOperator(string url)
    {
        var people = await P1Data.PeopleAsync(factory);

        using (var merchant = factory.CreateClientAs(people.Merchant))
            Assert.Equal(HttpStatusCode.OK, (await merchant.GetAsync(url)).StatusCode);

        using var op = factory.CreateClientAs(people.Operator);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync(url)).StatusCode);
    }

    [PostgresTheory]
    [MemberData(nameof(BusinessApis))]
    public async Task BusinessApis_AreOpen_ForMerchant_And403_ForOperator(string url)
    {
        var people = await P1Data.PeopleAsync(factory);

        using (var merchant = factory.CreateClientAs(people.Merchant))
            Assert.Equal(HttpStatusCode.OK, (await merchant.GetAsync(url)).StatusCode);

        using var op = factory.CreateClientAs(people.Operator);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync(url)).StatusCode);
    }

    [PostgresFact]
    public async Task SaleOfOneRug_Is403_ForOperator_ButAgingStaysOpen()
    {
        var people = await P1Data.PeopleAsync(factory);
        var rugId = (await P1Data.DemoRugIdsAsync(factory, people.TenantId))[0];

        using var op = factory.CreateClientAs(people.Operator);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync($"/api/sales/rug/{rugId}")).StatusCode);

        // کهنگی و گلوگاه مبلغ ندارد و برای جلو بردن کار است
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/api/analytics/aging")).StatusCode);
    }

    // ── کار روزانه (Floor): هر سه نقش کارگاه ─────────────────────────────────

    public static TheoryData<string> FloorPages => new()
    {
        "/", "/Dashboard", "/Rugs", "/Rugs/Create", "/Groups", "/Workflows", "/Labels",
        "/More", "/Help/Card", "/Account/Profile"
    };

    public static TheoryData<string> FloorApis => new()
    {
        "/api/me", "/api/me/preferences", "/api/home/summary", "/api/rugs", "/api/batches",
        "/api/lookups/step-types", "/api/lookups/custom-fields", "/api/lookups/workflow-templates",
        "/api/step-types", "/api/labels", "/api/workflows/templates"
    };

    [PostgresTheory]
    [MemberData(nameof(FloorPages))]
    public async Task FloorPages_Return200_ForEveryTenantRole(string url)
    {
        var people = await P1Data.PeopleAsync(factory);

        foreach (var principal in new[] { people.Admin, people.Merchant, people.Operator })
        {
            using var client = factory.CreateClientAs(principal);
            using var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"{principal.Role} GET {url} → {(int)response.StatusCode}");
        }
    }

    [PostgresTheory]
    [MemberData(nameof(FloorApis))]
    public async Task FloorApis_Return200_ForEveryTenantRole(string url)
    {
        var people = await P1Data.PeopleAsync(factory);

        foreach (var principal in new[] { people.Admin, people.Merchant, people.Operator })
        {
            using var client = factory.CreateClientAs(principal);
            using var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"{principal.Role} GET {url} → {(int)response.StatusCode}");
        }
    }

    [PostgresFact]
    public async Task RugAndGroupDetails_Return200_ForEveryTenantRole()
    {
        var people = await P1Data.PeopleAsync(factory);
        var rugId = (await P1Data.DemoRugIdsAsync(factory, people.TenantId))[0];

        foreach (var principal in new[] { people.Admin, people.Merchant, people.Operator })
        {
            using var client = factory.CreateClientAs(principal);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Rugs/Details/{rugId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Rugs/Edit/{rugId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/rugs/{rugId}")).StatusCode);
        }
    }

    /// <summary>
    /// «هر آدرس قبلی برای نقش درست ۲۰۰ می‌دهد» — صفحه‌هایی که شناسه یا پرس‌وجو لازم دارند و در جدول‌های بالا
    /// نیستند: جزئیات ورودی، چاپ برچسب، چاپ و CSV فروش، صورت‌حساب کارگاه، ویرایش قالب گردش کار و طراحی برچسب.
    /// </summary>
    [PostgresFact]
    public async Task PreExistingPagesWithIds_OpenForTheRightRole_Only()
    {
        var people = await P1Data.PeopleAsync(factory);
        var rugId = (await P1Data.DemoRugIdsAsync(factory, people.TenantId))[0];

        Guid batchId, templateId, labelId;
        var provider = new Domain.Entities.ServiceProvider { TenantId = people.TenantId, Name = "IT provider", IsActive = true };
        await using (var db = factory.CreateDbContext())
        {
            batchId = await db.RugBatches.AsNoTracking()
                .Where(b => b.TenantId == people.TenantId).Select(b => b.Id).FirstAsync();
            templateId = await db.WorkflowTemplates.AsNoTracking()
                .Where(t => t.TenantId == people.TenantId).Select(t => t.Id).FirstAsync();
            labelId = await db.LabelTemplates.AsNoTracking()
                .Where(l => l.TenantId == people.TenantId).Select(l => l.Id).FirstAsync();
            db.ServiceProviders.Add(provider);
            await db.SaveChangesAsync();
        }

        var everyone = new[] { people.Admin, people.Merchant, people.Operator };

        // Floor
        foreach (var url in new[] { $"/Groups/Details/{batchId}", $"/Labels/Print?rugIds={rugId}" })
        {
            foreach (var principal in everyone)
            {
                using var client = factory.CreateClientAs(principal);
                using var response = await client.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{principal.Role} GET {url} → {(int)response.StatusCode}");
            }
        }

        // «/Help» همان کارت راهنماست
        using (var op = factory.CreateClientAs(people.Operator))
        {
            using var help = await op.GetAsync("/Help");
            Assert.Equal(HttpStatusCode.Redirect, help.StatusCode);
            Assert.Equal("/Help/Card", HttpAssert.LocationPath(help), StringComparer.OrdinalIgnoreCase);
        }

        // Business
        foreach (var url in new[] { "/Sales/Print", "/Sales/ExportCsv", $"/Providers/Statement/{provider.Id}" })
        {
            foreach (var principal in new[] { people.Admin, people.Merchant })
            {
                using var client = factory.CreateClientAs(principal);
                using var response = await client.GetAsync(url);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{principal.Role} GET {url} → {(int)response.StatusCode}");
            }

            using var op = factory.CreateClientAs(people.Operator);
            Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync(url)).StatusCode);
        }

        // Configure
        foreach (var url in new[] { $"/Workflows/Edit/{templateId}", $"/Labels/Design?id={labelId}" })
        {
            using (var admin = factory.CreateClientAs(people.Admin))
                Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);

            foreach (var principal in new[] { people.Merchant, people.Operator })
            {
                using var client = factory.CreateClientAs(principal);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
            }
        }
    }

    [PostgresFact]
    public async Task SystemAdmin_IsKeptOutOfTenantApis()
    {
        var systemAdminId = await factory.GetSystemAdminIdAsync();
        using var client = factory.CreateClientAs(TestPrincipal.SystemAdmin(systemAdminId));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/rugs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/home/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin")).StatusCode);
    }

    // ── خانه و صفحه‌های خطا ────────────────────────────────────────────────

    [PostgresFact]
    public async Task Home_SignedOut_GoesToLogin_AndSystemAdmin_GoesToAdmin()
    {
        using (var anonymous = factory.CreateRawClient())
            HttpAssert.RedirectsToLogin(await anonymous.GetAsync("/"));

        var systemAdminId = await factory.GetSystemAdminIdAsync();
        using var admin = factory.CreateClientAs(TestPrincipal.SystemAdmin(systemAdminId));
        using var response = await admin.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin", HttpAssert.LocationPath(response), StringComparer.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task ErrorPages_WorkAnonymously()
    {
        using var anonymous = factory.CreateRawClient();

        using (var notFound = await anonymous.GetAsync("/status/404"))
        {
            Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
            Assert.Contains("text/html", notFound.Content.Headers.ContentType?.MediaType ?? string.Empty);
        }

        using var error = await anonymous.GetAsync("/Home/Error");
        Assert.Equal(HttpStatusCode.OK, error.StatusCode);
    }

    [PostgresFact]
    public async Task Api_SignedOut_Is401_NotARedirect()
    {
        using var anonymous = factory.CreateRawClient();
        using var response = await anonymous.GetAsync("/api/rugs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── کوکی واقعی: صفحه ۳۰۲ به «دسترسی مجاز نیست»، API ۴۰۳ ─────────────────

    [PostgresFact]
    public async Task Merchant_WithCookie_GetsDeniedRedirect_OnConfigurePages_And403_OnSettingsApi()
    {
        var people = await P1Data.PeopleAsync(factory);
        var session = factory.NewSession();
        await session.LoginAsync(people.MerchantUser.Email, people.MerchantUser.Password);

        foreach (var url in new[] { "/settings", "/settings/preferences", "/Settings/Tenant", "/Users" })
        {
            using var page = await session.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
            Assert.Equal("/account/denied", HttpAssert.LocationPath(page), StringComparer.OrdinalIgnoreCase);
        }

        using var api = await session.GetAsync("/api/settings/preferences");
        Assert.Equal(HttpStatusCode.Forbidden, api.StatusCode);
        Assert.Contains("application/json", api.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    [PostgresFact]
    public async Task Operator_WithCookie_GetsDeniedRedirect_OnReports_AndLandsOnHome_AfterLogin()
    {
        var people = await P1Data.PeopleAsync(factory);
        var session = factory.NewSession();
        await session.LoginAsync(people.OperatorUser.Email, people.OperatorUser.Password);

        using var reports = await session.GetAsync("/Reports");
        Assert.Equal(HttpStatusCode.Redirect, reports.StatusCode);
        Assert.Equal("/account/denied", HttpAssert.LocationPath(reports), StringComparer.OrdinalIgnoreCase);

        using var home = await session.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
    }

    // ── نمای ساده: فقط پوسته، نه دسترسی ──────────────────────────────────────

    [PostgresFact]
    public async Task SimpleMode_HidesWorkshopControls_AndAdminPreviewChangesChromeOnly()
    {
        var people = await P1Data.PeopleAsync(factory);
        const string workshopMarker = "data-island=\"list-quick-advance\"";

        // تنظیم‌کننده (نمای کامل): دکمه‌های «مرحلهٔ بعد» در فهرست فرش‌ها هست
        using (var admin = factory.CreateClientAs(people.Admin))
            Assert.Contains(workshopMarker, await admin.GetStringAsync("/Rugs"));

        // بازرگان و انباردار (نمای ساده): نیست
        foreach (var principal in new[] { people.Merchant, people.Operator })
        {
            using var client = factory.CreateClientAs(principal);
            Assert.DoesNotContain(workshopMarker, await client.GetStringAsync("/Rugs"));
        }

        // پیش‌نمایش «نمای ساده»ی تنظیم‌کننده: پوسته ساده می‌شود ولی تنظیمات همچنان باز است
        using (var preview = factory.CreateClientAs(people.Admin))
        {
            preview.DefaultRequestHeaders.Add("Cookie", $"{Web.Support.UiPreview.CookieName}={Web.Support.UiPreview.OnValue}");
            Assert.DoesNotContain(workshopMarker, await preview.GetStringAsync("/Rugs"));
            Assert.Equal(HttpStatusCode.OK, (await preview.GetAsync("/settings")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await preview.GetAsync("/api/settings/preferences")).StatusCode);
        }

        // همان کوکی برای انباردار هیچ دری باز نمی‌کند
        using var op = factory.CreateClientAs(people.Operator);
        op.DefaultRequestHeaders.Add("Cookie", $"{Web.Support.UiPreview.CookieName}={Web.Support.UiPreview.OnValue}");
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync("/settings")).StatusCode);
    }

    // ── کاربران: ساختن «پدر» با نقش بازرگان ─────────────────────────────────

    [PostgresFact]
    public async Task TenantAdmin_CreatesMerchant_FromUsersForm_WithSimpleModeAndLargeText()
    {
        var people = await P1Data.PeopleAsync(factory);
        using var admin = factory.CreateClientAs(people.Admin);
        var email = $"father-{Guid.NewGuid():N}@it.test";

        using (var form = await admin.GetAsync("/Users/Create"))
        {
            var html = await form.Content.ReadAsStringAsync();
            Assert.Contains($"value=\"{(int)Domain.Enums.UserRole.Merchant}\"", html);
            Assert.Contains(Web.Support.RoleLabels.For(Domain.Enums.UserRole.Merchant), html);
        }

        using var save = await AntiforgeryHelper.PostFormAsync(admin, "/Users/Create", "/Users/Save",
        [
            new("FullName", "پدر"),
            new("Email", email),
            new("Role", ((int)Domain.Enums.UserRole.Merchant).ToString()),
            new("IsActive", "true"),
            new("Password", "father-Passw0rd-1")
        ]);
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Equal("/Users", HttpAssert.LocationPath(save), StringComparer.OrdinalIgnoreCase);

        await using var db = factory.CreateDbContext();
        var father = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.Equal(Domain.Enums.UserRole.Merchant, father.Role);
        Assert.Equal(Domain.Enums.UiMode.Simple, father.UiMode);
        Assert.Equal(Domain.Enums.TextScale.Large, father.TextScale);
        Assert.Equal(people.TenantId, father.TenantId);
    }

    // ── ترجیحات خودِ کاربر ────────────────────────────────────────────────

    [PostgresFact]
    public async Task TextScale_Persists_AndAcceptsNumberOrName()
    {
        var people = await P1Data.PeopleAsync(factory);
        using var op = P1Client.As(factory, people.Operator);

        using (var byName = await op.SendRawJsonAsync(HttpMethod.Put, "/api/me/preferences", """{"textScale":"xlarge"}"""))
            Assert.Equal(HttpStatusCode.NoContent, byName.StatusCode);
        Assert.Contains("\"textScale\":2", await (await op.GetAsync("/api/me/preferences")).Content.ReadAsStringAsync());

        using (var byNumber = await op.SendRawJsonAsync(HttpMethod.Put, "/api/me/preferences", """{"textScale":1}"""))
            Assert.Equal(HttpStatusCode.NoContent, byNumber.StatusCode);
        Assert.Contains("\"textScale\":1", await (await op.GetAsync("/api/me/preferences")).Content.ReadAsStringAsync());

        using var invalid = await op.SendRawJsonAsync(HttpMethod.Put, "/api/me/preferences", """{"textScale":9}""");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("\"textScale\"", await invalid.Content.ReadAsStringAsync());
    }
}
