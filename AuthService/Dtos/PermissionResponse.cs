namespace AuthService.Dtos;

public class PermissionResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? ApplicationId { get; set; }
    public string? ApplicationCode { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}
