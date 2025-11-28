// Services/EmailService.cs
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace ProjectCapstone.Services
{
    public interface IEmailService
    {
        Task SendDocumentReadyEmailAsync(string toEmail, string studentName, string documentType, string queueNumber);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendDocumentReadyEmailAsync(string toEmail, string studentName, string documentType, string queueNumber)
        {
            try
            {
                _logger.LogInformation($"📧 Preparing email for {studentName} ({toEmail})");

                var emailMessage = CreateEmailMessage(toEmail, studentName, documentType, queueNumber);
                await SendEmailAsync(emailMessage);

                _logger.LogInformation($"✅ Email sent successfully to {toEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error sending email: {ex.Message}");
                throw;
            }
        }

        private MimeMessage CreateEmailMessage(string toEmail, string studentName, string documentType, string queueNumber)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]
            ));
            message.To.Add(new MailboxAddress(studentName, toEmail));
            message.Subject = "📄 Your Document is Ready for Pickup - CDM Document Queue";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = GetEmailTemplate(studentName, documentType, queueNumber)
            };

            message.Body = bodyBuilder.ToMessageBody();
            return message;
        }

        private async Task SendEmailAsync(MimeMessage message)
        {
            using var client = new SmtpClient();

            try
            {
                // Connect to SMTP server
                await client.ConnectAsync(
                    _configuration["EmailSettings:SmtpServer"],
                    int.Parse(_configuration["EmailSettings:Port"]),
                    SecureSocketOptions.StartTls
                );

                // Authenticate
                await client.AuthenticateAsync(
                    _configuration["EmailSettings:SenderEmail"],
                    _configuration["EmailSettings:Password"]
                );

                // Send email
                await client.SendAsync(message);

                _logger.LogInformation("📬 Email sent via SMTP");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ SMTP Error: {ex.Message}");
                throw;
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }

        private string GetEmailTemplate(string studentName, string documentType, string queueNumber)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background: #f5f7fa; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; }}
        .header {{ background: linear-gradient(135deg, #0d5c2f 0%, #1a8f4f 100%); color: white; padding: 40px 30px; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 32px; font-weight: 700; }}
        .header p {{ margin: 10px 0 0 0; font-size: 16px; opacity: 0.9; }}
        .content {{ padding: 40px 30px; }}
        .logo-container {{ width: 80px; height: 80px; margin: 0 auto 20px; background: white; border-radius: 50%; padding: 10px; }}
        .greeting {{ font-size: 20px; font-weight: 600; color: #2d3748; margin-bottom: 15px; }}
        .message {{ font-size: 16px; color: #4a5568; margin-bottom: 30px; }}
        .info-box {{ 
            background: #f0fdf4; 
            border-left: 4px solid #22c55e; 
            padding: 25px;
            margin: 30px 0;
            border-radius: 8px;
        }}
        .info-box h3 {{ 
            margin: 0 0 15px 0; 
            color: #166534; 
            font-size: 18px;
            display: flex;
            align-items: center;
            gap: 8px;
        }}
        .info-item {{ 
            display: flex;
            justify-content: space-between;
            padding: 10px 0;
            border-bottom: 1px solid #dcfce7;
        }}
        .info-item:last-child {{ border-bottom: none; }}
        .info-label {{ color: #6b7280; font-size: 14px; }}
        .info-value {{ 
            color: #1f2937; 
            font-weight: 600;
            font-size: 14px;
        }}
        .queue-number {{ 
            background: linear-gradient(135deg, #0d5c2f 0%, #1a8f4f 100%);
            color: white;
            padding: 4px 12px;
            border-radius: 6px;
            font-weight: 700;
            font-size: 16px;
        }}
        .instructions {{ 
            background: #fff7ed; 
            border-radius: 8px;
            padding: 25px;
            margin: 30px 0;
        }}
        .instructions h3 {{ 
            margin: 0 0 15px 0;
            color: #9a3412;
            font-size: 18px;
        }}
        .instructions ul {{ 
            margin: 0;
            padding-left: 20px;
        }}
        .instructions li {{ 
            color: #78350f;
            margin: 10px 0;
            font-size: 15px;
        }}
        .office-hours {{ 
            background: #dbeafe;
            border-radius: 8px;
            padding: 20px;
            margin: 20px 0;
        }}
        .office-hours h4 {{ 
            margin: 0 0 10px 0;
            color: #1e40af;
            font-size: 16px;
        }}
        .office-hours p {{ 
            margin: 5px 0;
            color: #1e3a8a;
            font-size: 14px;
        }}
        .warning {{ 
            background: #fef3c7;
            border-left: 4px solid #f59e0b;
            padding: 20px;
            border-radius: 8px;
            margin: 30px 0;
        }}
        .warning p {{ 
            margin: 0;
            color: #92400e;
            font-size: 14px;
            display: flex;
            align-items: center;
            gap: 8px;
        }}
        .footer {{ 
            background: #f9fafb;
            padding: 30px;
            text-align: center;
            border-top: 1px solid #e5e7eb;
        }}
        .footer p {{ 
            margin: 5px 0;
            color: #6b7280;
            font-size: 13px;
        }}
        .footer strong {{ 
            color: #1f2937;
        }}
        .button {{ 
            display: inline-block;
            background: linear-gradient(135deg, #0d5c2f 0%, #1a8f4f 100%);
            color: white;
            padding: 14px 32px;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            margin: 20px 0;
            font-size: 16px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo-container'>
                <svg xmlns='http://www.w3.org/2000/svg' fill='#0d5c2f' viewBox='0 0 24 24'>
                    <path d='M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z'/>
                </svg>
            </div>
            <h1>✅ Document Ready!</h1>
            <p>Your requested document is now available for pickup</p>
        </div>
        
        <div class='content'>
            <p class='greeting'>Hello {studentName},</p>
            
            <p class='message'>
                Great news! Your document request has been processed and is now ready for pickup at the Registrar's Office.
            </p>
            
            <div class='info-box'>
                <h3>📋 Document Details</h3>
                <div class='info-item'>
                    <span class='info-label'>Queue Number:</span>
                    <span class='queue-number'>{queueNumber}</span>
                </div>
                <div class='info-item'>
                    <span class='info-label'>Document Type:</span>
                    <span class='info-value'>{documentType}</span>
                </div>
                <div class='info-item'>
                    <span class='info-label'>Status:</span>
                    <span class='info-value'>✅ Ready for Pickup</span>
                </div>
            </div>
            
            <div class='instructions'>
                <h3>📍 What to do next:</h3>
                <ul>
                    <li><strong>Visit the Registrar's Office</strong> during office hours</li>
                    <li>Bring your <strong>Student ID</strong> or valid government-issued identification</li>
                    <li>Present your Queue Number: <strong>{queueNumber}</strong></li>
                    <li>Sign the document release form upon pickup</li>
                </ul>
            </div>
            
            <div class='office-hours'>
                <h4>🕐 Office Hours:</h4>
                <p><strong>Monday - Friday:</strong> 8:00 AM - 5:00 PM</p>
                <p><strong>Saturday:</strong> 9:00 AM - 12:00 PM</p>
                <p><strong>Closed:</strong> Sundays and Holidays</p>
            </div>
            
            <div class='warning'>
                <p>
                    ⚠️ <strong>Important:</strong> Please collect your document within <strong>30 days</strong>. 
                    Unclaimed documents will be archived and may require a new request to retrieve.
                </p>
            </div>
            
            <p style='color: #4a5568; font-size: 15px; margin-top: 30px;'>
                If you have any questions or concerns, please contact the Registrar's Office or visit our help desk.
            </p>
            
            <p style='margin-top: 40px; color: #6b7280;'>
                Best regards,<br>
                <strong style='color: #1f2937;'>Registrar's Office</strong><br>
                <strong style='color: #0d5c2f;'>Colegio de Montalban</strong>
            </p>
        </div>
        
        <div class='footer'>
            <p>This is an automated message from the CDM Document Queue System.</p>
            <p>Please do not reply to this email.</p>
            <p style='margin-top: 15px;'>
                <strong>Colegio de Montalban</strong><br>
                Document Request System
            </p>
        </div>
    </div>
</body>
</html>";
        }
    }
}