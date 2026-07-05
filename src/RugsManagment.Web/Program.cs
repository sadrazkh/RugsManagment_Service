using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using RugsManagment.Application;
using RugsManagment.Infrastructure;
using RugsManagment.Infrastructure.Persistence.Seed;

// ═══════════════════════════════════════════════════════════════════
// میزبان یکپارچه: MVC/Razor + کنترلرهای API + جزیره‌های Vue
// یک اپلیکیشن، یک پورت. بدون سرور جداگانه‌ی فرانت در تولید.
//   - AddApplication/AddInfrastructure: همان سرویس‌ها و دیتابیس پروژه‌ی قبلی
//   - احراز هویت مبتنی بر Cookie (طبیعی برای اپ MVC؛ مشکل ورود قبلی را حل می‌کند)
//   - جزیره‌های Vue از wwwroot/dist سرو می‌شوند (خروجی build خط لوله‌ی Vite)
// ═══════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// کلیدهای Data Protection را ماندگار کن تا کوکی احراز هویت و توکن antiforgery
// با ری‌استارت یا اجرای چند نمونه باطل نشوند (لازم برای محیط تولید).
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")))
    .SetApplicationName("RugsManagment");

builder.Services.AddControllersWithViews();

// خروجی HTML فارسی به‌جای entityهای عددی، به‌صورت UTF-8 تمیز رندر شود.
// (کاراکترهای خطرناک HTML همچنان escape می‌شوند؛ فقط دامنهٔ یونیکد گسترده می‌شود.)
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));

// محافظت CSRF برای APIها: جزیره‌های Vue توکن را از meta خوانده و در این هدر می‌فرستند.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddScoped<RugsManagment.Web.Controllers.Api.ApiExceptionFilter>();

// احراز هویت با کوکی — همان claimها (NameIdentifier / Role / tenant_id) که بک‌اند انتظار دارد
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "rugs.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

// دسترسی به کاربر جاری از داخل سرویس‌ها (برای جداسازی مستأجر)
builder.Services.AddHttpContextAccessor();

// پل Razor ↔ خروجی build فرانت (Vue islands)
builder.Services.AddSingleton<RugsManagment.Web.Frontend.ViteAssets>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// کنترلرهای API (JSON برای جزیره‌های Vue) — الگوی attribute-route: [Route("api/...")]
app.MapControllers();

// صفحات MVC/Razor
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// migration + دادهٔ اولیه (ادمین سیستم، کارگاه دمو، قالب‌ها) هنگام استارت
await DatabaseSeeder.SeedAsync(app.Services);

app.Run();
