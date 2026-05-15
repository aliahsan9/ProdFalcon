using AutoMapper;
using ExamDynamicsAPI.Core.DTOs.ExamDTOs;
using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace ExamDynamicsAPI.Applications.Services
{
    public class ExamService : IExamService
    {
        private const string CacheKeyAll = "exams:all";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        private readonly IExamRepository _repository;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;

        public ExamService(IExamRepository repository, IMapper mapper, IMemoryCache cache)
        {
            _repository = repository;
            _mapper = mapper;
            _cache = cache;
        }

        public async Task<IEnumerable<ExamDto>> GetAllAsync()
        {
            if (_cache.TryGetValue(CacheKeyAll, out IEnumerable<ExamDto>? cached) && cached != null)
                return cached;

            var exams = await _repository.GetAllAsync();
            var dto = _mapper.Map<IEnumerable<ExamDto>>(exams);
            _cache.Set(CacheKeyAll, dto, CacheDuration);
            return dto;
        }

        public async Task<ExamDto?> GetByIdAsync(int id)
        {
            var exam = await _repository.GetByIdAsync(id);
            return exam == null ? null : _mapper.Map<ExamDto>(exam);
        }

        public async Task<ExamDto> CreateAsync(CreateExamDto createDto)
        {
            var exam = _mapper.Map<Exam>(createDto);
            await _repository.AddAsync(exam);
            _cache.Remove(CacheKeyAll);
            return _mapper.Map<ExamDto>(exam);
        }
 
        public async Task<bool> UpdateAsync(int id, UpdateExamDto updateDto)
        {
            var exam = await _repository.GetByIdAsync(id);
            if (exam == null) return false;

            _mapper.Map(updateDto, exam);
            await _repository.UpdateAsync(exam);
            _cache.Remove(CacheKeyAll);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var exam = await _repository.GetByIdAsync(id);
            if (exam == null) return false;

            await _repository.DeleteAsync(id);
            _cache.Remove(CacheKeyAll);
            return true;
        }
    }
}
