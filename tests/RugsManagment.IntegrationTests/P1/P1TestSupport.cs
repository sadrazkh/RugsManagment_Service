using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.P1;

/// <summary>
/// کلاینت HTTP با هویتِ سربرگ تست (بدون صفحهٔ ورود) که برای POST/PUT/DELETEهای API توکن ضد CSRF را هم
/// می‌گیرد: یک بار صفحهٔ پروفایل را با همان هویت باز می‌کند و توکن و کوکی antiforgery را نگه می‌دارد، مثل
/// جزیره‌ای که توکن را از <c>&lt;meta name="csrf-token"&gt;</c> می‌خواند.
/// </summary>
internal sealed class P1Client(HttpClient client) : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string? _token;
    private string _cookies = string.Empty;

    public HttpClient Http { get; } = client;

    public static P1Client As(RugsWebFactory factory, TestPrincipal principal)
        => new(factory.CreateClientAs(principal));

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url);

    public async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string url, object? body)
    {
        await EnsureTokenAsync();

        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        request.Headers.Add("X-CSRF-TOKEN", _token);
        if (_cookies.Length > 0)
            request.Headers.Add("Cookie", _cookies);

        return await Http.SendAsync(request);
    }

    /// <summary>بدنهٔ JSON خام (برای مقدارهایی مثل نام enum که شیء C# آن را نمی‌سازد).</summary>
    public async Task<HttpResponseMessage> SendRawJsonAsync(HttpMethod method, string url, string json)
    {
        await EnsureTokenAsync();

        var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-CSRF-TOKEN", _token);
        if (_cookies.Length > 0)
            request.Headers.Add("Cookie", _cookies);

        return await Http.SendAsync(request);
    }

    private async Task EnsureTokenAsync()
    {
        if (_token is not null) return;

        var tokens = await AntiforgeryHelper.FetchAsync(Http, "/Account/Profile");
        _token = tokens.FormToken;
        _cookies = tokens.CookieHeader;
    }

    public void Dispose() => Http.Dispose();
}

