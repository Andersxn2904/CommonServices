using EmailService.Models;
using EmailService.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailSenderService _emailSenderService;

    public EmailController(IEmailSenderService emailSenderService)
    {
        _emailSenderService = emailSenderService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendEmail([FromForm] EmailRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
            
        try
        {
            await _emailSenderService.SendEmailAsync(request);
            return Ok(new { message = "Email sent successfully" });
        }
        catch (Exception ex)
        {
            // Ideally, log the exception here
            return StatusCode(500, new { message = "Error sending email", details = ex.Message });
        }
    }
}
