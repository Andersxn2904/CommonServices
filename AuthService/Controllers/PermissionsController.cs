using AuthService.Dtos;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
[Route("permissions")]
public class PermissionsController(PermissionService permissionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? applicationId = null) =>
        Ok(await permissionService.GetAllAsync(applicationId));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePermissionRequest request)
    {
        try
        {
            var permission = await permissionService.CreateAsync(request);
            return StatusCode(201, permission);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