/// <summary>داده و ابزار مشترک آزمون‌های فاز ۱.</summary>
internal static class P1Data
{
    /// <summary>
    /// کلیدهای JSON که مبلغ تومانی‌اند (A.5 / L.2). در پاسخِ کسی که Business نیست هیچ‌کدام نباید مقدار
    /// غیر null داشته باشد. «costs» کل بخش خلاصهٔ هزینهٔ فرش است.
    /// </summary>
    public static readonly HashSet<string> MoneyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "totalInvestment", "purchaseCost", "costPerM2", "targetSalePrice", "estimatedMargin",
        "outstandingReceivable", "effectiveCost", "calculatedCost", "appliedUnitRate", "manualCostOverride",
        "adjustment", "pricingConfigJson", "defaultUnitRate", "totalProcessCost", "actualSaleAmount",
        "actualProfit", "pipelineValue", "profitEstimate", "readyForSaleValue", "actualSalesTotal",
        "actualProfitTotal", "openShipmentCostPerM2", "unallocatedEntries", "costs", "salePrice",
        "netAmount", "receivedAmount", "discount", "unitRate", "costAtSale", "investmentTotal", "profitTotal",
        // فاز ۲: سطرهای خرید و بها هر تخته
        "purchaseLines", "costPerPiece"
    };

    /// <summary>کارگاه دمو با مدیرش و یک بازرگان و یک انباردارِ واقعی (برای صفحهٔ پروفایل و توکن CSRF).</summary>
    public static async Task<TenantPeople> PeopleAsync(RugsWebFactory factory)
    {
        var (tenantId, adminId) = await factory.GetDemoTenantAsync();
        var merchant = await factory.CreateUserAsync(tenantId, UserRole.Merchant);
        var op = await factory.CreateUserAsync(tenantId, UserRole.Operator);
        return new TenantPeople(
            tenantId,
            TestPrincipal.TenantAdmin(adminId, tenantId),
            new TestPrincipal(merchant.Id, UserRole.Merchant, tenantId),
            TestPrincipal.Operator(op.Id, tenantId),
            merchant,
            op);
    }

    /// <summary>فرش‌های کارگاه دمو (دادهٔ اولیه).</summary>
    public static async Task<IReadOnlyList<Guid>> DemoRugIdsAsync(RugsWebFactory factory, Guid tenantId)
    {
        await using var db = factory.CreateDbContext();
        return await db.Rugs.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Sku)
            .Select(r => r.Id)
            .ToListAsync();
    }

    /// <summary>
    /// همهٔ مبلغ‌های غیر صفری که Business از این فرش‌ها می‌بیند (≥ ۱۰٬۰۰۰ تا عددهای کوچک تصادفاً با شمارنده یا
    /// اندازه‌ای یکی نشوند) — همان‌هایی که نباید در هیچ پاسخِ انباردار بیایند.
    /// </summary>
    public static async Task<IReadOnlyList<decimal>> BusinessMoneyValuesAsync(P1Client business, IEnumerable<Guid> rugIds)
    {
        var values = new HashSet<decimal>();
        foreach (var id in rugIds)
        {
            using var response = await business.GetAsync($"/api/rugs/{id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            CollectMoney(doc.RootElement, insideMoney: false, values);
        }

        return values.Where(v => Math.Abs(v) >= 10_000).OrderBy(v => v).ToList();
    }

    /// <summary>
    /// عددهای JSON (هر کلیدی، نه فقط کلیدهای پولی شناخته‌شده) که با یکی از مبلغ‌ها برابرند — مسیر هر کدام. اگر
    /// مبلغی با نام تازه‌ای در DTO بیاید هم گرفته می‌شود.
    /// </summary>
    public static IReadOnlyList<string> NumbersEqualTo(JsonElement element, IReadOnlyCollection<decimal> money)
    {
        var found = new List<string>();
        Walk(element, "$", found);
        return found;

        void Walk(JsonElement e, string at, List<string> list)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject())
                        Walk(p.Value, $"{at}.{p.Name}", list);
                    break;
                case JsonValueKind.Array:
                    var i = 0;
                    foreach (var item in e.EnumerateArray())
                        Walk(item, $"{at}[{i++}]", list);
                    break;
                case JsonValueKind.Number when e.TryGetDecimal(out var number) && money.Contains(number):
                    list.Add($"{at}={number}");
                    break;
            }
        }
    }

    /// <summary>کلیدهای پولیِ غیر null در یک سند JSON (مسیر هر کدام)، برای پیام شکست خوانا.</summary>
    public static IReadOnlyList<string> NonNullMoneyKeys(JsonElement element, string path = "$")
    {
        var found = new List<string>();
        Walk(element, path, found);
        return found;

        static void Walk(JsonElement e, string at, List<string> found)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in e.EnumerateObject())
                    {
                        var child = $"{at}.{p.Name}";
                        if (MoneyKeys.Contains(p.Name) && p.Value.ValueKind != JsonValueKind.Null)
                            found.Add(child);
                        else
                            Walk(p.Value, child, found);
                    }
                    break;
                case JsonValueKind.Array:
                    var i = 0;
                    foreach (var item in e.EnumerateArray())
                        Walk(item, $"{at}[{i++}]", found);
                    break;
            }
        }
    }

    private static void CollectMoney(JsonElement e, bool insideMoney, HashSet<decimal> values)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                    CollectMoney(p.Value, insideMoney || MoneyKeys.Contains(p.Name), values);
                break;
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray())
                    CollectMoney(item, insideMoney, values);
                break;
            case JsonValueKind.Number when insideMoney && e.TryGetDecimal(out var number) && number != 0:
                values.Add(number);
                break;
        }
    }

    /// <summary>
    /// شکل‌هایی که یک مبلغ در HTML ممکن است داشته باشد: قالب صفحه با ارقام فارسی («۴۵,۰۰۰,۰۰۰») و قالب پیام‌ها
    /// («۴۵٬۰۰۰٬۰۰۰»). عدد لاتین جست‌وجو نمی‌شود چون با تاریخ و شناسه‌های صفحه تصادفاً یکی می‌شد.
    /// </summary>
    public static IEnumerable<string> RenderedForms(decimal value)
    {
        yield return Web.Support.PersianFormat.Money(value);
        yield return Application.Common.Messages.FormatNumber(decimal.Round(value, 0, MidpointRounding.AwayFromZero));
    }
}

/// <summary>
/// Theory برای تستی که پستگرس واقعی لازم دارد — همتای <see cref="PostgresFactAttribute"/>: بدون
/// <c>RUGS_TEST_PG</c> (و بیرون از CI) رد می‌شود.
/// </summary>
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (PostgresTestEnvironment.ShouldSkip)
            Skip = $"{PostgresTestEnvironment.VariableName} تنظیم نشده است؛ تست پستگرس رد شد.";
    }
}

/// <summary>آدم‌های یک کارگاه در آزمون: سه نقش کارگاهی با هویت سربرگ.</summary>
internal sealed record TenantPeople(
    Guid TenantId,
    TestPrincipal Admin,
    TestPrincipal Merchant,
    TestPrincipal Operator,
    SeededUser MerchantUser,
    SeededUser OperatorUser);
