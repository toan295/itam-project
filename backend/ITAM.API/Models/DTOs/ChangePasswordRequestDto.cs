using System.ComponentModel.DataAnnotations;

namespace ITAM.API.Models.DTOs;

public class ChangePasswordRequestDto
{
    [Required]
    public string CurrentPassword { get; set; } = null!;

    [Required, MinLength(6), MaxLength(100)]
    public string NewPassword { get; set; } = null!;
}
