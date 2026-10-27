using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PrivateNotes;

public static class LabConfiguration
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // Hard-coded loopback listeners: --urls cannot expose this laboratory on a LAN.
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 5181));
        var connection = builder.Configuration.GetConnectionString("Notes")!;
        var database = new NpgsqlConnectionStringBuilder(connection);
        if (database.Host != "127.0.0.1" || database.Port != 55441 || database.Database != "notes_lab_vulnerable")
            throw new InvalidOperationException("Laboratorium wymaga własnej lokalnej bazy notes_lab_vulnerable na porcie 55441.");
        builder.Services.AddDbContext<NotesDb>(o => o.UseNpgsql(connection));
        builder.Services.AddDataProtection().SetApplicationName("NotesLab.Vulnerable");
        builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        }).AddEntityFrameworkStores<NotesDb>();
        builder.Services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "NotesLab.Vulnerable.Session";
            o.Cookie.HttpOnly = false;
            o.Cookie.SecurePolicy = CookieSecurePolicy.None;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.ExpireTimeSpan = TimeSpan.FromHours(1);
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        // LAB VULNERABILITY: no CSRF validation. Lax is still a partial browser mitigation.
        builder.Services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-CSRF-TOKEN";
            o.Cookie.Name = "NotesLab.Vulnerable.Csrf";
        });
        builder.Services.AddControllers();
    }
}
