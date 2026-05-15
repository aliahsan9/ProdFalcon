using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamDynamicsAPI.Infrastructure.Repositories
{
    public class ExamCategoryRepository : IExamCategoryRepository
    {
        private readonly ExamDynamicsDbContext _context;

        public ExamCategoryRepository(ExamDynamicsDbContext context)
        {
            _context = context;
        }

        // GET ALL
        public async Task<IEnumerable<ExamCategory>> GetAllAsync()
        {
            return await _context.ExamCategories
                                 .AsNoTracking()
                                 .ToListAsync();
        }
 
        // GET BY ID
        public async Task<ExamCategory?> GetByIdAsync(int id)
        {
            return await _context.ExamCategories.FindAsync(id);
        }

        // ADD
        public async Task AddAsync(ExamCategory entity)
        {
            await _context.ExamCategories.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        // UPDATE
        public async Task UpdateAsync(ExamCategory entity)
        {
            _context.ExamCategories.Update(entity);
            await _context.SaveChangesAsync();
        }

        // DELETE
        public async Task DeleteAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                _context.ExamCategories.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }
    }
}
