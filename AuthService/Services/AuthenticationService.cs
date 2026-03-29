using AuthService.Auth;
using AuthService.Configurations;
using AuthService.Data;
using AuthService.Dtos;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

public class AuthenticationService(
    UserService userService,
    RefreshTokenService refreshTokenService,
    TokenService tokenService,
    PasswordHasher passwordHasher,
    EmailNotificationService emailNotification,
    AppDbContext db,
    IOptions<JwtOptions> jwtOptions,
    IOptions<LockoutOptions> lockoutOptions,
    ILogger<AuthenticationService> logger)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly LockoutOptions _lockout = lockoutOptions.Value;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ip)
    {
        var user = await userService.GetByEmailEntityAsync(request.Email);

        if (user is not null && user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            logger.LogWarning(
                "Login bloqueado para usuario: {UserId} {Email} desde IP: {Ip}. Bloqueado hasta: {LockedUntil}",
                user.Id, user.Email, ip, user.LockedUntil);
            throw new UnauthorizedAccessException("La cuenta está bloqueada temporalmente. Intenta más tarde.");
        }

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            if (user is not null)
            {
                await userService.RecordFailedLoginAsync(user, _lockout.MaxFailedAttempts, _lockout.LockoutDurationMinutes);
                logger.LogWarning(
                    "Intento de login fallido #{Attempts} para usuario: {UserId} {Email} desde IP: {Ip}",
                    user.FailedLoginAttempts, user.Id, user.Email, ip);
            }
            else
            {
                logger.LogWarning("Intento de login con email no registrado: {Email} desde IP: {Ip}", request.Email, ip);
            }

            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        if (!user.IsEmailVerified)
        {
            logger.LogWarning("Login denegado, email no verificado: {UserId}", user.Id);
            throw new UnauthorizedAccessException("Debes verificar tu email antes de iniciar sesión.");
        }

        if (!user.IsActive)
        {
            logger.LogWarning("Login denegado para usuario inactivo: {UserId}", user.Id);
            throw new UnauthorizedAccessException("La cuenta está desactivada.");
        }

        await userService.ResetLoginAttemptsAsync(user);

        var (roles, permissions) = await userService.GetAuthorizationAsync(user.Id);
        var accessToken = tokenService.GenerateAccessToken(user, roles, permissions);
        var (_, plainRefresh) = await refreshTokenService.IssueAsync(user.Id, ip, null);

        logger.LogInformation("Login exitoso para usuario: {UserId} {Email}", user.Id, user.Email);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = plainRefresh,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
        };
    }

    public async Task<LoginResponse> RefreshAsync(string plainRefreshToken, string? ip)
    {
        var token = await refreshTokenService.FindActiveAsync(plainRefreshToken);

        if (token is null || !token.IsActive)
        {
            logger.LogWarning("Intento de refresh con token inválido desde IP: {Ip}", ip);
            throw new UnauthorizedAccessException("Refresh token inválido o expirado.");
        }

        if (!token.User.IsActive)
        {
            logger.LogWarning("Refresh denegado para usuario inactivo: {UserId}", token.UserId);
            throw new UnauthorizedAccessException("La cuenta está desactivada.");
        }

        var (_, newPlain) = await refreshTokenService.RotateAsync(token, ip, null);
        var (roles, permissions) = await userService.GetAuthorizationAsync(token.UserId);
        var accessToken = tokenService.GenerateAccessToken(token.User, roles, permissions);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = newPlain,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
        };
    }

    public async Task LogoutAsync(string plainRefreshToken, string? ip)
    {
        var token = await refreshTokenService.FindActiveAsync(plainRefreshToken);
        if (token is null || !token.IsActive)
            return;

        await refreshTokenService.RevokeAsync(token, ip);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ip)
    {
        var user = await userService.GetByIdEntityAsync(userId)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("La contraseña actual es incorrecta.");

        await userService.UpdatePasswordAsync(user, request.NewPassword);
        await refreshTokenService.RevokeAllForUserAsync(userId, ip);

        logger.LogInformation("Cambio de contraseña y sesiones revocadas para usuario: {UserId}", userId);
    }

    public async Task ForgotPasswordAsync(string email)
    {
        // Respuesta genérica siempre para no revelar si el email existe
        var user = await userService.GetByEmailEntityAsync(email);
        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Forgot password para email no encontrado o inactivo: {Email}", email);
            return;
        }

        var plain = tokenService.GenerateRefreshTokenValue();
        var hash = tokenService.HashToken(plain);

        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        };

        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        _ = emailNotification.SendPasswordResetAsync(user.Email, user.DisplayName, plain);
        logger.LogInformation("Token de reset de contraseña generado para usuario: {UserId}", user.Id);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, string? ip)
    {
        var hash = tokenService.HashToken(request.Token);
        var resetToken = await db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (resetToken is null || !resetToken.IsValid)
            throw new UnauthorizedAccessException("Token de recuperación inválido o expirado.");

        resetToken.UsedAt = DateTime.UtcNow;
        await userService.UpdatePasswordAsync(resetToken.User, request.NewPassword);
        await refreshTokenService.RevokeAllForUserAsync(resetToken.UserId, ip);

        logger.LogInformation("Contraseña restablecida para usuario: {UserId}", resetToken.UserId);
    }

    public async Task VerifyEmailAsync(string token)
    {
        var hash = tokenService.HashToken(token);
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailVerificationTokenHash == hash);

        if (user is null || user.EmailVerificationTokenExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Token de verificación inválido o expirado.");

        user.IsEmailVerified = true;
        user.IsActive = true;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Email verificado y cuenta activada para usuario: {UserId}", user.Id);

        _ = emailNotification.SendWelcomeAsync(user.Email, user.DisplayName);
    }
}
