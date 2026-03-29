using EmailService.Models;
using EmailService.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController(IEmailSenderService emailSenderService, ILogger<EmailController> logger) : ControllerBase
{
    [HttpPost("send")]
    public async Task<IActionResult> SendEmail([FromForm] EmailRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await emailSenderService.SendEmailAsync(request);
            return Ok(new { message = "Email sent successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error procesando solicitud de email para {ToEmail} con asunto '{Subject}'",
                request.ToEmail, request.Subject);
            return StatusCode(500, new { message = "Error sending email", details = ex.Message });
        }
    }
}
