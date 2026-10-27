using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PrivateNotes;

[ApiController, Authorize, Route("api/notes")]
public sealed class NotesController(NotesDb db) : ControllerBase
{
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Note> Mine => db.Notes.Where(x => x.OwnerId == Owner);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, StringLength(200)] string? q)
    {
        var query = Mine.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = "%" + q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            // FormattableString: interpolated VALUES become DB parameters, not SQL text.
            // PostgreSQL's default LIKE escape character is backslash.
            query = db.Notes.FromSql($"SELECT * FROM \"Notes\" WHERE \"OwnerId\" = {Owner} AND (\"Title\" ILIKE {pattern} OR \"Content\" ILIKE {pattern})").AsNoTracking();
        }
        var notes = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).ToListAsync();
        return Ok(notes.Select(NoteResponse.From));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var note = await Mine.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return note is null ? NotFound() : Ok(NoteResponse.From(note));
    }

    [HttpPost]
    public async Task<IActionResult> Create(NoteInput input)
    {
        var note = new Note { OwnerId = Owner, Title = input.Title.Trim(), Content = input.Content };
        db.Notes.Add(note);
        await db.SaveChangesAsync();
        return Created($"/api/notes/{note.Id}", NoteResponse.From(note));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, NoteInput input)
    {
        var note = await Mine.FirstOrDefaultAsync(x => x.Id == id);
        if (note is null) return NotFound();
        note.Title = input.Title.Trim();
        note.Content = input.Content;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(NoteResponse.From(note));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var note = await Mine.FirstOrDefaultAsync(x => x.Id == id);
        if (note is null) return NotFound();
        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
