using RugsManagment.Application.DTOs.Auth;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>ورود و تازه‌سازی توکن</summary>
public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(Guid userId, CancellationToken cancellationToken = default);
}
