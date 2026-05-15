using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ExamDynamicsAPI.Core.Interfaces.Services;

namespace ExamDynamicsAPI.Applications.Services
{
    public class OpenAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public OpenAIService(IHttpClientFactory factory, IConfiguration config)
        {
            _httpClient = factory.CreateClient();
            _apiKey = config["OpenAI:ApiKey"]!;
        }

        // 🔥 MAIN RAG METHOD
        public async Task<string> GetAnswerAsync(string question, string context)
        {
            var messages = new[]
            {
                new { role = "system", content = $"Use this context to answer:\n{context}" },
                new { role = "user", content = question }
            };

            var payload = new
            {
                model = "gpt-4o-mini",
                messages = messages
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                content
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"OpenAI Error: {response.StatusCode} - {error}";
            }

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);

            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString() ?? "No response";
        }

        // ✅ OPTIONAL METHOD (to fix your error)
        public async Task<string> AskQuestion(string question)
        {
            // No RAG context (fallback)
            return await GetAnswerAsync(question, "");
        }
    }
}