using System.Text.Json;
using System.Text.RegularExpressions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Platform;

/// <summary>
/// ترجیحات خودِ کاربر: اندازهٔ نوشته، جهت Enter، حالت رابط (فقط تنظیم‌کننده) و راهنماهای بسته‌شده.
///
/// کاربر ردیابی‌شده خوانده و فقط ستون‌های عوض‌شده نوشته می‌شوند (بدون <c>Update</c>)، تا اگر مدیر هم‌زمان
/// نقش یا رمز همین کاربر را عوض کرد، تغییر اندازهٔ نوشته آن را بی‌صدا برنگرداند (User توکن هم‌زمانی ندارد).
/// تغییر اندازهٔ نوشته رویداد تاریخچه نمی‌سازد؛ فقط سلیقهٔ خودِ کاربر است.
/// </summary>
public sealed partial class UserPreferencesService(
    IUserRepository users,
    IUnitOfWork unitOfWork) : IUserPreferencesService
{
    /// <summary>بیشترین طول کلید یک راهنمای شروع.</summary>
    public const int CoachKeyMaxLength = 60;

    /// <summary>
    /// بیشترین تعداد راهنمای بسته‌شده که نگه داشته می‌شود؛ قدیمی‌ترها کنار می‌روند تا ستون بی‌حد رشد نکند.
    /// </summary>
    public const int MaxCoachDismissed = 100;

    public async Task<UserPreferencesDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        => ToDto(await LoadAsync(userId, cancellationToken));

    public async Task<UserPreferencesDto> UpdateAsync(
        Guid userId, UpdateMyPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.TextScale is { } scale && !Enum.IsDefined(scale))
            errors["textScale"] = [Messages.InvalidValue.Format(("label", "اندازهٔ نوشته"))];
        if (request.GridEnter is { } enter && !Enum.IsDefined(enter))
            errors["gridEnter"] = [Messages.InvalidValue.Format(("label", "جهت حرکت در جدول"))];
        if (request.UiMode is { } mode && !Enum.IsDefined(mode))
            errors["uiMode"] = [Messages.InvalidValue.Format(("label", "حالت نمایش"))];

        string? coachKey = null;
        if (request.DismissCoach is not null)
        {
            coachKey = request.DismissCoach.Trim();
            if (coachKey.Length is 0 or > CoachKeyMaxLength || !CoachKeyPattern().IsMatch(coachKey))
                errors["dismissCoach"] = [Messages.InvalidValue.Format(("label", "راهنما"))];
        }

        if (errors.Count > 0)
            throw new AppValidationException(Messages.ValidationFailed.Text, errors);

        var user = await LoadAsync(userId, cancellationToken);
        var changed = false;

        if (request.TextScale is { } newScale && newScale != user.TextScale)
        {
            user.TextScale = newScale;
            changed = true;
        }

        if (request.GridEnter is { } newEnter && newEnter != user.GridEnter)
        {
            user.GridEnter = newEnter;
            changed = true;
        }

        // حالت بازرگان و انباردار را تنظیم‌کننده از صفحهٔ کاربران می‌گذارد (چک‌لیست آماده‌سازی به آن تکیه دارد)؛
        // پس از این مسیر فقط حالتِ خودِ تنظیم‌کننده عوض می‌شود و بقیه بی‌صدا نادیده گرفته می‌شوند.
        if (request.UiMode is { } newMode && user.Role == UserRole.TenantAdmin && newMode != user.UiMode)
        {
            user.UiMode = newMode;
            changed = true;
        }

        if (coachKey is not null)
        {
            var dismissed = ParseCoach(user.CoachDismissedJson).ToList();
            if (!dismissed.Contains(coachKey, StringComparer.Ordinal))
            {
                dismissed.Add(coachKey);
                if (dismissed.Count > MaxCoachDismissed)
                    dismissed.RemoveRange(0, dismissed.Count - MaxCoachDismissed);
                user.CoachDismissedJson = JsonSerializer.Serialize(dismissed);
                changed = true;
            }
        }

        if (changed)
        {
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToDto(user);
    }

    public async Task<MeDto> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await LoadAsync(userId, cancellationToken);
        return new MeDto(
            user.Id,
            user.TenantId,
            user.Tenant?.Name,
            user.FullName,
            user.Email,
            user.Role,
            RoleLabels.For(user.Role),
            ToDto(user));
    }

    /// <summary>
    /// فهرست راهنماهای بسته‌شده از jsonb؛ متن خراب یا هر چیزی جز آرایهٔ رشته فهرست خالی است، نه خطا —
    /// پوستهٔ هر صفحه این را می‌خواند و نباید به‌خاطرش بشکند.
    /// </summary>
    public static IReadOnlyList<string> ParseCoach(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];

            return document.RootElement.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<User> LoadAsync(Guid userId, CancellationToken ct)
        => await users.GetByIdAsync(userId, ct) ?? throw new KeyNotFoundException(Messages.NotFound.Text);

    private static UserPreferencesDto ToDto(User user)
        => new(user.UiMode, user.TextScale, user.GridEnter, ParseCoach(user.CoachDismissedJson));

    /// <summary>کلید راهنما: حرف کوچک لاتین، رقم، «-»، «_» و «.» (مثل «home.tiles»).</summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$")]
    private static partial Regex CoachKeyPattern();
}
