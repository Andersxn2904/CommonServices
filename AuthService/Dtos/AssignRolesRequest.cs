using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class AssignRolesRequest
{
    [Required, MinLength(1)]
    public List<Guid> RoleIds { get; set; } = [];
}
