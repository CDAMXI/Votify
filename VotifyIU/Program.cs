using System.Data.Common;
using VotifyIU.Services;
using VotifyIU.Client.Pages;
using VotifyIU.Components;
using Votify.BusinessLogic.Service;
using Votify.Persistence;
using Npgsql;

// Npgsql 6+ rechaza DateTime sin Kind=Utc en columnas timestamptz
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(SessionConfig.TimeoutHours);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddScoped<VotifyDBContext>(sp =>
{
    var connStr = sp.GetRequiredService<IConfiguration>()
        .GetConnectionString("VotifyDbConnection")
        ?? throw new InvalidOperationException("Connection string 'VotifyDbConnection' not found.");
    return new VotifyDBContext(connStr);
});
builder.Services.AddScoped<IDAL, EntityFrameworkDAL>();
builder.Services.AddScoped<IVotifyService, VotifyService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseWebAssemblyDebugging();
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

// ── Endpoints de autenticación ──────────────────────────────────

app.MapPost("/api/auth/login", (LoginRequest req, IVotifyService service, HttpContext http) =>
{
    try
    {
        service.LogIn(req.Username, req.Password);
        http.Session.SetString(SessionConfig.UsernameKey, req.Username);
        return Results.Ok();
    }
    catch (ServiceException)
    {
        return Results.Unauthorized();
    }
});

app.MapPost("/api/auth/logout", (HttpContext http) =>
{
    http.Session.Remove(SessionConfig.UsernameKey);
    return Results.Ok();
});

app.MapPost("/api/auth/register", (RegisterRequest req, IVotifyService service) =>
{
    try
    {
        service.Registrar(req.Username, req.Email, req.Password);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/auth/forgot-password", async (ForgotPasswordRequest req, IVotifyService service, IEmailService emailService, HttpRequest http) =>
{
    try
    {
        string token = service.GeneratePasswordResetToken(req.Email);
        string resetLink = $"{http.Scheme}://{http.Host}/reset-password?token={token}";
        await emailService.SendPasswordResetAsync(req.Email, resetLink);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/auth/reset-password", (ResetPasswordRequest req, IVotifyService service, HttpContext http) =>
{
    try
    {
        service.ResetPassword(req.Token, req.NuevaPassword);
        http.Session.Clear();
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

// ── Endpoints de votos ──────────────────────────────────────────

app.MapPost("/api/votos/finalizar/{idEvento}", (int idEvento, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    http.Session.SetString(SessionConfig.VoteFlagKey(username, idEvento), "true");
    return Results.Ok();
});

app.MapGet("/api/votos/hasVotado/{idEvento}", (int idEvento, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    if (http.Session.GetString(SessionConfig.VoteFlagKey(username, idEvento)) == "true")
        return Results.Ok(true);

    try
    {
        service.RestoreSession(username);
        return Results.Ok(service.HasVotadoEnEvento(idEvento));
    }
    catch (ServiceException) { return Results.Ok(false); }
});

// ── Endpoints de perfil ─────────────────────────────────────────

app.MapGet("/api/perfil", (IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var (user, email, foto) = service.GetPerfil();
        return Results.Ok(new { Username = user, Email = email, FotoPerfil = foto });
    }
    catch (ServiceException) { return Results.Unauthorized(); }
});

app.MapPut("/api/perfil/email", (UpdateEmailRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.UpdateEmail(req.NuevoEmail);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPut("/api/perfil/password", (UpdatePasswordRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.UpdatePassword(req.PasswordActual, req.NuevaPassword);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPut("/api/perfil/foto", (UpdateFotoRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.UpdateFotoPerfil(req.Base64Foto);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.Run();

// ── Helpers ─────────────────────────────────────────────────────

static string? ObtenerUsernameAutenticado(HttpContext http)
    => http.Session.GetString(SessionConfig.UsernameKey);

// ── Configuración de sesión ──────────────────────────────────────

static class SessionConfig
{
    public const string UsernameKey = "username";
    public const int TimeoutHours = 2;
    public static string VoteFlagKey(string username, int idEvento) => $"voted_{username}_{idEvento}";
}

// ── DTOs de request ──────────────────────────────────────────────

record LoginRequest(string Username, string Password);
record RegisterRequest(string Username, string Email, string Password);
record ForgotPasswordRequest(string Email);
record ResetPasswordRequest(string Token, string NuevaPassword);
record UpdateEmailRequest(string NuevoEmail);
record UpdatePasswordRequest(string PasswordActual, string NuevaPassword);
record UpdateFotoRequest(string Base64Foto);
