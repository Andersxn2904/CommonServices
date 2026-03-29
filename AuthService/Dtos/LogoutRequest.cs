using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class LogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
