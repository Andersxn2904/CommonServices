using AuthService.Auth;
using AuthService.Data;
using AuthService.Dtos;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class UserService(AppDbContext db, PasswordHasher passwordHasher, TokenService tokenService, EmailNotificationService emailNotification, ILogger<UserService> logger)
{
    public async Task<UserResponse> CreateAsync(CreateUserRequest request)
    {
        var exists = await db.Users.AnyAsync(u => u.Email == request.Email.ToLower());
        if (exists)
            throw new InvalidOperationException("Ya existe un usuario con ese email.");

        var verificationPlain = tokenService.GenerateRefreshTokenValue();
        var verificationHash = tokenService.HashToken(verificationPlain);

        var user = new User
        {
            Email = request.Email.ToLower(),
            DisplayName = request.DisplayName,
            PasswordHash = passwordHasher.Hash(request.Password),
            IsActive = false,
            EmailVerificationTokenHash = verificationHash,
            EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        logger.LogInformation("Usuario creado: {UserId} {Email}", user.Id, user.Email);

        _ = emailNotification.SendEmailVerificationAsync(user.Email, user.DisplayName, verificationPlain);

        return ToResponse(user);
    }

    public async Task<User?> GetByIdEntityAsync(Guid id) =>
        await db.Users.FindAsync(id);

    public async Task<User?> GetByEmailEntityAsync(string email) =>
        await db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower());

    public async Task<UserResponse?> GetByIdAsync(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        return user is null ? null : ToResponse(user);
    }

    public async Task AssignRolesAsync(Guid userId, AssignRolesRequest request)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        var existingRoleIds = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        var toAdd = request.RoleIds
            .Except(existingRoleIds)
            .Select(rid => new UserRole { UserId = userId, RoleId = rid });

        db.UserRoles.AddRange(toAdd);
        await db.SaveChangesAsync();

        logger.LogInformation("Roles asignados al usuario {UserId}: {Count}", userId, request.RoleIds.Count);
    }

    public async Task<(List<string> roles, List<string> permissions)> GetAuthorizationAsync(Guid userId)
    {
        var roleNames = await db.UserRoles
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        var permissionCodes = await db.UserRoles
            .Where(ur => ur.UserId == userId && ur.Role.IsActive)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Where(rp => rp.Permission.IsActive)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

        return (roleNames, permissionCodes);
    }

    public async Task<List<UserResponse>> GetAllAsync(UserQuery query)
    {
        var q = db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Application)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            q = q.Where(u => u.Email.Contains(search) || u.DisplayName.ToLower().Contains(search));
        }

        if (query.IsActive.HasValue)
            q = q.Where(u => u.IsActive == query.IsActive.Value);

        if (query.RoleId.HasValue)
            q = q.Where(u => u.UserRoles.Any(ur => ur.RoleId == query.RoleId.Value));

        if (query.ApplicationId.HasValue)
            q = q.Where(u => u.UserRoles.Any(ur => ur.Role.ApplicationId == query.ApplicationId.Value));

        if (!string.IsNullOrWhiteSpace(query.ApplicationCode))
            q = q.Where(u => u.UserRoles.Any(ur => ur.Role.Application != null &&
                ur.Role.Application.Code == query.ApplicationCode));

        var users = await q.OrderBy(u => u.Email).ToListAsync();

        return users.Select(u => new UserResponse
        {
            Id = u.Id,
            Email = u.Email,
            DisplayName = u.DisplayName,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            Roles = u.UserRoles
                .Where(ur => ur.Role.IsActive)
                .Select(ur => ur.Role.Name)
                .ToList(),
        }).ToList();
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        var user = await db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        var emailTaken = await db.Users
            .AnyAsync(u => u.Email == request.Email.ToLower() && u.Id != id);
        if (emailTaken)
            throw new InvalidOperationException("Ya existe un usuario con ese email.");

        user.Email = request.Email.ToLower();
        user.DisplayName = request.DisplayName;
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Usuario actualizado: {UserId}", user.Id);
        return ToResponse(user);
    }

    public async Task<UserResponse> SetStatusAsync(Guid id, bool isActive)
    {
        var user = await db.Users.FindAsync(id)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Estado de usuario {UserId} cambiado a {IsActive}", user.Id, isActive);
        return ToResponse(user);
    }

    public async Task RecordFailedLoginAsync(User user, int maxAttempts, int lockoutMinutes)
    {
        user.FailedLoginAttempts++;

        if (user.FailedLoginAttempts >= maxAttempts)
        {
            user.LockedUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes);
            logger.LogWarning(
                "Cuenta bloqueada por {LockoutMinutes} min: {UserId} {Email} tras {Attempts} intentos fallidos",
                lockoutMinutes, user.Id, user.Email, user.FailedLoginAttempts);
        }

        await db.SaveChangesAsync();
    }

    public async Task ResetLoginAttemptsAsync(User user)
    {
        if (user.FailedLoginAttempts == 0 && user.LockedUntil is null)
            return;

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await db.SaveChangesAsync();
    }

    public async Task UpdatePasswordAsync(User user, string newPassword)
    {
        user.PasswordHash = passwordHasher.Hash(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        logger.LogInformation("Contrase\u00f1a actualizada para usuario: {UserId}", user.Id);
    }

    public static UserResponse ToResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
    };
}
