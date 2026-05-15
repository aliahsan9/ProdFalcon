using ExamDynamicsAPI.Core.DTOs.ContactMessageDTOs;

namespace ExamDynamicsAPI.Core.Interfaces.Services
{
    public interface IContactMessageService
    {
        Task SendMessageAsync(ContactMessageDto dto);
    }
}
 