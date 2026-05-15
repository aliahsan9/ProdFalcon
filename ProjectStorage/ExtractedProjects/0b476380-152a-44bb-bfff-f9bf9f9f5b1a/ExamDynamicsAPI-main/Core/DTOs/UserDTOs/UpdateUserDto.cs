namespace ExamDynamicsAPI.Core.DTOs.UserDTOs
{
    public class UpdateUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
    }
}