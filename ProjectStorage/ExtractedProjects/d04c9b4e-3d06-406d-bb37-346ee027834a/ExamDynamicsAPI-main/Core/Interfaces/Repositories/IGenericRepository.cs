namespace ExamDynamicsAPI.Core.Interfaces.Repositories
{
    public interface IGenericRepository<T> where T : class
    {
        // Get all entities
        Task<IEnumerable<T>> GetAllAsync();

        // Get entity by ID
        Task<T?> GetByIdAsync(int id);

        // Add new entity
        Task AddAsync(T entity);

        // Update existing entity
        Task UpdateAsync(T entity);

        // Delete entity by ID
        Task DeleteAsync(int id);
    }
}
