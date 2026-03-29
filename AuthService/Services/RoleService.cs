using AuthService.Data;
using AuthService.Dtos;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class RoleService(AppDbContext db, ApplicationService appService, ILogger<RoleService> logger)
{
    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request)
    {
        if (request.ApplicationId.HasValue)
        {
            var app = await appService.GetByIdAsync(request.ApplicationId.Value);
            if (app is null || !app.IsActive)
                throw new InvalidOperationException("La aplicación no existe o está inactiva.");
        }

        var role = new Role
        {
            Name = request.Name,
            ApplicationId = request.ApplicationId,
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync();

        await db.Entry(role).Reference(r => r.Application).LoadAsync();

        logger.LogInformation("Rol creado: {RoleId} [{Name}]", role.Id, role.Name);
        return ToResponse(role);
    }

    public async Task<List<RoleResponse>> GetAllAsync() =>
        await db.Roles
            .Include(r => r.Application)
            .OrderBy(r => r.Name)
            .Select(r => ToResponse(r))
            .ToListAsync();

    public async Task<Role?> GetByIdAsync(Guid id) =>
        await db.Roles.Include(r => r.Application).FirstOrDefaultAsync(r => r.Id == id);

    public async Task AssignPermissionsAsync(Guid roleId, AssignPermissionsRequest request)
    {
        var role = await db.Roles.FindAsync(roleId)
            ?? throw new KeyNotFoundException("Rol no encontrado.");

        var existingIds = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var toAdd = request.PermissionIds
            .Except(existingIds)
            .Select(pid => new RolePermission { RoleId = roleId, PermissionId = pid });

        db.RolePermissions.AddRange(toAdd);
        await db.SaveChangesAsync();

        logger.LogInformation("Permisos asignados al rol {RoleId}: {Count}", roleId, request.PermissionIds.Count);
    }

    public static RoleResponse ToResponse(Role role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        ApplicationId = role.ApplicationId,
        ApplicationCode = role.Application?.Code,
        IsActive = role.IsActive,
    };
}
