using AuthService.Data;
using AuthService.Dtos;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class PermissionService(AppDbContext db, ApplicationService appService, ILogger<PermissionService> logger)
{
    public async Task<PermissionResponse> CreateAsync(CreatePermissionRequest request)
    {
        if (request.ApplicationId.HasValue)
        {
            var app = await appService.GetByIdAsync(request.ApplicationId.Value);
            if (app is null || !app.IsActive)
                throw new InvalidOperationException("La aplicación no existe o está inactiva.");
        }

        var exists = await db.Permissions.AnyAsync(p =>
            p.Code == request.Code.ToLower() && p.ApplicationId == request.ApplicationId);
        if (exists)
            throw new InvalidOperationException($"Ya existe un permiso con el código '{request.Code}' en ese alcance.");

        var permission = new Permission
        {
            Code = request.Code.ToLower(),
            ApplicationId = request.ApplicationId,
            Description = request.Description,
        };

        db.Permissions.Add(permission);
        await db.SaveChangesAsync();

        await db.Entry(permission).Reference(p => p.Application).LoadAsync();

        logger.LogInformation("Permiso creado: {PermId} [{Code}]", permission.Id, permission.Code);
        return ToResponse(permission);
    }

    public async Task<List<PermissionResponse>> GetAllAsync(Guid? applicationId = null)
    {
        var query = db.Permissions.Include(p => p.Application).AsQueryable();

        if (applicationId.HasValue)
            query = query.Where(p => p.ApplicationId == applicationId);

        return await query
            .OrderBy(p => p.Code)
            .Select(p => ToResponse(p))
            .ToListAsync();
    }

    public static PermissionResponse ToResponse(Permission p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        ApplicationId = p.ApplicationId,
        ApplicationCode = p.Application?.Code,
        IsActive = p.IsActive,
        Description = p.Description,
    };
}
