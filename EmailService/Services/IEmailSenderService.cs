using EmailService.Models;

namespace EmailService.Services;

public interface IEmailSenderService
{
    Task SendEmailAsync(EmailRequest request);
}
