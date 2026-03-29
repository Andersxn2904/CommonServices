namespace AuthService.Entities;

public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public Guid? ApplicationId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public Application? Application { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
