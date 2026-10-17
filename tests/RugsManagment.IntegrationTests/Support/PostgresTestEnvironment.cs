namespace RugsManagment.IntegrationTests.Support;

/// <summary>
/// تنظیم محیط تست‌های پستگرس.
///
/// <c>RUGS_TEST_PG</c> رشتهٔ اتصال پایه است، بدون Database؛ مثلاً
/// <c>Host=localhost;Port=5432;Username=postgres;Password=…</c>. کاربرش باید اجازهٔ CREATE DATABASE داشته باشد.
/// </summary>
public static class PostgresTestEnvironment
{
    public const string VariableName = "RUGS_TEST_PG";

    public static string? BaseConnectionString
        => Environment.GetEnvironmentVariable(VariableName) is { Length: > 0 } value ? value : null;

    /// <summary>GitHub Actions (و بیشتر CIها) <c>CI=true</c> می‌گذارند.</summary>
    public static bool IsCi
        => string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// روی ماشین توسعه بدون متغیر، تست رد می‌شود؛ در CI نباید بی‌صدا رد شود چون آن‌وقت سبزِ دروغین
    /// می‌دیدیم — پس اجرا می‌شود و با پیام روشن شکست می‌خورد.
    /// </summary>
    public static bool ShouldSkip => BaseConnectionString is null && !IsCi;

    public static string RequireBaseConnectionString()
        => BaseConnectionString ?? throw new InvalidOperationException(
            $"متغیر محیطی {VariableName} تنظیم نشده است (در CI اجباری است). " +
            "نمونه: Host=localhost;Port=5432;Username=postgres;Password=postgres");
}

/// <summary>
/// Fact برای تستی که پستگرس واقعی لازم دارد؛ بدون <c>RUGS_TEST_PG</c> (و بیرون از CI) رد می‌شود.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (PostgresTestEnvironment.ShouldSkip)
            Skip = $"{PostgresTestEnvironment.VariableName} تنظیم نشده است؛ تست پستگرس رد شد.";
    }
}
