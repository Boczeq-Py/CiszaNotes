using System.ComponentModel.DataAnnotations;

namespace PrivateNotes;

public record NoteInput(
    [Required, StringLength(160)] string Title,
    [Required(AllowEmptyStrings = true), StringLength(20000)] string Content);
