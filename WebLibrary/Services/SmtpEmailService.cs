using System.Net;
using System.Net.Mail;



namespace WebLibrary.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly SmtpClient _client;
        private readonly string _fromEmail;

        public SmtpEmailService(IConfiguration config)
        {
            var emailConfig = config.GetSection("Email");

            _fromEmail = emailConfig["FromAddress"];
            var host = emailConfig["SmtpHost"];
            var port = int.Parse(emailConfig["SmtpPort"]);
            var username = emailConfig["Username"];
            var password = emailConfig["Password"];

            _client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };
        }

        public async Task SendEmailAsync(string to, string subject, string htmlContent)
        {
            var mail = new MailMessage(_fromEmail, to)
            {
                Subject = subject,
                Body = htmlContent,
                IsBodyHtml = true
            };

            await _client.SendMailAsync(mail);
        }
    }
}
