using System.ComponentModel.DataAnnotations;

namespace PrivateNotes;

public record LoginInput(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(256)] string Password);
