using AuthService.Auth;
using AuthService.Data;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class RefreshTokenService(AppDbContext db, TokenService tokenService, ILogger<RefreshTokenService> logger)
{
    public async Task<(RefreshToken entity, string plainToken)> IssueAsync(Guid userId, string? ip, string? deviceInfo)
    {
        var plain = tokenService.GenerateRefreshTokenValue();
        var hash = tokenService.HashToken(plain);

        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ip,
            DeviceInfo = deviceInfo,
        };

        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        return (token, plain);
    }

    public async Task<RefreshToken?> FindActiveAsync(string plainToken)
    {
        var hash = tokenService.HashToken(plainToken);
        return await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash);
    }

    public async Task<(RefreshToken newEntity, string newPlain)> RotateAsync(RefreshToken old, string? ip, string? deviceInfo)
    {
        var plain = tokenService.GenerateRefreshTokenValue();
        var hash = tokenService.HashToken(plain);

        old.RevokedAt = DateTime.UtcNow;
        old.RevokedByIp = ip;
        old.ReplacedByTokenHash = hash;

        var newToken = new RefreshToken
        {
            UserId = old.UserId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ip,
            DeviceInfo = deviceInfo,
        };

        db.RefreshTokens.Add(newToken);
        await db.SaveChangesAsync();

        logger.LogInformation("Refresh token rotado para usuario: {UserId}", old.UserId);
        return (newToken, plain);
    }

    public async Task RevokeAsync(RefreshToken token, string? ip)
    {
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = ip;
        await db.SaveChangesAsync();
        logger.LogInformation("Refresh token revocado para usuario: {UserId}", token.UserId);
    }

    public async Task RevokeAllForUserAsync(Guid userId, string? ip)
    {
        var active = await db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var t in active)
        {
            t.RevokedAt = DateTime.UtcNow;
            t.RevokedByIp = ip;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Todas las sesiones revocadas para usuario: {UserId} ({Count} tokens)", userId, active.Count);
    }
}
