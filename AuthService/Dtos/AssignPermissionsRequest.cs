using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class AssignPermissionsRequest
{
    [Required, MinLength(1)]
    public List<Guid> PermissionIds { get; set; } = [];
}
