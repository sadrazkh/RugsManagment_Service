using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RugsManagment.Application.Abstractions.Services;

namespace RugsManagment.IntegrationTests.Support;

/// <summary>
/// کاربر جاریِ ساختگی به‌جای HttpCurrentUser (که از کوکی می‌خواند).
/// Scoped ثبت می‌شود و هر scope نقش یک درخواست HTTP را دارد؛ پس هر «درخواست» تست
/// می‌تواند از طرف کاربرِ یک کارگاه دیگر باشد.
/// </summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public string? DisplayName { get; set; }
    public Guid? TenantId { get; set; }
}

/// <summary>
/// محیط میزبانِ حداقلی. LocalImageStorage (ثبت‌شده در AddInfrastructure) آن را می‌خواهد؛
/// بدون آن ValidateOnBuild گراف DI را رد می‌کرد، هرچند تست‌ها هیچ‌وقت تصویر ذخیره نمی‌کنند.
/// </summary>
internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "RugsManagment.IntegrationTests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
