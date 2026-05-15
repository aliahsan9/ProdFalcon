namespace ExamDynamicsAPI.Core.DTOs.AuthDTOs
{
    public class UserSignupDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; 
    }
}