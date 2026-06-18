using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace CleaningManagmentSystem.Services
{
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly bool _enableSsl;
        private readonly string _username;
        private readonly string _password;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;

            var emailSettings = configuration.GetSection("EmailSettings");

            _smtpHost  = emailSettings["SmtpHost"]  ?? "mail.etbur.com";
            _smtpPort  = int.TryParse(emailSettings["SmtpPort"], out var port) ? port : 587;
            _enableSsl = bool.TryParse(emailSettings["EnableSsl"], out var ssl) ? ssl : true;
            _username  = emailSettings["Username"]  ?? "pay@etbur.com";
            _password  = emailSettings["Password"]  ?? "";
            _fromEmail = emailSettings["FromEmail"] ?? _username;
            _fromName  = emailSettings["FromName"]  ?? "Yeka Cleaning";
        }

        public async Task<(bool Success, string Error)> SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            bool isHtml = true)
        {
            try
            {
                using var message = new MailMessage
                {
                    From       = new MailAddress(_fromEmail, _fromName),
                    Subject    = subject,
                    Body       = body,
                    IsBodyHtml = isHtml
                };
                message.To.Add(toEmail);

                using var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
                {
                    Credentials          = new NetworkCredential(_username, _password),
                    EnableSsl            = _enableSsl,
                    UseDefaultCredentials = false,
                    Timeout              = 60000
                };

                await smtpClient.SendMailAsync(message);
                return (true, "");
            }
            catch (SmtpException smtpEx)
            {
                return (false, smtpEx.Message);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken)
        {
            try
            {
                var baseUrl   = _configuration["AppBaseUrl"] ?? "http://localhost:5000";
                var resetLink = $"{baseUrl}/ResetPassword?token={resetToken}&email={Uri.EscapeDataString(toEmail)}";
                var subject   = "Password Reset - Yeka Cleaning";

                var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; padding: 20px;'>
                    <h2 style='color: #008080;'>Yeka Cleaning - Password Reset</h2>
                    <p>You requested a password reset. Click below:</p>
                    <p style='margin: 20px 0;'>
                        <a href='{resetLink}'
                           style='background: darkcyan; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px;'>
                           Reset Password
                        </a>
                    </p>
                    <p>Or copy this link:</p>
                    <p style='word-break: break-all; color: #666;'>{resetLink}</p>
                    <hr>
                    <p style='color: #999; font-size: 12px;'>
                        If you didn't request this, ignore this email.<br>
                        This link expires in 1 hour.
                    </p>
                </body>
                </html>";

                var (success, _) = await SendEmailAsync(toEmail, subject, body);
                return success;
            }
            catch
            {
                return false;
            }
        }
    }
}
