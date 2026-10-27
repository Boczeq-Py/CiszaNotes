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
            // LAB ONLY: concatenated input changes SQL structure. Never copy to production.
            var sql = "SELECT * FROM \"Notes\" WHERE \"OwnerId\" = '" + Owner
                + "' AND (\"Title\" ILIKE '%" + q + "%' OR \"Content\" ILIKE '%" + q + "%')";
            var unsafeNotes = await db.Notes.FromSqlRaw(sql).AsNoTracking().ToListAsync();
            return Ok(unsafeNotes.OrderByDescending(x => x.UpdatedAt).Select(NoteResponse.From));
        }
        var notes = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).ToListAsync();
        return Ok(notes.Select(NoteResponse.From));
    }

    [HttpGet("{id:guid}")]
    // LAB ONLY: IDOR. Authentication alone is not an ownership check.
    public async Task<IActionResult> Get(Guid id)
    {
        var note = await db.Notes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
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
        var note = await db.Notes.FirstOrDefaultAsync(x => x.Id == id); // LAB: missing owner check
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
        var note = await db.Notes.FirstOrDefaultAsync(x => x.Id == id); // LAB: missing owner check
        if (note is null) return NotFound();
        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
