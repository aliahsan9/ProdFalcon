using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamDynamicsAPI.Infrastructure.Repositories
{
    public class AnswerRepository : IAnswerRepository
    {
        private readonly ExamDynamicsDbContext _context;

        public AnswerRepository(ExamDynamicsDbContext context)
        {
            _context = context;
        }

        // Get all answers
        public async Task<IEnumerable<Answer>> GetAllAsync()
        {
            return await _context.Answers
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        // Get answer by ID
        public async Task<Answer?> GetByIdAsync(int id)
        {
            return await _context.Answers
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(a => a.Id == id);
        }

        // Add a new answer
        public async Task AddAsync(Answer entity)
        {
            await _context.Answers.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        // Update an existing answer
        public async Task UpdateAsync(Answer entity)
        {
            _context.Answers.Update(entity);
            await _context.SaveChangesAsync();
        }

        // Delete answer by ID
        public async Task DeleteAsync(int id)
        {
            var entity = await _context.Answers.FindAsync(id);
            if (entity != null)
            {
                _context.Answers.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
