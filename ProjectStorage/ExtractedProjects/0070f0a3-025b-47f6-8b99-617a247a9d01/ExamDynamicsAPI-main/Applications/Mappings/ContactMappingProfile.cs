using ExamDynamicsAPI.Core.DTOs.ContactMessageDTOs;
using ExamDynamicsAPI.Core.Models;

namespace ExamDynamicsAPI.Applications.Mappings
{
    public class ContactMappingProfile : AutoMapper.Profile
    {
        public ContactMappingProfile()
        {
            CreateMap<ContactMessageDto, ContactMessage>();
        }
    }
}
