using System.ComponentModel.DataAnnotations;

namespace DocQA.Models
{
    public sealed class RegisterRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public required string Email { get; set; }
        [Required, StringLength(100, MinimumLength = 8)]
        public required string Password { get; set; }
    }

    public sealed class LoginRequest
    {
        [Required]
        public required string Email { get; set; }
        [Required]
        public required string Password { get; set; }
    }

    public sealed record AuthResponse(string Token, DateTime ExpiresAt, string Email);
}
