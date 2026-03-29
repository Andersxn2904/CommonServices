namespace AuthService.Entities;

public class Application
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<Permission> Permissions { get; set; } = [];
}
