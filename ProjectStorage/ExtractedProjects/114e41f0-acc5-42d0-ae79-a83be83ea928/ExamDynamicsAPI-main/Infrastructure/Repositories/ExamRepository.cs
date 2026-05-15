using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using ExamDynamicsAPI.Infrastructure.Data;

namespace ExamDynamicsAPI.Infrastructure.Repositories
{
    public class ExamRepository : IExamRepository
    {
        private readonly ExamDynamicsDbContext _context;

        public ExamRepository(ExamDynamicsDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Exam>> GetAllAsync()
        {
            return await _context.Exams.AsNoTracking().ToListAsync();
        }

        public async Task<Exam?> GetByIdAsync(int id)
        {
            return await _context.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.ExamId == id);
        }

        public async Task AddAsync(Exam exam)
        {
            await _context.Exams.AddAsync(exam);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Exam exam)
        {
            _context.Exams.Update(exam);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam != null)
            {
                _context.Exams.Remove(exam);
                await _context.SaveChangesAsync();
            }
        }
    }
}