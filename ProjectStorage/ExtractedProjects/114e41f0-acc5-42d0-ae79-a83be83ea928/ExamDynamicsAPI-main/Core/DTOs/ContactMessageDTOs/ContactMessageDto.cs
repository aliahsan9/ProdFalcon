using System.Text.Json.Serialization;

namespace ExamDynamicsAPI.Core.DTOs.ContactMessageDTOs
{
    public class ContactMessageDto
    {
        [JsonPropertyName("userEmail")]
        public string UserEmail { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }
 
        public string Message { get; set; } = string.Empty;

        [JsonIgnore]
        public string ResolvedEmail =>
            !string.IsNullOrWhiteSpace(UserEmail)
                ? UserEmail.Trim()
                : (Email?.Trim() ?? string.Empty);
    }
}
