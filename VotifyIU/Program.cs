using System.Data.Common;
using VotifyIU.Services;
using VotifyIU.Client.Pages;
using VotifyIU.Components;
using Votify.BusinessLogic.Service;
using System.Linq;
using Votify.Persistence;
using Npgsql;

// Npgsql 6+ rechaza DateTime sin Kind=Utc en columnas timestamptz — activar comportamiento legacy
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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
builder.Services.AddScoped<IEmailService, EmailService>();

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

// Endpoint para eliminar contenido multimedia de proyectos
app.MapDelete("/api/projects/{id}/content", (string id, IWebHostEnvironment env) =>
{
    var webRoot = env.WebRootPath ?? "wwwroot";
    var dir = System.IO.Path.Combine(webRoot, "imagenes", "projects");
    var dest = System.IO.Path.Combine(dir, id + ".png");

    try
    {
        if (!System.IO.File.Exists(dest))
            return Results.NotFound();

        System.IO.File.Delete(dest);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
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

app.MapPost("/api/auth/forgot-password", async (ForgotPasswordRequest req, IVotifyService service, IEmailService email, HttpRequest http) =>
{
    try
    {
        string token = service.GeneratePasswordResetToken(req.Email);
        string baseUrl = $"{http.Scheme}://{http.Host}";
        string resetLink = $"{baseUrl}/reset-password?token={token}";
        await email.SendPasswordResetAsync(req.Email, resetLink);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/auth/reset-password", (ResetPasswordRequest req, IVotifyService service, HttpContext context) =>
{
    try
    {
        service.ResetPassword(req.Token, req.NuevaPassword);
        context.Session.Clear(); // Cerrar sesión inmediatamente al restablecer contraseña
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapGet("/api/perfil", (IVotifyService service, HttpContext http) =>
{
    var username = http.Session.GetString("username");
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
    var username = http.Session.GetString("username");
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
    var username = http.Session.GetString("username");
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
    var username = http.Session.GetString("username");
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.UpdateFotoPerfil(req.Base64Foto);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

// Crear una nueva votación (requiere sesión de EncargadoVotacion)
app.MapPost("/api/votaciones", (CreateVotacionRequest req, IVotifyService service, HttpContext http) =>
{
    var username = http.Session.GetString("username");
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        int id = service.CrearVotacion(req.FechaFin, req.Estado, req.Titulo, req.Descripcion);
        return Results.Ok(id);
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

// Asignar un rol en un evento al usuario actual
app.MapPost("/api/roles/assign", (AssignRoleRequest req, IVotifyService service, HttpContext http) =>
{
    var username = http.Session.GetString("username");
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.AsignarRolEnEvento(req.TipoRol, req.IdEvento);
        return Results.Ok();
    }
    catch (ServiceException ex)
    {
        // Si el evento no existe en la base de datos (mock UI), asignamos el rol sin evento
        if (ex.Message?.Contains("evento no existe", StringComparison.OrdinalIgnoreCase) == true ||
            ex.Message?.Contains("El evento no existe", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                service.AsignarRolSinEvento(req.TipoRol);
                return Results.Ok();
            }
            catch (ServiceException inner) { return Results.BadRequest(inner.Message); }
        }

        return Results.BadRequest(ex.Message);
    }
});

// Endpoint para recibir contenido multimedia de proyectos (PNG) y guardarlo en wwwroot/imagenes/projects/{id}.png
app.MapPost("/api/projects/{id}/content", async (string id, HttpRequest request, IWebHostEnvironment env) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Expected multipart/form-data");

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file == null) return Results.BadRequest("No file provided");

    if (!string.Equals(file.ContentType, "image/png", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest("Only PNG files are accepted");

    var webRoot = env.WebRootPath ?? "wwwroot";
    var dir = System.IO.Path.Combine(webRoot, "imagenes", "projects");
    System.IO.Directory.CreateDirectory(dir);
    var dest = System.IO.Path.Combine(dir, id + ".png");

    try
    {
        await using var fs = new System.IO.FileStream(dest, System.IO.FileMode.Create);
        await file.CopyToAsync(fs);
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.Run();

record LoginRequest(string Username, string Password);
record RegisterRequest(string Username, string Email, string Password);
record ForgotPasswordRequest(string Email);
record ResetPasswordRequest(string Token, string NuevaPassword);
record UpdateEmailRequest(string NuevoEmail);
record UpdatePasswordRequest(string PasswordActual, string NuevaPassword);
record UpdateFotoRequest(string Base64Foto);
record CreateVotacionRequest(DateTime FechaFin, bool Estado, string Titulo, string Descripcion);
record AssignRoleRequest(string TipoRol, int IdEvento);
