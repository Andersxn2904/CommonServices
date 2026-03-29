using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
