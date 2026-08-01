using Microsoft.AspNetCore.Razor.TagHelpers;

namespace RugsManagment.Web.Support;

/// <summary>
/// آیکون SVG از اسپرایت /icons.svg — جایگزین ایموجی در رابط کاربری.
///
/// استفاده:  &lt;icon name="dashboard" /&gt;
///          &lt;icon name="trash" class="h-4 w-4 text-error" /&gt;
///          &lt;icon name="check" label="تکمیل شد" /&gt;   ← وقتی آیکون تنها معنا را می‌رساند
///
/// اگر label داده نشود آیکون تزئینی فرض شده و از درخت دسترس‌پذیری پنهان می‌شود
/// (aria-hidden) تا صفحه‌خوان متن دکمهٔ کنارش را دوباره تکرار نکند.
/// </summary>
[HtmlTargetElement("icon", TagStructure = TagStructure.WithoutEndTag)]
public sealed class IconTagHelper : TagHelper
{
    /// <summary>شناسهٔ symbol در فایل اسپرایت (مثلاً dashboard)</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>اگر آیکون خودش معنا دارد (دکمهٔ بدون متن)، برچسب دسترس‌پذیری آن</summary>
    public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;

        // اندازهٔ پیش‌فرض ۲۰px؛ با کلاس h-*/w-* در استفاده قابل تغییر است
        var existingClass = output.Attributes["class"]?.Value?.ToString();
        output.Attributes.SetAttribute("class",
            string.IsNullOrWhiteSpace(existingClass) ? "h-5 w-5 shrink-0" : existingClass + " shrink-0");

        if (string.IsNullOrWhiteSpace(Label))
        {
            output.Attributes.SetAttribute("aria-hidden", "true");
            output.Attributes.SetAttribute("focusable", "false");
        }
        else
        {
            output.Attributes.SetAttribute("role", "img");
            output.Attributes.SetAttribute("aria-label", Label);
        }

        output.Content.SetHtmlContent($"<use href=\"/icons.svg#{Name}\"></use>");
    }
}
