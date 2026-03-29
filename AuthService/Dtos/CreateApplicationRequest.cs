using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class CreateApplicationRequest
{
    [Required, RegularExpression(@"^[a-z0-9\-]+$", ErrorMessage = "El código solo puede contener letras minúsculas, números y guiones.")]
    public string Code { get; set; } = string.Empty;

    [Required, MinLength(2)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
