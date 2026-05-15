using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;

namespace ExamDynamicsAPI.Applications.Services
{
    public class QuestionService : IQuestionService
    {
        private readonly IQuestionRepository _questionRepository;

        public QuestionService(IQuestionRepository questionRepository)
        {
            _questionRepository = questionRepository;
        }

        public Task<IEnumerable<Question>> GetAllAsync()
        {
            return _questionRepository.GetAllAsync();
        }

        public Task<Question?> GetByIdAsync(int id)
        {
            return _questionRepository.GetByIdAsync(id);
        }

        public async Task<Question> CreateAsync(Question question)
        {
            return await _questionRepository.AddAsync(question);
        }

        public async Task<Question?> UpdateAsync(Question question)
        {
            return await _questionRepository.UpdateAsync(question);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _questionRepository.DeleteAsync(id);
        }
    }
}
