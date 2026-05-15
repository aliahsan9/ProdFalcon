using ExamDynamicsAPI.Core.DTOs.ExamDTOs;
using ExamDynamicsAPI.Core.DTOs.OptionDTOs;
using ExamDynamicsAPI.Core.DTOs.QuestionDTOs;
using ExamDynamicsAPI.Core.DTOs.UserDTOs;
using ExamDynamicsAPI.Core.Models;
namespace ExamDynamicsAPI.Applications.Mappings

{
    public class MappingProfile : AutoMapper.Profile
    {
        public MappingProfile()
        {
            // User
            CreateMap<ApplicationUser, UserDto>().ReverseMap();
            CreateMap<ApplicationUser, UserReadDTO>();
            CreateMap<CreateUserDto, ApplicationUser>();
            CreateMap<UpdateUserDto, ApplicationUser>();

            // Exam
            CreateMap<Exam, ExamDto>().ReverseMap();
            CreateMap<CreateExamDto, Exam>();
            CreateMap<UpdateExamDto, Exam>();

            // Question
            CreateMap<Question, QuestionDto>().ReverseMap();
            CreateMap<CreateQuestionDto, Question>();
            CreateMap<UpdateQuestionDto, Question>();

            // Option
            CreateMap<Option, OptionDto>().ReverseMap();
            CreateMap<OptionCreateDto, Option>();
            CreateMap<OptionUpdateDTO, Option>();

        }
    }
}
