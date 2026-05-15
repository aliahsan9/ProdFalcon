
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Infrastructure.Data;

namespace ExamDynamicsAPI.Applications.Services
{
public class RagService : IRagService
{
    private readonly ExamDynamicsDbContext _context;

    public RagService(ExamDynamicsDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetRelevantContextAsync(string query)
    {
        // 🔥 Simple keyword search (upgrade later to embeddings)
        var results = _context.Questions
            .Where(q => q.Text.Contains(query))
            .Take(5)
            .Select(q => q.Text)
            .ToList();

        return string.Join("\n", results);
    }
}
}