using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class UpdateUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(2)]
    public string DisplayName { get; set; } = string.Empty;
}
