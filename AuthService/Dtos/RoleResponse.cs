namespace AuthService.Dtos;

public class RoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ApplicationId { get; set; }
    public string? ApplicationCode { get; set; }
    public bool IsActive { get; set; }
}
