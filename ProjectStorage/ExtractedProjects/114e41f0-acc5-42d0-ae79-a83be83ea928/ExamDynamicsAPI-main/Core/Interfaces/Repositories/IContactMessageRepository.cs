using ExamDynamicsAPI.Core.Models;

namespace ExamDynamicsAPI.Core.Interfaces.Repositories
{
    public interface IContactMessageRepository
    {
        Task AddAsync(ContactMessage contactMessage);
    }
}
 