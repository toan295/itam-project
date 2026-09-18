using System.ComponentModel.DataAnnotations;

namespace ITAM.API.Models.DTOs;

public class ForgotPasswordRequestDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = null!;
}
