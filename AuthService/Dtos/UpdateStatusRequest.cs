using System.ComponentModel.DataAnnotations;

namespace AuthService.Dtos;

public class UpdateStatusRequest
{
    [Required]
    public bool IsActive { get; set; }
}
