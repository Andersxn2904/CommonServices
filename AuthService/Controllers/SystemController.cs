using System.Reflection;
using AuthService.Data;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
public class SystemController(AppDbContext db) : ControllerBase
{
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        try
        {
            await db.Database.CanConnectAsync();
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
        catch
        {
            return StatusCode(503, new { status = "unhealthy", timestamp = DateTime.UtcNow });
        }
    }

    [HttpGet("version")]
    public IActionResult Version()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "1.0.0";

        return Ok(new { version, timestamp = DateTime.UtcNow });
    }
}
