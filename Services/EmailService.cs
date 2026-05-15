using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace GadgetVault.Services
{
    public class EmailService : IEmailService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _user;
        private readonly string _pass;

        public EmailService()
        {
            _host = Environment.GetEnvironmentVariable("SMTP_HOST") ?? string.Empty;
            _port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var port) ? port : 587;
            _user = Environment.GetEnvironmentVariable("SMTP_USER") ?? string.Empty;
            _pass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? string.Empty;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                if (string.IsNullOrEmpty(_host) || string.IsNullOrEmpty(_user) || string.IsNullOrEmpty(_pass))
                {
                    Console.WriteLine("Email Service Error: SMTP configuration is missing.");
                    return false;
                }

                using var client = new SmtpClient(_host, _port)
                {
                    Credentials = new NetworkCredential(_user, _pass),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_user, "GadgetVault System"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                // Report the failure gracefully
                Console.WriteLine($"Email Service Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendInviteEmailAsync(string toEmail, string username, string tempPassword)
        {
            var subject = "Welcome to GadgetVault - Account Invitation";
            var body = $@"
                <div style='font-family: sans-serif; padding: 30px; border: 1px solid #e2e8f0; border-radius: 12px; max-width: 600px; margin: 20px auto; color: #1f2937;'>
                    <h2 style='color: #4F46E5; margin-top: 0;'>Welcome to GadgetVault!</h2>
                    <p style='font-size: 1.1rem;'>You have been invited to join the <strong>GadgetVault ERP</strong> system.</p>
                    <div style='background-color: #f8fafc; padding: 20px; border-radius: 8px; margin: 25px 0;'>
                        <p style='margin: 0 0 10px 0;'><strong>Login Email:</strong> {toEmail}</p>
                        <p style='margin: 0;'><strong>Temporary Password:</strong> <span style='font-family: monospace; color: #4F46E5;'>{tempPassword}</span></p>
                    </div>
                    <p>Please use these credentials to access your dashboard at your earliest convenience.</p>
                    <p style='color: #ef4444;'><strong>For security purposes, please change your password immediately after your first successful login.</strong></p>
                    <hr style='border: 0; border-top: 1px solid #e2e8f0; margin: 30px 0;' />
                    <p style='font-size: 0.8rem; color: #64748b; margin-bottom: 0;'>This is an automated message sent from the GadgetVault System. Please do not reply directly to this email.</p>
                </div>";
            
            return await SendEmailAsync(toEmail, subject, body);
        }
    }
}
