using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PrivateNotes;

public static class LabConfiguration
{
    public static void Configure(WebApplicationBuilder builder)
    {
        // Hard-coded loopback listeners: --urls cannot expose this laboratory on a LAN.
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.Listen(IPAddress.Loopback, 5182);
            o.Listen(IPAddress.Loopback, 7182, l => l.UseHttps());
        });
        var connection = builder.Configuration.GetConnectionString("Notes")!;
        var database = new NpgsqlConnectionStringBuilder(connection);
        if (database.Host != "127.0.0.1" || database.Port != 55442 || database.Database != "notes_lab_secured")
            throw new InvalidOperationException("Laboratorium wymaga własnej lokalnej bazy notes_lab_secured na porcie 55442.");
        builder.Services.AddDbContext<NotesDb>(o => o.UseNpgsql(connection));
        builder.Services.AddDataProtection().SetApplicationName("NotesLab.Secured");
        builder.Services.AddIdentity<IdentityUser, IdentityRole>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        }).AddEntityFrameworkStores<NotesDb>();
        builder.Services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "__Host-NotesLab.Secured";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.ExpireTimeSpan = TimeSpan.FromHours(1);
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        builder.Services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-CSRF-TOKEN";
            o.Cookie.Name = "__Host-NotesLab.Secured.Csrf";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Strict;
        });
        builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
        builder.Services.AddHttpsRedirection(o =>
        {
            o.HttpsPort = 7182;
            o.RedirectStatusCode = 307;
        });
    }
}
