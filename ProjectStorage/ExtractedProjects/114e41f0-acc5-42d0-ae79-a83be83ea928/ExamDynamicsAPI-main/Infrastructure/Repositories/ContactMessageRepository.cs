using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Models;
using ExamDynamicsAPI.Infrastructure.Data;

namespace ExamDynamicsAPI.Infrastructure.Repositories
{
    public class ContactMessageRepository : IContactMessageRepository
    {
        private readonly ExamDynamicsDbContext _context;

        public ContactMessageRepository(ExamDynamicsDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(ContactMessage contactMessage)
        {
            await _context.ContactMessages.AddAsync(contactMessage);
            await _context.SaveChangesAsync();
        }
    }
}
