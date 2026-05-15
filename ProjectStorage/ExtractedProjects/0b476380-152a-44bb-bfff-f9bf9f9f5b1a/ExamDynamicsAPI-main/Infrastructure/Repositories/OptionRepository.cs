using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamDynamicsAPI.Infrastructure.Repositories
{
    public class OptionRepository : IOptionRepository
    {
        private readonly ExamDynamicsDbContext _context;

        public OptionRepository(ExamDynamicsDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<Option>> GetAllAsync()
        {
            return await _context.Options.ToListAsync();
        }

        public async Task<Option?> GetByIdAsync(int id)
        {
            return await _context.Options.FindAsync(id);
        }

        public async Task AddAsync(Option option)
        {
            await _context.Options.AddAsync(option);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Option option)
        {
            _context.Options.Update(option);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Option option)
        {
            _context.Options.Remove(option);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Option>> GetByQuestionIdAsync(int questionId)
        {
            return await _context.Options
                .Where(o => o.QuestionId == questionId)
                .ToListAsync();
        }
    }
}
