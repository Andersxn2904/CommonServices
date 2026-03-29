using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
