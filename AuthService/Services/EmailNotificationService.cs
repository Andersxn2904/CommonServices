using AuthService.Configurations;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

public class EmailNotificationService(
    IHttpClientFactory httpClientFactory,
    IOptions<EmailServiceOptions> emailOpts,
    IOptions<AppOptions> appOpts,
    ILogger<EmailNotificationService> logger)
{
    private readonly string _emailBaseUrl = emailOpts.Value.BaseUrl.TrimEnd('/');
    private readonly string _appBaseUrl = appOpts.Value.BaseUrl.TrimEnd('/');

    public async Task SendWelcomeAsync(string toEmail, string displayName)
    {
        var subject = "Bienvenido a la plataforma";
        var body = $"""
            <h2>¡Hola, {displayName}!</h2>
            <p>Tu cuenta ha sido creada exitosamente.</p>
            <p>Verifica tu correo electrónico para activar tu cuenta.</p>
            """;

        await SendAsync(toEmail, subject, body);
    }

    public async Task SendEmailVerificationAsync(string toEmail, string displayName, string token)
    {
        var encodedToken = Uri.EscapeDataString(token);
        var verificationLink = $"{_appBaseUrl}/auth/verify-email?token={encodedToken}";

        var subject = "Verifica tu dirección de correo";
        var body = $"""
            <h2>Hola, {displayName}</h2>
            <p>Haz clic en el siguiente enlace para verificar tu correo electrónico y activar tu cuenta:</p>
            <p>
                <a href="{verificationLink}"
                   style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;border-radius:6px;text-decoration:none;font-weight:bold;">
                    Verificar mi cuenta
                </a>
            </p>
            <p>O copia y pega este enlace en tu navegador:</p>
            <p style="word-break:break-all;color:#6b7280;font-size:13px;">{verificationLink}</p>
            <p>Este enlace expira en <strong>24 horas</strong>.</p>
            <p>Si no creaste esta cuenta, ignora este mensaje.</p>
            """;

        await SendAsync(toEmail, subject, body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string displayName, string token)
    {
        var encodedToken = Uri.EscapeDataString(token);
        var resetLink = $"{_appBaseUrl}/auth/reset-password?token={encodedToken}";

        var subject = "Recuperación de contraseña";
        var body = $"""
            <h2>Hola, {displayName}</h2>
            <p>Recibimos una solicitud para restablecer tu contraseña.</p>
            <p>
                <a href="{resetLink}"
                   style="display:inline-block;padding:12px 24px;background:#4f46e5;color:#fff;border-radius:6px;text-decoration:none;font-weight:bold;">
                    Restablecer contraseña
                </a>
            </p>
            <p>O copia y pega este enlace en tu navegador:</p>
            <p style="word-break:break-all;color:#6b7280;font-size:13px;">{resetLink}</p>
            <p>Este enlace expira en <strong>1 hora</strong>.</p>
            <p>Si no solicitaste esto, ignora este mensaje. Tu contraseña no cambiará.</p>
            """;

        await SendAsync(toEmail, subject, body);
    }

    private async Task SendAsync(string toEmail, string subject, string body)
    {
        try
        {
            var http = httpClientFactory.CreateClient("EmailService");
            var content = new FormUrlEncodedContent([
                new("ToEmail", toEmail),
                new("Subject", subject),
                new("Body", body),
                new("IsHtml", "true"),
            ]);

            var response = await http.PostAsync($"{_emailBaseUrl}/api/email/send", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("EmailService respondió {StatusCode} para {ToEmail}", response.StatusCode, toEmail);
            else
                logger.LogInformation("Email '{Subject}' enviado a {ToEmail}", subject, toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar email a {ToEmail}", toEmail);
        }
    }
}
