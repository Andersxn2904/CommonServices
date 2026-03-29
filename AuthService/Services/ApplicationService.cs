using AuthService.Data;
using AuthService.Dtos;
using AuthService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class ApplicationService(AppDbContext db, ILogger<ApplicationService> logger)
{
    public async Task<ApplicationResponse> CreateAsync(CreateApplicationRequest request)
    {
        var exists = await db.Applications.AnyAsync(a => a.Code == request.Code.ToLower());
        if (exists)
            throw new InvalidOperationException($"Ya existe una aplicación con el código '{request.Code}'.");

        var app = new Application
        {
            Code = request.Code.ToLower(),
            Name = request.Name,
            Description = request.Description,
        };

        db.Applications.Add(app);
        await db.SaveChangesAsync();

        logger.LogInformation("Aplicación registrada: {AppId} [{Code}]", app.Id, app.Code);
        return ToResponse(app);
    }

    public async Task<List<ApplicationResponse>> GetAllAsync() =>
        await db.Applications
            .OrderBy(a => a.Name)
            .Select(a => ToResponse(a))
            .ToListAsync();

    public async Task<Application?> GetByIdAsync(Guid id) =>
        await db.Applications.FindAsync(id);

    public static ApplicationResponse ToResponse(Application app) => new()
    {
        Id = app.Id,
        Code = app.Code,
        Name = app.Name,
        IsActive = app.IsActive,
        Description = app.Description,
    };
}
