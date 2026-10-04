using System.Net;

namespace RugsManagment.IntegrationTests.Support;

/// <summary>
/// یک «مرورگر» برای تست HTTP: کلاینت بدون دنبال کردن ریدایرکت + ظرف کوکیِ دستی.
///
/// ظرف کوکی دستی است (نه CookieContainer) تا تست بتواند کوکی ورود را «بدزدد» (<see cref="Clone"/>) و
/// جدا از نشست اصلی به کار ببرد — همان چیزی که سناریوهای ابطال نشست لازم دارند.
/// </summary>
public sealed class WebSession(HttpClient client)
{
    /// <summary>نام کوکی ورود در Program.cs؛ اگر بزرگ شود با پسوند C1، C2، … تکه‌تکه می‌شود.</summary>
    public const string AuthCookieName = "rugs.auth";

    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    public HttpClient Client { get; } = client;

    public bool HasAuthCookie => _cookies.Keys.Any(k => k.StartsWith(AuthCookieName, StringComparison.Ordinal));

    /// <summary>مقدار فعلی کوکی ورود (برای مقایسهٔ قبل/بعد)؛ null اگر نشستی نیست.</summary>
    public string? AuthCookie => HasAuthCookie
        ? string.Join("; ", _cookies.Where(c => c.Key.StartsWith(AuthCookieName, StringComparison.Ordinal))
            .OrderBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => $"{c.Key}={c.Value}"))
        : null;

    private string? CookieHeader => _cookies.Count == 0
        ? null
        : string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}"));

    /// <summary>
    /// همان کوکی‌ها روی یک «دستگاه» دیگر (مثلاً کوکیِ دزدیده‌شده). از این به بعد دو ظرف مستقل‌اند: کوکی‌ای که
    /// سرور به یکی می‌دهد یا از یکی پاک می‌کند روی دیگری اثر ندارد.
    /// </summary>
    public WebSession Clone()
    {
        var copy = new WebSession(Client);
        foreach (var (name, value) in _cookies)
            copy._cookies[name] = value;
        return copy;
    }

    public async Task<HttpResponseMessage> GetAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (CookieHeader is { } cookie)
            request.Headers.Add("Cookie", cookie);

        var response = await Client.SendAsync(request);
        Absorb(response);
        return response;
    }

    /// <summary>صفحهٔ <paramref name="pageUrl"/> را برای توکن ضد CSRF باز می‌کند و فرم را به <paramref name="postUrl"/> می‌فرستد.</summary>
    public async Task<HttpResponseMessage> PostFormAsync(
        string pageUrl, string postUrl, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var tokens = await AntiforgeryHelper.FetchAsync(Client, pageUrl, CookieHeader);
        foreach (var (name, value) in tokens.Cookies)
            _cookies[name] = value;

        // کوکی antiforgery حالا داخل ظرف است و با بقیه فرستاده می‌شود
        using var request = AntiforgeryHelper.CreateFormPost(
            postUrl, tokens with { Cookies = new Dictionary<string, string>() }, fields, CookieHeader);

        var response = await Client.SendAsync(request);
        Absorb(response);
        return response;
    }

    /// <summary>ورود واقعی از صفحهٔ ورود؛ باید با ریدایرکت و کوکی ورود تمام شود.</summary>
    public async Task LoginAsync(string email, string password)
    {
        using var response = await PostFormAsync("/account/login", "/account/login",
        [
            new("Email", email),
            new("Password", password),
            new("RememberMe", "true")
        ]);

        if (response.StatusCode != HttpStatusCode.Redirect || !HasAuthCookie)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"ورود {email} ناموفق بود: {(int)response.StatusCode}، کوکی ورود: {HasAuthCookie}. {Truncate(body)}");
        }
    }

    private void Absorb(HttpResponseMessage response)
    {
        foreach (var (name, value) in CookieParser.ReadSetCookieChanges(response))
        {
            if (value is null)
                _cookies.Remove(name);
            else
                _cookies[name] = value;
        }
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300] + "…";
}

/// <summary>کمکی‌های بررسی پاسخ برای تست‌های HTTP.</summary>
public static class HttpAssert
{
    /// <summary>ریدایرکت به صفحهٔ ورود = کوکی پذیرفته نشد (یا اصلاً نبود).</summary>
    public static void RedirectsToLogin(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/account/login", LocationPath(response), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>مسیرِ سربرگ Location؛ ریدایرکت کوکی مطلق است (http://localhost/…) و ریدایرکت اکشن‌ها نسبی.</summary>
    public static string LocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        if (location is null)
            return string.Empty;
        return location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString.Split('?')[0];
    }

    public static void Ok(HttpResponseMessage response)
        => Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
