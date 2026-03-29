using System.Security.Claims;
using AuthService.Dtos;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthenticationService authService, UserService userService) : ControllerBase
{
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await authService.LoginAsync(request, ClientIp);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await authService.RefreshAsync(request.RefreshToken, ClientIp);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await authService.LogoutAsync(request.RefreshToken, ClientIp);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

        var user = await userService.GetByIdEntityAsync(userId);
        if (user is null) return NotFound();

        var (roles, permissions) = await userService.GetAuthorizationAsync(userId);

        return Ok(new MeResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
            Roles = roles,
            Permissions = permissions,
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

        try
        {
            await authService.ChangePasswordAsync(userId, request, ClientIp);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        // Siempre responde 204 para no revelar si el email existe
        await authService.ForgotPasswordAsync(request.Email);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await authService.ResetPasswordAsync(request, ClientIp);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmailByLink([FromQuery] string token)
    {
        try
        {
            await authService.VerifyEmailAsync(token);
            var html = """
                <!DOCTYPE html>
                <html lang="es">
                <head><meta charset="UTF-8"><title>Cuenta verificada</title></head>
                <body style="font-family:sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#f9fafb;">
                  <div style="text-align:center;padding:40px;background:#fff;border-radius:12px;box-shadow:0 2px 16px rgba(0,0,0,.08);max-width:420px;">
                    <div style="font-size:56px;">✅</div>
                    <h1 style="color:#111;margin:16px 0 8px;">¡Cuenta verificada!</h1>
                    <p style="color:#6b7280;">Tu correo ha sido verificado y tu cuenta está activa. Ya puedes iniciar sesión.</p>
                  </div>
                </body>
                </html>
                """;
            return Content(html, "text/html");
        }
        catch (InvalidOperationException)
        {
            var html = """
                <!DOCTYPE html>
                <html lang="es">
                <head><meta charset="UTF-8"><title>Enlace inválido</title></head>
                <body style="font-family:sans-serif;display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0;background:#f9fafb;">
                  <div style="text-align:center;padding:40px;background:#fff;border-radius:12px;box-shadow:0 2px 16px rgba(0,0,0,.08);max-width:420px;">
                    <div style="font-size:56px;">❌</div>
                    <h1 style="color:#111;margin:16px 0 8px;">Enlace inválido</h1>
                    <p style="color:#6b7280;">El enlace de verificación es inválido o ya expiró. Solicita un nuevo correo de verificación.</p>
                  </div>
                </body>
                </html>
                """;
            return Content(html, "text/html");
        }
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        try
        {
            await authService.VerifyEmailAsync(request.Token);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
