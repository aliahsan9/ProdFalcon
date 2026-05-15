using ExamDynamicsAPI.Core.DTOs.AnswerDTOs;
using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using AutoMapper;

namespace ExamDynamicsAPI.Applications.Services
{
    public class AnswerService : IAnswerService
    {
        private readonly IAnswerRepository _answerRepository;
        private readonly IMapper _mapper;

        public AnswerService(IAnswerRepository answerRepository, IMapper mapper)
        {
            _answerRepository = answerRepository;
            _mapper = mapper;
        }

        // CREATE
        public async Task<AnswerReadDto> CreateAsync(AnswerCreateDto dto, int userId)
        {
            // Map DTO to entity
            var answer = _mapper.Map<Answer>(dto);

            // Set the logged-in user's Id
            answer.UserId = userId;

            await _answerRepository.AddAsync(answer);

            return _mapper.Map<AnswerReadDto>(answer);
        }

        // GET BY ID
        public async Task<AnswerReadDto?> GetByIdAsync(int id)
        {
            var answer = await _answerRepository.GetByIdAsync(id);
            return answer == null ? null : _mapper.Map<AnswerReadDto>(answer);
        }

        // GET ALL
        public async Task<IEnumerable<AnswerReadDto>> GetAllAsync()
        {
            var answers = await _answerRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<AnswerReadDto>>(answers);
        }

        // UPDATE
        public async Task<bool> UpdateAsync(int id, AnswerUpdateDto dto)
        {
            var existing = await _answerRepository.GetByIdAsync(id);
            if (existing == null) return false;

            _mapper.Map(dto, existing);
            existing.UpdatedAt = DateTime.UtcNow;
            await _answerRepository.UpdateAsync(existing);
            return true;
        }

        // DELETE
        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _answerRepository.GetByIdAsync(id);
            if (existing == null) return false;

            await _answerRepository.DeleteAsync(id);
            return true;
        }
    }
}
