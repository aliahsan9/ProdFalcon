using ExamDynamicsAPI.Core.DTOs.AnswerDTOs;
using ExamDynamicsAPI.Core.Models;

namespace ExamDynamicsAPI.Applications.Mappings
{
    public class AnswerProfile : AutoMapper.Profile
    {
        public AnswerProfile()
        {
            // For creating a new Answer
            CreateMap<AnswerCreateDto, Answer>();

            // For returning Answer to client
            CreateMap<Answer, AnswerDto>().ReverseMap();

            // If you also have Update DTO
            CreateMap<AnswerUpdateDto, Answer>();
        }
    }
}
