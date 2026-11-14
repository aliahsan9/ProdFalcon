using ProdFalcon.Application.Scanning.Remediation;

namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IRemediationService
{
    Task<RemediationPlanDto> GetAsync(int scanResultId, CancellationToken cancellationToken = default);

    Task<RemediationPlanDto> BuildAsync(int scanResultId, CancellationToken cancellationToken = default);

    Task<RemediationProposalDto> DecideAsync(int proposalId, string decision, string? note, CancellationToken cancellationToken = default);
}
