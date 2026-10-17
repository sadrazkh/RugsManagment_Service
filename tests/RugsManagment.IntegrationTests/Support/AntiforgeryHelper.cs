using System.Net;
using System.Text.RegularExpressions;

namespace RugsManagment.IntegrationTests.Support;

/// <summary>توکن ضد CSRF یک صفحه: مقدار فیلد فرم و کوکی‌هایی که همان پاسخ گذاشت (کوکی antiforgery).</summary>
public sealed record AntiforgeryTokens(string FormToken, IReadOnlyDictionary<string, string> Cookies)
{
    /// <summary>کوکی‌های همین پاسخ به شکل سربرگ Cookie (خالی اگر کوکی تازه‌ای نگذاشته باشد).</summary>
    public string CookieHeader => string.Join("; ", Cookies.Select(c => $"{c.Key}={c.Value}"));
}

/// <summary>
/// POST به اکشن‌های MVC بدون توکن ضد CSRF با ۴۰۰ رد می‌شود ([ValidateAntiForgeryToken]). این کمکی همان
/// کاری را می‌کند که مرورگر می‌کند: صفحه را GET می‌کند، <c>__RequestVerificationToken</c> و کوکی antiforgery را
/// برمی‌دارد و هر دو را به POST می‌چسباند.
///
/// توکن به هویت درخواست گره خورده است؛ پس GET باید با همان کوکی ورود (یا همان سربرگ هویت تست) انجام شود که
/// POST با آن فرستاده می‌شود.
/// </summary>
public static class AntiforgeryHelper
{
    public const string FormFieldName = "__RequestVerificationToken";

    // Razor فیلد مخفی را به شکل <input name="__RequestVerificationToken" type="hidden" value="…" /> می‌نویسد؛
    // ترتیب ویژگی‌ها تضمین‌شده نیست، پس هر دو ترتیب پذیرفته می‌شود.
    private static readonly Regex[] FormTokenPatterns =
    [
        new("""<input[^>]*name="__RequestVerificationToken"[^>]*value="([^"]+)"[^>]*>""", RegexOptions.Compiled),
        new("""<input[^>]*value="([^"]+)"[^>]*name="__RequestVerificationToken"[^>]*>""", RegexOptions.Compiled)
    ];

    // جزیره‌های Vue همان توکن را از <meta name="csrf-token"> می‌خوانند (Layout)
    private static readonly Regex MetaTokenPattern =
        new("""<meta[^>]*name="csrf-token"[^>]*content="([^"]+)"[^>]*>""", RegexOptions.Compiled);

    /// <summary>صفحه را GET می‌کند و توکن فرم + کوکی‌های تازهٔ پاسخ را برمی‌گرداند.</summary>
    public static async Task<AntiforgeryTokens> FetchAsync(HttpClient client, string pageUrl, string? cookieHeader = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, pageUrl);
        if (!string.IsNullOrEmpty(cookieHeader))
            request.Headers.Add("Cookie", cookieHeader);

        using var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException(
                $"GET {pageUrl} برای گرفتن توکن ضد CSRF پاسخ {(int)response.StatusCode} داد (انتظار ۲۰۰).");

        var html = await response.Content.ReadAsStringAsync();
        var token = ExtractToken(html)
            ?? throw new InvalidOperationException($"صفحهٔ {pageUrl} توکن {FormFieldName} ندارد.");

        return new AntiforgeryTokens(token, CookieParser.ReadSetCookies(response));
    }

    /// <summary>توکن را از فیلد مخفی فرم، وگرنه از meta صفحه بیرون می‌کشد.</summary>
    public static string? ExtractToken(string html)
    {
        foreach (var pattern in FormTokenPatterns)
        {
            var match = pattern.Match(html);
            if (match.Success)
                return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        var meta = MetaTokenPattern.Match(html);
        return meta.Success ? WebUtility.HtmlDecode(meta.Groups[1].Value) : null;
    }

    /// <summary>
    /// درخواست POST فرم با توکن: فیلد <see cref="FormFieldName"/> اضافه می‌شود و کوکی antiforgery در کنار
    /// کوکی‌های دیگری که داده شده (مثلاً کوکی ورود) در سربرگ Cookie می‌نشیند.
    /// </summary>
    public static HttpRequestMessage CreateFormPost(
        string postUrl,
        AntiforgeryTokens tokens,
        IEnumerable<KeyValuePair<string, string>> fields,
        string? otherCookies = null)
    {
        var form = fields.Append(new KeyValuePair<string, string>(FormFieldName, tokens.FormToken)).ToList();
        var request = new HttpRequestMessage(HttpMethod.Post, postUrl) { Content = new FormUrlEncodedContent(form) };

        var cookie = string.Join("; ", new[] { otherCookies, tokens.CookieHeader }.Where(c => !string.IsNullOrEmpty(c)));
        if (cookie.Length > 0)
            request.Headers.Add("Cookie", cookie);

        return request;
    }

    /// <summary>GET صفحه برای توکن و بلافاصله POST فرم — حالت معمولِ یک کلیک کاربر روی دکمهٔ فرم.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string pageUrl,
        string postUrl,
        IEnumerable<KeyValuePair<string, string>> fields,
        string? cookieHeader = null)
    {
        var tokens = await FetchAsync(client, pageUrl, cookieHeader);
        using var request = CreateFormPost(postUrl, tokens, fields, cookieHeader);
        return await client.SendAsync(request);
    }
}

/// <summary>خواندن سربرگ‌های Set-Cookie به نام → مقدار؛ کوکیِ منقضی‌شده (حذف) مقدار null می‌گیرد.</summary>
internal static class CookieParser
{
    public static IReadOnlyDictionary<string, string> ReadSetCookies(HttpResponseMessage response)
        => ReadSetCookieChanges(response)
            .Where(c => c.Value is not null)
            .ToDictionary(c => c.Key, c => c.Value!, StringComparer.Ordinal);

    /// <summary>هر کوکی پاسخ؛ مقدار null یعنی سرور حذفش کرده (expires گذشته یا max-age=0).</summary>
    public static IReadOnlyList<KeyValuePair<string, string?>> ReadSetCookieChanges(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
            return [];

        var changes = new List<KeyValuePair<string, string?>>();
        foreach (var header in headers)
        {
            var parts = header.Split(';', StringSplitOptions.TrimEntries);
            var nameValue = parts[0];
            var separator = nameValue.IndexOf('=');
            if (separator <= 0)
                continue;

            var name = nameValue[..separator];
            var value = nameValue[(separator + 1)..];
            changes.Add(new(name, IsDeletion(parts.Skip(1), value) ? null : value));
        }

        return changes;
    }

    private static bool IsDeletion(IEnumerable<string> attributes, string value)
    {
        if (value.Length == 0)
            return true;

        foreach (var attribute in attributes)
        {
            if (attribute.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(attribute["max-age=".Length..], out var maxAge) && maxAge <= 0)
                return true;

            if (attribute.StartsWith("expires=", StringComparison.OrdinalIgnoreCase)
                && DateTimeOffset.TryParse(attribute["expires=".Length..], out var expires)
                && expires <= DateTimeOffset.UtcNow)
                return true;
        }

        return false;
    }
}
