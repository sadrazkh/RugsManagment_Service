using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;
using RugsManagment.Web.Controllers;
using RugsManagment.Web.Controllers.Api;
using RugsManagment.Web.Filters;

namespace RugsManagment.IntegrationTests.P1;

/// <summary>
/// <see cref="DomainExceptionPageFilter"/> (I.1): خطای دامنه در فرم‌های Razor → toast خطا + بازگشت به صفحهٔ قبل
/// (۳۰۲)، نه صفحهٔ ۵۰۰. بدون دیتابیس: فیلتر مستقیم با یک ExceptionContext ساختگی صدا زده می‌شود.
///
/// (حذف فرشِ داخل نگله که اولین مسیر واقعیِ این خطاست در فاز محموله می‌آید؛ آن‌جا آزمون HTTP خودش را دارد.)
/// </summary>
public sealed class P1DomainExceptionPageFilterTests
{
    [Fact]
    public void Conflict_OnPage_BecomesErrorToast_AndRedirectBackToReferer()
    {
        var (context, tempData) = Context<RugsController>(
            new DomainConflictException("اول این فرش را از نگلهٔ ۱۲ خارج کنید.", "rug"),
            referer: "http://localhost/Rugs/Details/42?tab=1");

        Filter().OnException(context);

        Assert.True(context.ExceptionHandled);
        var redirect = Assert.IsType<RedirectResult>(context.Result);
        Assert.Equal("/Rugs/Details/42?tab=1", redirect.Url);
        Assert.Equal("اول این فرش را از نگلهٔ ۱۲ خارج کنید.", tempData[PageToast.MessageKey]);
        Assert.Equal(PageToast.Error, tempData[PageToast.LevelKey]);
    }

    [Fact]
    public void Validation_OnPage_IsHandledToo()
    {
        foreach (var exception in new Exception[]
                 {
                     AppValidationException.ForField("currency", "واحد پول کارگاه فقط تومان یا ریال است."),
                     new DomainValidationException("اندازه را به متر بنویسید", "length")
                 })
        {
            var (context, tempData) = Context<SettingsController>(exception, referer: "http://localhost/Settings/Tenant");
            Filter().OnException(context);

            Assert.True(context.ExceptionHandled);
            Assert.Equal("/Settings/Tenant", Assert.IsType<RedirectResult>(context.Result).Url);
            Assert.Equal(exception.Message, tempData[PageToast.MessageKey]);
            Assert.Equal(PageToast.Error, tempData[PageToast.LevelKey]);
        }
    }

    [Fact]
    public void ForeignOrMissingReferer_GoesHome()
    {
        foreach (var referer in new[] { "https://evil.example/phish", "", "not a url", "http://localhost//evil.example" })
        {
            var (context, _) = Context<RugsController>(new DomainConflictException("x"), referer);
            Filter().OnException(context);
            Assert.Equal("/", Assert.IsType<RedirectResult>(context.Result).Url);
        }
    }

    [Fact]
    public void ApiControllers_AndOtherExceptions_AreLeftAlone()
    {
        // API: ApiExceptionFilter همان را ۴۰۹ JSON می‌کند؛ این فیلتر نباید ریدایرکت بسازد
        var (api, _) = Context<RugsApiController>(new DomainConflictException("x"), "http://localhost/Rugs", apiController: true);
        Filter().OnException(api);
        Assert.False(api.ExceptionHandled);
        Assert.Null(api.Result);

        // خطای غیر دامنه (۴۰۴، خرابی واقعی) دست‌نخورده می‌ماند
        foreach (var exception in new Exception[] { new KeyNotFoundException(), new InvalidOperationException("boom") })
        {
            var (page, _) = Context<RugsController>(exception, "http://localhost/Rugs");
            Filter().OnException(page);
            Assert.False(page.ExceptionHandled);
            Assert.Null(page.Result);
        }
    }

    [Fact]
    public void ApiExceptionFilter_MapsConflictWithFieldAndExtensions_AndValidationWithErrors()
    {
        var conflict = new ExceptionContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), [])
        {
            Exception = new ConflictWithNextFree()
        };
        new ApiExceptionFilter().OnException(conflict);
        var body = Assert.IsType<Dictionary<string, object?>>(Assert.IsType<ObjectResult>(conflict.Result).Value);
        Assert.Equal(409, ((ObjectResult)conflict.Result!).StatusCode);
        Assert.Equal("شمارهٔ ۴۰۰۵ آزاد است", body["message"]);
        Assert.Equal("rug.serial", body["field"]);
        Assert.Equal(4005, body["nextFree"]);

        var validation = new ExceptionContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), [])
        {
            Exception = AppValidationException.ForField("textScale", "«اندازهٔ نوشته» درست نیست.")
        };
        new ApiExceptionFilter().OnException(validation);
        var result = Assert.IsType<ObjectResult>(validation.Result);
        Assert.Equal(400, result.StatusCode);
        var errors = result.Value!.GetType().GetProperty("errors")!.GetValue(result.Value);
        var dictionary = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(errors);
        Assert.Equal(new[] { "«اندازهٔ نوشته» درست نیست." }, dictionary["textScale"]);
    }

    // ── ساختن زمینهٔ ساختگی ────────────────────────────────────────────────

    private static DomainExceptionPageFilter Filter()
        => new(new FakeTempDataFactory(), NullLogger<DomainExceptionPageFilter>.Instance);

    private static (ExceptionContext Context, ITempDataDictionary TempData) Context<TController>(
        Exception exception, string referer, bool apiController = false)
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "http";
        http.Request.Host = new HostString("localhost");
        if (referer.Length > 0)
            http.Request.Headers.Referer = referer;

        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(TController).GetTypeInfo(),
            ControllerName = typeof(TController).Name,
            ActionName = "Any",
            EndpointMetadata = apiController ? [new ApiControllerAttribute()] : []
        };

        var context = new ExceptionContext(new ActionContext(http, new RouteData(), descriptor), [])
        {
            Exception = exception
        };

        var tempData = FakeTempDataFactory.For(http);
        return (context, tempData);
    }

    private sealed class ConflictWithNextFree() : DomainConflictException("شمارهٔ ۴۰۰۵ آزاد است", "rug.serial")
    {
        public override IReadOnlyDictionary<string, object?>? Extensions
            => new Dictionary<string, object?> { ["nextFree"] = 4005, ["message"] = "نباید جای پیام را بگیرد" };
    }

    private sealed class FakeTempDataFactory : ITempDataDictionaryFactory
    {
        private static readonly object Key = new();

        public ITempDataDictionary GetTempData(HttpContext context) => For(context);

        public static ITempDataDictionary For(HttpContext context)
        {
            if (context.Items.TryGetValue(Key, out var existing) && existing is ITempDataDictionary tempData)
                return tempData;

            tempData = new TempDataDictionary(context, new NullTempDataProvider());
            context.Items[Key] = tempData;
            return tempData;
        }
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
