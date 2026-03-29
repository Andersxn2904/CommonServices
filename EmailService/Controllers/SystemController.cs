using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace EmailService.Controllers;

[ApiController]
public class SystemController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "healthy", timestamp = DateTime.UtcNow });

    [HttpGet("version")]
    public IActionResult Version()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "1.0.0";

        return Ok(new { version, timestamp = DateTime.UtcNow });
    }
}
