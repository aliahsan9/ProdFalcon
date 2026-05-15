using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace ExamDynamicsAPI.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ExamDynamicsDbContext _context;

        public UserRepository(ExamDynamicsDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<ApplicationUser?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task AddUserAsync(ApplicationUser user)
        {
            await _context.Users.AddAsync(user);
        }

        public void UpdateUser(ApplicationUser user)
        {
            _context.Users.Update(user);
        }

        public void DeleteUser(ApplicationUser user)
        {
            _context.Users.Remove(user);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
