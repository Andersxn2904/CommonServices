using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class VerifyEmailRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
