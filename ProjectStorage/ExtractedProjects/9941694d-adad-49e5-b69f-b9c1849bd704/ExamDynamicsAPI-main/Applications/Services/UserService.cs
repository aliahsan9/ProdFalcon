using AutoMapper;
using ExamDynamicsAPI.Core.DTOs.UserDTOs;
using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;

namespace ExamDynamicsAPI.Applications.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public UserService(IUserRepository userRepository, IMapper mapper)
        {
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<UserReadDTO>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllUsersAsync();
            return _mapper.Map<IEnumerable<UserReadDTO>>(users);
        }

        public async Task<UserReadDTO?> GetUserByIdAsync(int id)
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            return user == null ? null : _mapper.Map<UserReadDTO>(user);
        }

        public async Task<UserReadDTO> CreateUserAsync(CreateUserDto dto)
        {
            var user = _mapper.Map<ApplicationUser>(dto);
            await _userRepository.AddUserAsync(user);
            await _userRepository.SaveChangesAsync();
            return _mapper.Map<UserReadDTO>(user);
        }

        public async Task<UserReadDTO?> UpdateUserAsync(int id, UpdateUserDto dto)
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            if (user == null) return null;

            _mapper.Map(dto, user);
            _userRepository.UpdateUser(user);
            await _userRepository.SaveChangesAsync();
            return _mapper.Map<UserReadDTO>(user);
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            if (user == null) return false;

            _userRepository.DeleteUser(user);
            await _userRepository.SaveChangesAsync();
            return true;
        }
    }
}