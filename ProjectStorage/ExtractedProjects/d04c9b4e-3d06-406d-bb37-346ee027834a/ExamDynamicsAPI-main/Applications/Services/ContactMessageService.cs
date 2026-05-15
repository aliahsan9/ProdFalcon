using ExamDynamicsAPI.Core.DTOs.ContactMessageDTOs;
using ExamDynamicsAPI.Core.Interfaces.Repositories;
using ExamDynamicsAPI.Core.Interfaces.Services;
using ExamDynamicsAPI.Core.Models;
using System.Net;
using System.Net.Mail;

namespace ExamDynamicsAPI.Applications.Services
{
    public class ContactMessageService : IContactMessageService
    {
        private readonly IContactMessageRepository _repository;
        private readonly IConfiguration _configuration;

        public ContactMessageService(
            IContactMessageRepository repository,
            IConfiguration configuration)
        {
            _repository = repository;
            _configuration = configuration;
        }

        public async Task SendMessageAsync(ContactMessageDto dto)
        {
            // ✅ Resolve email
            var visitorEmail = dto.ResolvedEmail?.Trim();

            if (string.IsNullOrWhiteSpace(visitorEmail))
                throw new Exception("Visitor email is required.");

            // ✅ Save to DB
            var contactMessage = new ContactMessage
            {
                UserEmail = visitorEmail,
                Message = dto.Message
            };

            await _repository.AddAsync(contactMessage);

            // ✅ Read config (CORRECT KEYS)
            var smtp = _configuration.GetSection("EmailSettings");

            string smtpServer = smtp["SmtpServer"] ?? throw new Exception("SmtpServer missing");
            int port = int.TryParse(smtp["Port"], out var p) ? p : 587;

            string senderEmail = smtp["SenderEmail"] ?? throw new Exception("SenderEmail missing");
            string username = smtp["Username"] ?? senderEmail; // fallback
            string password = smtp["Password"] ?? throw new Exception("Password missing");

            // ✅ Use sender as receiver (admin inbox)
            string receiverEmail = senderEmail;

            // ✅ Create SMTP client (FIXED)
            using var client = new SmtpClient(smtpServer, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
                UseDefaultCredentials = false
            };

            // ✅ Email content
            var subject = $"[Contact] {visitorEmail}";
            var body = $@"
New Contact Message:

From: {visitorEmail}

Message:
{dto.Message}
";

            // ✅ Create mail message
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "ExamDynamics"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            // ✅ IMPORTANT: add receiver properly
            mailMessage.To.Add(receiverEmail);

            // ✅ Add reply-to (safe)
            try
            {
                mailMessage.ReplyToList.Add(new MailAddress(visitorEmail));
            }
            catch
            {
                // ignore invalid email format
            }

            // ✅ Send email
            await client.SendMailAsync(mailMessage);
        }
    }
}