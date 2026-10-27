using PrivateNotes;

var builder = WebApplication.CreateBuilder(args);
LabConfiguration.Configure(builder);
var app = builder.Build();

if (await DatabaseInitializer.TryRunAsync(app.Services, args))
    return;

app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { message = "Błąd serwera laboratorium. Zobacz lokalny log." });
}));
app.UseMiddleware<LabRequestMiddleware>();

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
