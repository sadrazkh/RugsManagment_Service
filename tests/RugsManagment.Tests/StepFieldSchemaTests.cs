using RugsManagment.Application.Common;

namespace RugsManagment.Tests;

/// <summary>
/// اعتبارسنجی فرم داینامیک مرحله — مرز اعتماد سرور به ورودی کاربر.
/// </summary>
public class StepFieldSchemaTests
{
    private const string ValidSchema = """
        [
          {"key":"color_code","label":"کد رنگ","type":0,"required":true},
          {"key":"temperature","label":"دمای آب","type":1,"required":false},
          {"key":"dye_type","label":"نوع رنگ","type":3,"required":true,"options":["گیاهی","شیمیایی"]}
        ]
        """;

    // ── اعتبارسنجی اسکیما ──

    [Fact]
    public void EmptySchema_IsValid() => Assert.True(StepFieldSchema.IsValid(null));

    [Fact]
    public void WellFormedSchema_IsValid() => Assert.True(StepFieldSchema.IsValid(ValidSchema));

    [Fact]
    public void MalformedJson_IsRejected() => Assert.False(StepFieldSchema.IsValid("{ not json"));

    [Fact]
    public void KeyWithSpace_IsRejected()
        => Assert.False(StepFieldSchema.IsValid("""[{"key":"a b","label":"x","type":0}]"""));

    [Fact]
    public void DuplicateKeys_AreRejected()
        => Assert.False(StepFieldSchema.IsValid(
            """[{"key":"c","label":"x","type":0},{"key":"c","label":"y","type":0}]"""));

    [Fact]
    public void SelectWithoutOptions_IsRejected()
        => Assert.False(StepFieldSchema.IsValid("""[{"key":"c","label":"رنگ","type":3}]"""));

    [Fact]
    public void EmptyLabel_IsRejected()
        => Assert.False(StepFieldSchema.IsValid("""[{"key":"c","label":"","type":0}]"""));

    [Fact]
    public void TooManyFields_AreRejected()
    {
        var fields = Enumerable.Range(0, StepFieldSchema.MaxFields + 1)
            .Select(i => $$"""{"key":"f{{i}}","label":"فیلد {{i}}","type":0}""");

        Assert.False(StepFieldSchema.IsValid($"[{string.Join(',', fields)}]"));
    }

    // ── اعتبارسنجی مقادیر ──

    [Fact]
    public void MissingRequiredValue_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StepFieldSchema.ValidateValues(ValidSchema, """{"temperature":"60"}"""));

        Assert.Contains("کد رنگ", ex.Message);
    }

    [Fact]
    public void OptionOutsideList_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StepFieldSchema.ValidateValues(
                ValidSchema, """{"color_code":"RD-1","dye_type":"نامعتبر"}"""));

        Assert.Contains("نوع رنگ", ex.Message);
    }

    [Fact]
    public void NonNumericValueForNumberField_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StepFieldSchema.ValidateValues(
                ValidSchema,
                """{"color_code":"RD-1","dye_type":"گیاهی","temperature":"خیلی داغ"}"""));

        Assert.Contains("دمای آب", ex.Message);
    }

    [Fact]
    public void PersianDigits_AreConvertedToNumber()
    {
        var result = StepFieldSchema.ValidateValues(
            ValidSchema,
            """{"color_code":"RD-1","dye_type":"گیاهی","temperature":"۶۵"}""");

        Assert.Contains("65", result);
    }

    [Fact]
    public void UnknownKeys_AreDroppedSilently()
    {
        // مرز امنیتی: کسی نباید بتواند دادهٔ دلخواه در ستون jsonb بریزد
        var result = StepFieldSchema.ValidateValues(
            ValidSchema,
            """{"color_code":"RD-1","dye_type":"گیاهی","injected":"مهاجم"}""");

        Assert.DoesNotContain("injected", result);
        Assert.DoesNotContain("مهاجم", result);
    }

    [Fact]
    public void OptionalEmptyField_IsOmittedNotStoredAsEmpty()
    {
        var result = StepFieldSchema.ValidateValues(
            ValidSchema, """{"color_code":"RD-1","dye_type":"گیاهی","temperature":""}""");

        Assert.DoesNotContain("temperature", result);
    }

    [Fact]
    public void NoSchema_IgnoresValuesEntirely()
    {
        // مرحله‌ای که فرم ندارد نباید مقدار دلخواه ذخیره کند
        Assert.Null(StepFieldSchema.ValidateValues(null, """{"anything":"x"}"""));
    }

    [Fact]
    public void BrokenValuesJson_Throws()
        => Assert.Throws<InvalidOperationException>(
            () => StepFieldSchema.ValidateValues(ValidSchema, "{ خراب"));
}
