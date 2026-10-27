using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace PrivateNotes;

public static class LabSeed
{
    // Harmless payload: only changes a visible local marker; no data exfiltration.
    public const string XssPayload = "<img src=\"/lab-xss-missing\" onerror=\"document.getElementById('lab-xss-status').textContent='XSS WYKONANY'\">";
    public static async Task RunAsync(IServiceProvider services)
    {
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        var db = services.GetRequiredService<NotesDb>();
        var created = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        foreach (var (id, email, password, index) in new[]
        {
            ("lab-anna", "anna@example.test", "Anna!Notes2026", 1),
            ("lab-piotr", "piotr@example.test", "Piotr!Notes2026", 2)
        })
        {
            if (await users.FindByIdAsync(id) is not null)
                continue;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var user = new IdentityUser { Id = id, UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            for (var n = 1; n <= 3; n++)
            {
                var title = n switch
                {
                    1 => "Prywatny plan — " + email,
                    2 => "Pomysły i 100% spokoju",
                    _ => index == 1 ? "LAB — zapisany XSS" : "Tylko dla Piotra"
                };
                var content = n switch
                {
                    1 => "Fikcyjny plan dnia: spacer, książka, herbata. Właściciel: " + email,
                    2 => "Polskie znaki: żółć. Wyszukiwanie dosłowne: 100% oraz _ i \\. Dane laboratoryjne.",
                    _ => index == 1 ? XssPayload : "PIOTR_PRIVATE_MARKER — fikcyjna treść do badania SQL Injection i IDOR."
                };
                db.Notes.Add(new Note
                {
                    Id = Guid.Parse($"{index}0000000-0000-0000-0000-00000000000{n}"),
                    OwnerId = id,
                    Title = title,
                    Content = content,
                    CreatedAt = created,
                    UpdatedAt = created.AddMinutes(n)
                });
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }
}
