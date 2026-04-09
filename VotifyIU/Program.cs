using System.Data.Common;
using VotifyIU.Client.Pages;
using VotifyIU.Components;
using Votify.BusinessLogic.Service;
using Votify.Persistence;
using Npgsql;

// Registrar el proveedor Npgsql para EF6 (necesario en .NET Core+)
DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

var builder = WebApplication.CreateBuilder(args);

// Razor + WASM
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Sesiones
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Servicios de dominio
builder.Services.AddScoped<VotifyDBContext>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connStr = config.GetConnectionString("VotifyDbConnection")
        ?? throw new InvalidOperationException("Connection string 'VotifyDbConnection' not found.");
    return new VotifyDBContext(connStr);
});
builder.Services.AddScoped<IDAL, EntityFrameworkDAL>();
builder.Services.AddScoped<IVotifyService, VotifyService>();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseSession();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(VotifyIU.Client._Imports).Assembly);

// ── API endpoints ──────────────────────────────────────────────

app.MapPost("/api/auth/login", (LoginRequest req, IVotifyService service, HttpContext http) =>
{
    try
    {
        service.LogIn(req.Username, req.Password);
        http.Session.SetString("username", req.Username);
        return Results.Ok();
    }
    catch (ServiceException)
    {
        return Results.Unauthorized();
    }
});

app.MapPost("/api/auth/logout", (HttpContext http) =>
{
    http.Session.Remove("username");
    return Results.Ok();
});

app.MapPost("/api/auth/register", (RegisterRequest req, IVotifyService service) =>
{
    try
    {
        service.Registrar(req.Username, req.Email, req.Password);
        return Results.Ok();
    }
    catch (ServiceException ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapPost("/api/votos/finalizar/{idEvento}", (int idEvento, HttpContext http) =>
{
    var username = http.Session.GetString("username");
    if (username == null) return Results.Unauthorized();
    http.Session.SetString($"voted_{username}_{idEvento}", "true");
    return Results.Ok();
});

app.MapGet("/api/votos/hasVotado/{idEvento}", (int idEvento, IVotifyService service, HttpContext http) =>
{
    var username = http.Session.GetString("username");
    if (username == null) return Results.Unauthorized();

    // Comprueba primero el flag de sesión (votos del mock)
    if (http.Session.GetString($"voted_{username}_{idEvento}") == "true")
        return Results.Ok(true);

    // Luego comprueba la base de datos (votos reales)
    try
    {
        service.RestoreSession(username);
        return Results.Ok(service.HasVotadoEnEvento(idEvento));
    }
    catch (ServiceException)
    {
        return Results.Ok(false);
    }
});

app.Run();

record LoginRequest(string Username, string Password);
record RegisterRequest(string Username, string Email, string Password);
