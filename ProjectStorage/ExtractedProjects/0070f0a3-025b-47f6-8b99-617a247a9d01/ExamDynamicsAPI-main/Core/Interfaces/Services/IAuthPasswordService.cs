namespace ExamDynamicsAPI.Core.Interfaces.Services;

public interface IAuthPasswordService
{
    /// <summary>
    /// Sends a password reset email if the account exists. Always succeeds from a caller perspective (no email enumeration).
    /// </summary>
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets password using Identity token.
    /// </summary>
    Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default);
}
