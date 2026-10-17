using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Application.DTOs.Users;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>
/// مدیریت کاربران یک کارگاه — همیشه به tenantId محدود می‌شود تا نشت داده بین کارگاه‌ها رخ ندهد.
/// نقش SystemAdmin از این مسیر قابل ساخت نیست.
/// </summary>
public interface IUserManagementService
{
    Task<IReadOnlyList<UserDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<UserDto> CreateAsync(Guid tenantId, CreateUserRequest request, CancellationToken ct = default);
    Task<UserUpdateResult> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken ct = default);
}

/// <summary>
/// نقش‌های کارگاه: بازرگان، انباردار و تنظیم‌کننده (<see cref="RoleLabels.TenantRoles"/>). کاربر تازه و کاربری
/// که نقشش عوض می‌شود پیش‌فرض‌های نقش را می‌گیرد (بازرگان «ساده + نوشتهٔ بزرگ»، انباردار «ساده»، تنظیم‌کننده
/// «کامل»)؛ ترجیحی که خودِ کاربر بعداً عوض کند با ویرایش نام یا رمز دست نمی‌خورد.
///
/// دو ترمز: کارگاه نباید بی تنظیم‌کنندهٔ فعال بماند، و هیچ‌کس نقش خودش را پایین نمی‌آورد یا حساب خودش را
/// غیرفعال نمی‌کند (۴۰۹ با پیام فارسی). رمز کاربر تازه و رمز بازنشانی‌شده قاعدهٔ <see cref="PasswordPolicy"/>
/// را دارند (۴۰۰ زیر خانهٔ رمز). <paramref name="currentUser"/> اختیاری است تا اجرای بی‌کاربر
/// (seed، آزمون) هم سرویس را بسازد؛ آن‌جا ترمز «خودِ من» معنایی ندارد.
/// </summary>
public sealed class UserManagementService(
    IUserRepository users,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    ICurrentUser? currentUser = null) : IUserManagementService
{
    public async Task<IReadOnlyList<UserDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await users.ListAsync(u => u.TenantId == tenantId, ct);
        // IsActive صریح پر می‌شود: فرم ویرایش از همین فهرست پر می‌شود و نباید حساب غیرفعال را فعال نشان دهد
        return list.OrderBy(u => u.FullName).Select(u => u.ToDto() with { IsActive = u.IsActive }).ToList();
    }

    public async Task<UserDto> CreateAsync(Guid tenantId, CreateUserRequest request, CancellationToken ct = default)
    {
        EnsureTenantRole(request.Role);
        EnsurePasswordAcceptable(request.Password);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.GetByEmailAsync(email, ct) is not null)
            throw new InvalidOperationException("این ایمیل قبلاً ثبت شده است.");

        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            FullName = request.FullName.Trim(),
            Role = request.Role,
            PasswordHash = AuthService.HashPassword(request.Password),
            IsActive = true
        };
        user.ApplyRoleDefaultPreferences();

        await users.AddAsync(user, ct);

        audit.Record(AuditAction.UserInvited, nameof(User), user.Id,
            $"کاربر «{user.FullName}» با نقش {RoleLabels.For(user.Role)} اضافه شد.", user.Email,
            new { role = user.Role, uiMode = user.UiMode, textScale = user.TextScale });

        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }

    /// <remarks>
    /// ویرایشی که ممکن است یک تنظیم‌کنندهٔ فعال را کم کند (نقش تازه غیر تنظیم‌کننده، یا غیرفعال) در یک تراکنش و زیر
    /// قفل تنظیم‌کننده‌های کارگاه (<see cref="IUserRepository.LockTenantAdminsAsync"/>) اجرا می‌شود: خواندن کاربر، شمردن
    /// تنظیم‌کننده‌های دیگر و نوشتن، هر سه پشت همان قفل. بدون آن، دو تنظیم‌کننده که هم‌زمان نقش هم را پایین می‌آوردند
    /// هر کدام دیگری را هنوز فعال می‌دیدند و کارگاه بی تنظیم‌کننده می‌ماند. ویرایشی که تنظیم‌کننده‌ای را کم نمی‌کند
    /// (نقش تنظیم‌کننده و فعال) قفل نمی‌خواهد.
    /// </remarks>
    public async Task<UserUpdateResult> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        EnsureTenantRole(request.Role);

        var mayRemoveAdmin = request.Role != UserRole.TenantAdmin || !request.IsActive;
        if (!mayRemoveAdmin)
            return await UpdateCoreAsync(tenantId, userId, request, ct);

        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await users.LockTenantAdminsAsync(tenantId, token);
            return await UpdateCoreAsync(tenantId, userId, request, token);
        }, ct);
    }

    private async Task<UserUpdateResult> UpdateCoreAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);

        // جداسازی مستأجر: کاربر کارگاه دیگر درست مثل کاربرِ ناموجود «یافت نشد» است (۴۰۴، نه ۴۰۳)
        // تا حتی وجودِ یک شناسه در کارگاه دیگر هم لو نرود.
        if (user is null || user.TenantId != tenantId)
            throw new KeyNotFoundException("کاربر یافت نشد.");

        var wasActive = user.IsActive;
        var passwordReset = !string.IsNullOrWhiteSpace(request.NewPassword);
        if (passwordReset)
            EnsurePasswordAcceptable(request.NewPassword);
        var roleChanged = user.Role != request.Role;
        var activeChanged = user.IsActive != request.IsActive;

        await EnsureAdminRemainsAsync(tenantId, user, request, ct);

        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (passwordReset)
            user.PasswordHash = AuthService.HashPassword(request.NewPassword!);

        // نقش تازه پیش‌فرض‌های نقش تازه را می‌آورد (بازرگان با نوشتهٔ بزرگ شروع می‌کند)
        if (roleChanged)
            user.ApplyRoleDefaultPreferences();

        // نقش و وضعیت داخل کوکی است؛ با مهر تازه همهٔ نشست‌های باز این کاربر در درخواست بعدی
        // (حداکثر چند دقیقه بعد، به‌خاطر کش اعتبارسنجی) رد می‌شوند و باید دوباره وارد شود.
        var rotated = roleChanged || activeChanged || passwordReset;
        if (rotated)
            user.RotateSecurityStamp();

        user.UpdatedAt = DateTimeOffset.UtcNow;

        // users.Update(user) عمداً صدا زده نمی‌شود: کاربر tracked است و EF فقط ستون‌هایی را که واقعاً عوض شده‌اند
        // می‌نویسد. Update همهٔ ستون‌ها را Modified می‌کرد و اگر خودِ کاربر هم‌زمان رمزش را عوض کرده بود
        // (User توکن هم‌زمانی ندارد)، ویرایشِ صرفِ نام به دست مدیر رمز و مهرِ قدیمی را بی‌صدا برمی‌گرداند.

        // غیرفعال شدن حساب رویداد امنیتی مهمی است و جدا ثبت می‌شود
        if (wasActive && !user.IsActive)
        {
            audit.Record(AuditAction.UserDisabled, nameof(User), user.Id,
                $"حساب «{user.FullName}» غیرفعال شد.", user.Email);
        }
        else
        {
            var notes = new List<string>(2);
            if (roleChanged) notes.Add($"نقش: {RoleLabels.For(user.Role)}");
            if (passwordReset) notes.Add("رمز عبور بازنشانی شد");
            var note = notes.Count == 0 ? "" : $" ({string.Join("، ", notes)})";

            audit.Record(AuditAction.Updated, nameof(User), user.Id,
                $"حساب «{user.FullName}» ویرایش شد{note}.", user.Email,
                roleChanged ? new { role = user.Role } : null);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new UserUpdateResult(
            user.ToDto() with { IsActive = user.IsActive },
            rotated ? user.SecurityStamp : null);
    }

    /// <summary>
    /// دو ترمز پیش از نوشتن:
    /// <list type="bullet">
    /// <item>کاربر جاری نقش خودش را از تنظیم‌کننده پایین نمی‌آورد و حساب خودش را غیرفعال نمی‌کند — وگرنه
    /// با یک کلیک اشتباه از تنظیمات کارگاه بیرون می‌ماند.</item>
    /// <item>آخرین تنظیم‌کنندهٔ فعال کارگاه نه نقشش عوض می‌شود و نه غیرفعال، چون بدون او هیچ‌کس نمی‌تواند
    /// کاربر یا تنظیمات را درست کند.</item>
    /// </list>
    /// </summary>
    private async Task EnsureAdminRemainsAsync(Guid tenantId, User user, UpdateUserRequest request, CancellationToken ct)
    {
        var losesAdmin = user.Role == UserRole.TenantAdmin && request.Role != UserRole.TenantAdmin;
        var deactivated = user.IsActive && !request.IsActive;

        if (currentUser?.UserId == user.Id && (losesAdmin || deactivated))
            throw new DomainConflictException(Messages.SelfDemote.Text, losesAdmin ? "role" : "isActive");

        if (!(user.Role == UserRole.TenantAdmin && user.IsActive) || !(losesAdmin || deactivated))
            return;

        var otherAdmins = await users.ListAsync(
            u => u.TenantId == tenantId && u.Id != user.Id && u.Role == UserRole.TenantAdmin && u.IsActive, ct);
        if (otherAdmins.Count == 0)
            throw new DomainConflictException(Messages.LastAdmin.Text, losesAdmin ? "role" : "isActive");
    }

    private static void EnsureTenantRole(UserRole role)
    {
        if (!RoleLabels.IsTenantRole(role))
            throw AppValidationException.ForField("role", Messages.InvalidRole.Text);
    }

    /// <summary>
    /// رمزی که تنظیم‌کننده برای کاربر تازه یا بازنشانی می‌گذارد همان قاعدهٔ تغییر رمز به دست خودِ کاربر را دارد
    /// (<see cref="PasswordPolicy"/>)؛ پیش از هر نوشتن، تا رمز کوتاه نه ذخیره شود و نه مهر نشست را بچرخاند.
    /// </summary>
    private static void EnsurePasswordAcceptable(string? password)
    {
        if (!PasswordPolicy.IsAcceptable(password))
            throw AppValidationException.ForField("password", PasswordPolicy.TooShortMessage);
    }
}
