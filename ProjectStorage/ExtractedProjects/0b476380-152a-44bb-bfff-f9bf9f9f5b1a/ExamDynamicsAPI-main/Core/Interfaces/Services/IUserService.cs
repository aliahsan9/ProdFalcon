using ExamDynamicsAPI.Core.DTOs.UserDTOs;

namespace ExamDynamicsAPI.Core.Interfaces.Services
{
  public interface IUserService
{
    Task<IEnumerable<UserReadDTO>> GetAllUsersAsync();
    Task<UserReadDTO?> GetUserByIdAsync(int id);
    Task<UserReadDTO> CreateUserAsync(CreateUserDto dto);
    Task<UserReadDTO?> UpdateUserAsync(int id, UpdateUserDto dto);
    Task<bool> DeleteUserAsync(int id);
}

}
 