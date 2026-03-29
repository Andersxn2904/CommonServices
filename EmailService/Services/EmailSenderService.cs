using EmailService.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailService.Services;

public class EmailSenderService : IEmailSenderService
{
    private readonly EmailConfiguration _emailConfig;
    private readonly ILogger<EmailSenderService> _logger;

    public EmailSenderService(IOptions<EmailConfiguration> emailConfig, ILogger<EmailSenderService> logger)
    {
        _emailConfig = emailConfig.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailRequest request)
    {
        var emailMessage = new MimeMessage();
        emailMessage.From.Add(new MailboxAddress(_emailConfig.FromName, _emailConfig.FromAddress));
        emailMessage.To.Add(new MailboxAddress(request.ToEmail, request.ToEmail));
        emailMessage.Subject = request.Subject;

        var bodyBuilder = new BodyBuilder();

        if (request.IsHtml)
            bodyBuilder.HtmlBody = request.Body;
        else
            bodyBuilder.TextBody = request.Body;

        if (request.Attachments != null && request.Attachments.Any())
        {
            foreach (var attachment in request.Attachments)
            {
                if (attachment.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await attachment.CopyToAsync(ms);
                    bodyBuilder.Attachments.Add(attachment.FileName, ms.ToArray(), ContentType.Parse(attachment.ContentType));
                }
            }
        }

        emailMessage.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            _logger.LogInformation("Conectando a SMTP {Server}:{Port} para enviar a {ToEmail}",
                _emailConfig.SmtpServer, _emailConfig.Port, request.ToEmail);

            await client.ConnectAsync(_emailConfig.SmtpServer, _emailConfig.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_emailConfig.UserName, _emailConfig.Password);
            await client.SendAsync(emailMessage);

            _logger.LogInformation("Email '{Subject}' enviado exitosamente a {ToEmail}", request.Subject, request.ToEmail);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            _logger.LogWarning(ex, "Fallo de autenticación SMTP para usuario {UserName}", _emailConfig.UserName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar email '{Subject}' a {ToEmail} via {Server}:{Port}",
                request.Subject, request.ToEmail, _emailConfig.SmtpServer, _emailConfig.Port);
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }
}
