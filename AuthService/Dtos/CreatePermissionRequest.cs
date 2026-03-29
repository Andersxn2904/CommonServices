using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class CreatePermissionRequest
{
    [Required, RegularExpression(@"^[a-z0-9\.\-]+$", ErrorMessage = "El código solo puede contener letras minúsculas, números, puntos y guiones.")]
    public string Code { get; set; } = string.Empty;

    public Guid? ApplicationId { get; set; }

    public string? Description { get; set; }
}
