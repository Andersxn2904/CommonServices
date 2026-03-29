using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class CreateRoleRequest
{
    [Required, MinLength(2)]
    public string Name { get; set; } = string.Empty;

    public Guid? ApplicationId { get; set; }
}
