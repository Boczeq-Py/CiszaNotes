namespace PrivateNotes;

// OwnerId stays on the server; the HTTP response has an explicit, stable shape.
public record NoteResponse(Guid Id, string Title, string Content, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static NoteResponse From(Note note) =>
        new(note.Id, note.Title, note.Content, note.CreatedAt, note.UpdatedAt);
}
