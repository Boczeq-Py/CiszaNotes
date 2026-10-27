using Microsoft.EntityFrameworkCore;

namespace PrivateNotes;

public static class DatabaseInitializer
{
    public static async Task<bool> TryRunAsync(IServiceProvider services, string[] args)
    {
        var reset = args.Contains("--reset-lab-data");
        if (!reset && !args.Contains("--init-db"))
            return false;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotesDb>();

        // LabConfiguration validates the exact database, host and port before this code.
        if (reset)
            await db.Database.EnsureDeletedAsync();

        await db.Database.MigrateAsync();
        await LabSeed.RunAsync(scope.ServiceProvider);
        return true;
    }
}
