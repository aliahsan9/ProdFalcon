using Microsoft.AspNetCore.Identity;

namespace ExamDynamicsAPI.Core.Interfaces.Services;

public interface IExternalAuthCompletionService
{
    Task<(bool Success, string Token, string? ErrorMessage)> CompleteExternalLoginAsync(
        ExternalLoginInfo info,
        CancellationToken cancellationToken = default);
}
