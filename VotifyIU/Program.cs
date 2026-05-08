using System.Data.Common;
using Votify.Entities;
using Votify.shared;
using VotifyIU.Services;
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

// Registro del Patrón Repositorio Genérico
builder.Services.AddScoped(typeof(IDAL<>), typeof(EntityFrameworkDAL<>));
builder.Services.AddScoped<VotifyRepositories>();
builder.Services.AddScoped<IVotifyService, VotifyService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpClient();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<VotifyDBContext>();
    dbContext.EnsureAdministrativeSettingsSchema();
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();

    // Evita que el navegador sirva versiones cacheadas del bundle WASM
    // y del manifiesto de arranque tras un rebuild. Solo en desarrollo.
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.Contains("/_framework/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("blazor.boot.json", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
        }
        await next();
    });
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

app.MapGet("/api/votos/misVotos/{idVotacion}", (int idVotacion, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Ok(new List<int>());

    try
    {
        service.RestoreSession(username);
        return Results.Ok(service.GetMisVotos(idVotacion));
    }
    catch
    {
        return Results.Ok(new List<int>());
    }
});

app.MapGet("/api/proyectos/{idVotacion}/mis-comentarios", (int idVotacion, IDAL<Votacion> votacionRepo, IDAL<Voto> votoRepo, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        var votacion = votacionRepo.GetById(idVotacion);
        if (votacion == null) return Results.NotFound("Votación no encontrada");

        var proyecto = votacion.evento?.proyectos?
            .FirstOrDefault(p => p.competidor?.usuario?.Username == username);

        if (proyecto == null)
            return Results.Ok(new List<string>());

        var comentarios = votoRepo.GetWhere(v =>
                v.VotacionId == idVotacion &&
                v.ProyectoId == proyecto.Id &&
                !string.IsNullOrWhiteSpace(v.Comentario))
            .OrderByDescending(v => v.Fecha)
            .Select(v => v.Comentario.Trim())
            .ToList();

        return Results.Ok(comentarios);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
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

app.MapGet("/api/perfil/historial", (IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var resultado = service.GetHistorialDelUsuario();
        var reclamaciones = service.GetReclamacionesDelUsuario()
            .GroupBy(r => r.EventoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.FechaCreacion).First());

        var dto = new HistorialEventosDTO
        {
            EventosParticipados = resultado.EventosParticipados,
            VotosEmitidos = resultado.VotosEmitidos,
            Eventos = resultado.Eventos.Select(item => new EventoHistorialItemDTO
            {
                IdEvento = item.Evento.IdEvento,
                Nombre = item.Evento.Nombre ?? $"Evento #{item.Evento.IdEvento}",
                FechaIni = item.Evento.FechaIni,
                FechaFin = item.Evento.FechaFin,
                RolUsuario = item.TipoRol,
                Voto = item.Voto,
                ProyectoDestacado = item.ProyectoDestacado?.Nombre,
                PosicionProyecto = item.PosicionProyecto,
                TotalProyectos = item.TotalProyectos,
                YaReclamado = reclamaciones.ContainsKey(item.Evento.IdEvento),
                EstadoReclamacion = reclamaciones.TryGetValue(item.Evento.IdEvento, out var r) ? r.Estado : null
            }).ToList()
        };
        return Results.Ok(dto);
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

// ── Endpoints de reclamaciones ──────────────────────────────────

app.MapPost("/api/reclamaciones", (CrearReclamacionRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var reclamacion = service.CrearReclamacion(req.EventoId, req.Descripcion);
        return Results.Ok(MapReclamacion(reclamacion, username));
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

app.MapGet("/api/reclamaciones/mias", (IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var reclamaciones = service.GetReclamacionesDelUsuario()
            .Select(r => MapReclamacion(r, username))
            .ToList();
        return Results.Ok(reclamaciones);
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

app.MapGet("/api/reclamaciones/organizador", (IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var reclamaciones = service.GetReclamacionesComoOrganizador()
            .Select(r => MapReclamacion(r, r.usuario?.Username ?? $"Usuario #{r.UsuarioId}"))
            .ToList();
        return Results.Ok(reclamaciones);
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

app.MapPut("/api/reclamaciones/{id}/responder", (int id, ResponderReclamacionRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var reclamacion = service.ResponderReclamacion(id, req.Estado, req.Respuesta);
        return Results.Ok(MapReclamacion(reclamacion, reclamacion.usuario?.Username ?? $"Usuario #{reclamacion.UsuarioId}"));
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

// ── Endpoints de votaciones ─────────────────────────────────────

app.MapPost("/api/votaciones", (VotacionDTO req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        int idVotacion = service.CrearVotacion(new CrearVotacionRequest
        {
            Titulo = req.Titulo,
            Descripcion = req.Descripcion,
            FechaFin = req.FechaFin,
            Activa = true,
            PermiteCompetidoresVotar = req.PermiteCompetidoresVotar,
            PesoJurado = req.PesoJurado,
            PesoPublico = req.PesoPublico,
            Categorias = req.Categorias?.Select(c => c.Nombre).ToList() ?? new List<string>()
        });
        return Results.Ok(idVotacion);
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapGet("/api/votaciones", (IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var usuarioActual = service.GetUsuarioActual();

        // Carga todos los roles del usuario en un único query en lugar de 5 queries por votación
        var rolesPorEvento = usuarioActual.roles?
            .GroupBy(r => r.EventoId)
            .ToDictionary(g => g.Key, g => g.First().TipoRol)
            ?? new Dictionary<int, string?>();

        var votaciones = service.GetAllVotaciones().OrderBy(v => v.FechaFin).Select(v => new VotacionDTO
        {
            Id = v.Id,
            IdEvento = v.EventoId,
            NombreEvento = v.evento?.Nombre ?? string.Empty,
            Descripcion = v.Descripcion,
            Titulo = string.IsNullOrEmpty(v.Titulo) ? $"Votación #{v.Id}" : v.Titulo,
            FechaIni = v.FechaIni,
            FechaFin = v.FechaFin,
            Estado = v.Estado,
            PesoJurado = v.PesoJurado,
            PesoPublico = v.PesoPublico,
            RolActual = rolesPorEvento.TryGetValue(v.EventoId, out var rol) ? rol : null
        }).ToList();

        return Results.Ok(votaciones);
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapGet("/api/eventos/{idEvento}/votaciones", (int idEvento, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        var usuarioActual = service.GetUsuarioActual();

        var rolesPorEvento2 = usuarioActual.roles?
            .GroupBy(r => r.EventoId)
            .ToDictionary(g => g.Key, g => g.First().TipoRol)
            ?? new Dictionary<int, string?>();

        var votaciones = service.GetVotacionesByEvento(idEvento)
            .OrderBy(v => v.FechaFin)
            .Select(v => new VotacionDTO
            {
                Id = v.Id,
                IdEvento = v.EventoId,
                NombreEvento = v.evento?.Nombre ?? string.Empty,
                Titulo = string.IsNullOrEmpty(v.Titulo) ? $"Votación #{v.Id}" : v.Titulo,
                Descripcion = v.Descripcion,
                FechaIni = v.FechaIni,
                FechaFin = v.FechaFin,
                Estado = v.Estado,
                PesoJurado = v.PesoJurado,
                PesoPublico = v.PesoPublico,
                RolActual = rolesPorEvento2.TryGetValue(v.EventoId, out var rol2) ? rol2 : null
            })
            .ToList();

        return Results.Ok(votaciones);
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapGet("/api/votaciones/{id}", (int id, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var votacion = service.GetVotacion(id);
        var usuarioActual = service.GetUsuarioActual();
        return Results.Ok(new VotacionDTO
        {
            Id = votacion.Id,
            IdEvento = votacion.EventoId,
            NombreEvento = votacion.evento?.Nombre ?? string.Empty,
            Titulo = votacion.Titulo,
            Descripcion = votacion.Descripcion,
            FechaIni = votacion.FechaIni,
            FechaFin = votacion.FechaFin,
            Estado = votacion.Estado,
            PesoJurado = votacion.PesoJurado,
            PesoPublico = votacion.PesoPublico,
            RolActual = usuarioActual == null ? null : service.GetTipoRolDeUsuario(usuarioActual.Id, votacion.EventoId)
        });
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});
app.MapPost("/api/votaciones/{id}/pausar", (int id, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.TogglePausarVotacion(id);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapGet("/api/eventos/{idEvento}/rol", (int idEvento, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var rol = service.GetTipoRolEnEvento(idEvento);
        return Results.Ok(new RolEventoResponse(idEvento, rol));
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/eventos/{idEvento}/rol", (int idEvento, AsignarRolEventoRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        service.AsignarRolEnEvento(req.TipoRol, idEvento);
        return Results.Ok(new RolEventoResponse(idEvento, req.TipoRol.Trim().ToUpperInvariant()));
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapDelete("/api/votaciones/{id}", (int id, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.EliminarEvento(id);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapPut("/api/votaciones/{id}", (int id, VotacionDTO req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.ModificarVotacion(id, req.FechaFin, req.Estado);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapPost("/api/votaciones/{id}/cerrar", (int id, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.CerrarVotacion(id);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

// ── Endpoint guardar voto ────────────────────────────────────────

app.MapPost("/api/votos/guardar", (GuardarVotoRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.GuardarVoto(req.VotacionId, req.ProyectoId, req.Puntuacion, req.Comentario);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

// ── Endpoint de proyectos ───────────────────────────────────────

app.MapPost("/api/proyectos/{idVotacion}", (int idVotacion, CrearProyectoRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        var proyecto = service.CrearProyecto(idVotacion, req.Nombre, req.Descripcion, req.UsernameCompetidor);

        return Results.Ok(new ProyectoResultadoDTO
        {
            Id = proyecto.Id,
            Nombre = proyecto.Nombre,
            Descripcion = proyecto.Descripcion,
            Competidor = proyecto.competidor?.usuario?.Username ?? req.UsernameCompetidor,
            Media = 0,
            NumVotos = 0,
            Rank = 0
        });
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapPut("/api/proyectos/{idVotacion}/{idProyecto}", (int idVotacion, int idProyecto, ModificarProyectoRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.ModificarProyecto(idProyecto, req.Nombre, req.Descripcion, req.ParticipantesAdicionales);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapDelete("/api/proyectos/{idVotacion}/{idProyecto}", (int idVotacion, int idProyecto, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        service.EliminarProyecto(idProyecto);
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

// ── Endpoint de resultados reales ───────────────────────────────

app.MapGet("/api/votaciones/{id}/configuracion-resultados", (
    int id,
    IVotifyService service,
    IDAL<Votacion> votacionRepo,
    IDAL<Organizador> organizadorRepo,
    IDAL<EncargadoVotacion> encargadoRepo,
    HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        var usuarioActual = service.GetUsuarioActual();
        var votacion = votacionRepo.GetById(id);
        if (votacion == null) return Results.NotFound("Votación no encontrada");
        if (usuarioActual == null || !UsuarioPuedeGestionarResultados(votacion, usuarioActual, organizadorRepo, encargadoRepo))
            return Results.Text("No tienes permisos para gestionar esta votación.", statusCode: StatusCodes.Status403Forbidden);

        return Results.Ok(new ConfiguracionResultadosDTO
        {
            PesoJurado = votacion.PesoJurado,
            PesoPublico = votacion.PesoPublico
        });
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapPut("/api/votaciones/{id}/configuracion-resultados", (
    int id,
    ConfiguracionResultadosDTO req,
    IVotifyService service,
    IDAL<Votacion> votacionRepo,
    IDAL<Organizador> organizadorRepo,
    IDAL<EncargadoVotacion> encargadoRepo,
    HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        var usuarioActual = service.GetUsuarioActual();
        var votacion = votacionRepo.GetById(id);
        if (votacion == null) return Results.NotFound("Votación no encontrada");
        if (usuarioActual == null || !UsuarioPuedeGestionarResultados(votacion, usuarioActual, organizadorRepo, encargadoRepo))
            return Results.Text("No tienes permisos para gestionar esta votación.", statusCode: StatusCodes.Status403Forbidden);

        string? errorPesos = ValidarPesosResultados(req.PesoJurado, req.PesoPublico);
        if (errorPesos != null)
            return Results.BadRequest(errorPesos);

        votacion.PesoJurado = req.PesoJurado;
        votacion.PesoPublico = req.PesoPublico;
        votacionRepo.Commit();

        return Results.Ok(new ConfiguracionResultadosDTO
        {
            PesoJurado = votacion.PesoJurado,
            PesoPublico = votacion.PesoPublico
        });
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapGet("/api/resultados/{idVotacion}", (int idVotacion, IDAL<Votacion> votacionRepo, IDAL<Voto> votoRepo, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        var votacion = votacionRepo.GetById(idVotacion);
        if (votacion == null) return Results.NotFound("Votación no encontrada");

        var evento = votacion.evento;
        if (evento == null) return Results.NotFound("Evento no encontrado");

        var proyectos = evento.proyectos?.ToList() ?? new List<Proyecto>();
        var votos = votoRepo.GetWhere(v => v.VotacionId == idVotacion).ToList();
        var votosPorProyecto = votos.GroupBy(v => v.ProyectoId).ToDictionary(g => g.Key, g => g.ToList());

        var resultados = proyectos
            .Select(p => BuildProjectResult(
                p,
                votosPorProyecto.TryGetValue(p.Id, out var votosProyecto) ? votosProyecto : new List<Voto>(),
                votacion))
            .OrderByDescending(r => r.Media)
        .Select((r, i) => { r.Rank = i + 1; return r; })
        .ToList();

        return Results.Ok(resultados);
    }
    catch (Exception ex)
    {
        return Results.Problem(ObtenerMensajeErrorDetallado(ex));
    }
});

// ── Endpoint de monitoreo ───────────────────────────────────────

app.MapGet("/api/monitor/{idVotacion}", (int idVotacion, IDAL<Votacion> votacionRepo, IDAL<Voto> votoRepo, IDAL<Jurado> juradoRepo, IDAL<Publico> publicoRepo, IDAL<Competidor> competidorRepo, IDAL<Usuario> usuarioRepo, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        var votacion = votacionRepo.GetById(idVotacion);
        if (votacion == null) return Results.NotFound("Votación no encontrada");

        var votos = votoRepo.GetWhere(v => v.VotacionId == idVotacion).ToList();
        var votanteIds = votos.Select(v => v.VotanteId).Distinct().ToHashSet();
        var evento = votacion.evento;

        var rolesVotantes = juradoRepo.GetWhere(r => votanteIds.Contains(r.Id)).Cast<Rol>()
            .Concat(publicoRepo.GetWhere(r => votanteIds.Contains(r.Id)).Cast<Rol>())
            .Concat(competidorRepo.GetWhere(r => votanteIds.Contains(r.Id)).Cast<Rol>())
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .ToList();
        var userIds = rolesVotantes.Select(r => r.UsuarioId).Distinct().ToHashSet();
        var usuarios = usuarioRepo.GetAll().ToList()
            .Where(u => userIds.Contains(u.Id))
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();

        var usernamePorRolId = rolesVotantes.ToDictionary(
            r => r.Id,
            r => usuarios.FirstOrDefault(u => u.Id == r.UsuarioId)?.Username ?? $"Votante #{r.Id}"
        );

        var todosVotantes = new List<VotanteEstadoDTO>();
        try
        {
            if (votacion.EventoId > 0)
            {
                var tiposPermitidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "JURADO",
                    "PUBLICO"
                };

                if (evento?.PermiteCompetidoresVotar == true)
                    tiposPermitidos.Add("COMPETIDOR");

                var rolesEsperados = juradoRepo.GetWhere(r => r.EventoId == votacion.EventoId).Cast<Rol>()
                    .Concat(publicoRepo.GetWhere(r => r.EventoId == votacion.EventoId).Cast<Rol>())
                    .Concat(evento?.PermiteCompetidoresVotar == true
                        ? competidorRepo.GetWhere(r => r.EventoId == votacion.EventoId).Cast<Rol>()
                        : Enumerable.Empty<Rol>())
                    .Where(r => tiposPermitidos.Contains(r.TipoRol ?? string.Empty))
                    .GroupBy(r => r.Id)
                    .Select(g => g.First())
                    .OrderBy(r => r.TipoRol)
                    .ThenBy(r => r.Id)
                    .ToList();

                var expectedUserIds = rolesEsperados.Select(r => r.UsuarioId).Distinct().ToHashSet();
                var usuariosEsperados = usuarioRepo.GetAll().ToList()
                    .Where(u => expectedUserIds.Contains(u.Id))
                    .GroupBy(u => u.Id)
                    .Select(g => g.First())
                    .ToDictionary(u => u.Id, u => u.Username ?? $"Usuario #{u.Id}");

                foreach (var rol in rolesEsperados)
                {
                    todosVotantes.Add(new VotanteEstadoDTO
                    {
                        Nombre = usuariosEsperados.TryGetValue(rol.UsuarioId, out var nombre)
                            ? nombre
                            : $"Usuario #{rol.UsuarioId}",
                        Tipo = ObtenerEtiquetaRolMonitor(rol.TipoRol),
                        HaVotado = votanteIds.Contains(rol.Id)
                    });
                }
            }
            else
            {
                var jurados = votacion.jurados?.ToList() ?? new();
            var publicos = votacion.publicos?.ToList() ?? new();
            var competidores = evento?.PermiteCompetidoresVotar == true
                ? evento.roles?.OfType<Competidor>().ToList() ?? new List<Competidor>()
                : new List<Competidor>();

            var allRolIds = jurados.Select(j => j.Id)
                .Concat(publicos.Select(p => p.Id))
                .Concat(competidores.Select(c => c.Id))
                .ToHashSet();
            var allRoles = juradoRepo.GetWhere(r => allRolIds.Contains(r.Id)).Cast<Rol>()
                .Concat(publicoRepo.GetWhere(r => allRolIds.Contains(r.Id)).Cast<Rol>())
                .Concat(competidorRepo.GetWhere(r => allRolIds.Contains(r.Id)).Cast<Rol>())
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .ToList();
            var allUserIds = allRoles.Select(r => r.UsuarioId).Distinct().ToHashSet();
            var allUsers = usuarioRepo.GetAll().ToList().Where(u => allUserIds.Contains(u.Id)).ToList();
            var nameMap = allRoles.ToDictionary(r => r.Id,
                r => allUsers.FirstOrDefault(u => u.Id == r.UsuarioId)?.Username ?? $"#{r.Id}");

            foreach (var j in jurados)
                todosVotantes.Add(new VotanteEstadoDTO
                {
                    Nombre = nameMap.TryGetValue(j.Id, out var n) ? n : $"Jurado #{j.Id}",
                    Tipo = "Jurado",
                    HaVotado = votanteIds.Contains(j.Id)
                });

            foreach (var p in publicos)
                todosVotantes.Add(new VotanteEstadoDTO
                {
                    Nombre = nameMap.TryGetValue(p.Id, out var n) ? n : $"Público #{p.Id}",
                    Tipo = "Público",
                    HaVotado = votanteIds.Contains(p.Id)
                });
            }
        }
        catch { /* Ignorado por seguridad de la relación */ }

        var proyectos = evento?.proyectos?.ToList() ?? new List<Proyecto>();
        var votosPorProyecto = votos.GroupBy(v => v.ProyectoId).ToDictionary(g => g.Key, g => g.ToList());
        int totalVotantesEsperados = todosVotantes.Count;

        var proyectoStats = proyectos
            .Select(p => BuildProjectMonitor(
                p,
                votosPorProyecto.TryGetValue(p.Id, out var votosProyecto) ? votosProyecto : new List<Voto>(),
                votacion,
                totalVotantesEsperados))
            .OrderByDescending(p => p.Media)
            .ToList();

        var historial = votos
            .OrderByDescending(v => v.Fecha)
            .Select(v => new VotoHistorialDTO
            {
                Votante = usernamePorRolId.TryGetValue(v.VotanteId, out var name) ? name : $"Votante #{v.VotanteId}",
                Proyecto = v.proyecto?.Nombre ?? $"Proyecto #{v.ProyectoId}",
                Valor = v.Valor,
                Comentario = string.IsNullOrWhiteSpace(v.Comentario) ? null : v.Comentario,
                Fecha = v.Fecha
            })
            .ToList();

        var dto = new MonitorVotacionDTO
        {
            VotosEmitidos = votos.Count,
            TotalVotantes = todosVotantes.Count,
            PromedioGeneral = proyectoStats.Any(p => p.NumVotos > 0)
                ? Math.Round(proyectoStats.Where(p => p.NumVotos > 0).Average(p => p.Media), 2)
                : 0,
            PesoJurado = votacion.PesoJurado,
            PesoPublico = votacion.PesoPublico,
            Proyectos = proyectoStats,
            Votantes = todosVotantes,
            Historial = historial
        };

        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        return Results.Problem(ObtenerMensajeErrorDetallado(ex));
    }
});

// ── Endpoint de IA ──────────────────────────────────────────────

app.MapPost("/api/ai/chat", async (AiChatRequest req, IConfiguration config, IHttpClientFactory httpFactory, IVotifyService service, HttpContext http) =>
{
    var apiKey = config["GeminiApiKey"] ?? "";
    if (string.IsNullOrEmpty(apiKey)) return Results.Problem("API key no configurada.");

    var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent?key={apiKey}";

    var eventosContexto = "";
    var username = ObtenerUsernameAutenticado(http);
    if (username != null)
    {
        try
        {
            service.RestoreSession(username);
            var votaciones = service.GetMisVotaciones().ToList();
            if (votaciones.Any())
            {
                var lineas = votaciones.Select(v =>
                    $"- \"{v.Titulo ?? $"Votación #{v.Id}"}\" " +
                    $"(ID: {v.Id}, " +
                    $"del {v.FechaIni:dd/MM/yyyy} al {v.FechaFin:dd/MM/yyyy}, " +
                    $"estado: {(v.FechaFin >= DateTime.Today ? "activo" : "finalizado")})");
                eventosContexto = $"\n\nEventos actuales del usuario:\n{string.Join("\n", lineas)}";
            }
            else
            {
                eventosContexto = "\n\nEl usuario no tiene eventos creados actualmente.";
            }
        }
        catch { }
    }

    var systemPrompt =
        "Eres el asistente de Votify, una plataforma para gestionar votaciones en hackathones, ferias de innovación y concursos. " +
        "Ayuda a los usuarios con dudas sobre cómo votar, crear eventos, gestionar proyectos y usar el sistema. " +
        "Responde siempre en español, de forma concisa y útil. Si no sabes algo, dilo claramente." +
        eventosContexto;

    var contents = new List<object>();
    foreach (var turn in req.History)
        contents.Add(new { role = turn.Role, parts = new[] { new { text = turn.Content } } });
    contents.Add(new { role = "user", parts = new[] { new { text = req.Message } } });

    var body = new
    {
        system_instruction = new { parts = new[] { new { text = systemPrompt } } },
        contents
    };

    var client = httpFactory.CreateClient();
    var response = await client.PostAsJsonAsync(geminiUrl, body);

    if (!response.IsSuccessStatusCode)
    {
        string userMessage = (int)response.StatusCode switch
        {
            503 => "El asistente está muy ocupado ahora mismo. Espera unos segundos e inténtalo de nuevo.",
            429 => "Se han enviado demasiadas solicitudes. Espera un momento antes de continuar.",
            401 or 403 => "Error de autenticación con el servicio de IA.",
            _ => "El asistente no está disponible en este momento. Inténtalo más tarde."
        };
        return Results.Problem(userMessage, statusCode: (int)response.StatusCode);
    }

    using var json = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    var text = json.RootElement
        .GetProperty("candidates")[0]
        .GetProperty("content")
        .GetProperty("parts")[0]
        .GetProperty("text")
        .GetString() ?? "Sin respuesta.";

    return Results.Ok(text);
});

// ── Endpoints de pruebas de aceptación ──────────────────────────

app.MapGet("/api/tests/ut3962", () =>
{
    var (ok, msg) = Votify.Tests.ValoresPorDefectoTest.RunAll();
    return ok ? Results.Ok(msg) : Results.BadRequest(msg);
});

app.MapGet("/api/tests/ut3938", () =>
{
    var (ok, msg) = Votify.Tests.EncargadoCicloVidaTest.RunAll();
    return ok ? Results.Ok(msg) : Results.BadRequest(msg);
});

app.Run();

// ── Helpers de Sesión ───────────────────────────────────────────

static string? ObtenerUsernameAutenticado(HttpContext http)
    => http.Session.GetString(SessionConfig.UsernameKey);

static string? ValidarPesosResultados(int pesoJurado, int pesoPublico)
{
    if (pesoJurado < 0 || pesoJurado > 100 || pesoPublico < 0 || pesoPublico > 100)
        return "Los pesos de jurado y público deben estar entre 0 y 100";

    if (pesoJurado + pesoPublico != 100)
        return "Los pesos de jurado y público deben sumar 100";

    return null;
}

static bool UsuarioPuedeGestionarResultados(Votacion votacion, Usuario usuarioActual, IDAL<Organizador> organizadorRepo, IDAL<EncargadoVotacion> encargadoRepo)
{
    bool esEncargado = encargadoRepo.GetWhere(r => r.Id == votacion.EncargadoId && r.UsuarioId == usuarioActual.Id).Any();
    bool esOrganizador = organizadorRepo.GetWhere(r => r.UsuarioId == usuarioActual.Id && r.EventoId == votacion.EventoId).Any();
    return esEncargado || esOrganizador;
}

static string ObtenerEtiquetaRolMonitor(string? tipoRol)
{
    return (tipoRol ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "JURADO" => "Jurado",
        "PUBLICO" => "Publico",
        "COMPETIDOR" => "Competidor",
        _ => "Votante"
    };
}

static string ObtenerMensajeErrorDetallado(Exception ex)
{
    return ex.InnerException?.InnerException?.Message
        ?? ex.InnerException?.Message
        ?? ex.Message;
}

static ReclamacionDTO MapReclamacion(Reclamacion reclamacion, string solicitante)
{
    return new ReclamacionDTO
    {
        Id = reclamacion.Id,
        EventoId = reclamacion.EventoId,
        EventoNombre = reclamacion.evento?.Nombre ?? $"Evento #{reclamacion.EventoId}",
        Solicitante = solicitante,
        Descripcion = reclamacion.Descripcion ?? string.Empty,
        FechaCreacion = reclamacion.FechaCreacion,
        Estado = reclamacion.Estado ?? Reclamacion.EstadoPendiente,
        RespuestaOrganizador = reclamacion.RespuestaOrganizador,
        FechaRespuesta = reclamacion.FechaRespuesta
    };
}

static ProyectoResultadoDTO BuildProjectResult(Proyecto proyecto, List<Voto> votosProyecto, Votacion votacion)
{
    var votosJurado = votosProyecto.Where(ResultadosVotacionCalculator.EsVotoExperto).ToList();
    var votosPopular = votosProyecto.Where(ResultadosVotacionCalculator.EsVotoPopular).ToList();
    double mediaBruta = ResultadosVotacionCalculator.CalcularMedia(votosProyecto);
    double? mediaJurado = votosJurado.Any() ? ResultadosVotacionCalculator.CalcularMedia(votosJurado) : null;
    double? mediaPopular = votosPopular.Any() ? ResultadosVotacionCalculator.CalcularMedia(votosPopular) : null;
    double mediaAjustada = ResultadosVotacionCalculator.CalcularPuntuacionAjustada(
        mediaJurado,
        mediaPopular,
        votacion.PesoJurado,
        votacion.PesoPublico);

    var participantesAdicionales = string.IsNullOrWhiteSpace(proyecto.ParticipantesAdicionales)
        ? new List<string>()
        : proyecto.ParticipantesAdicionales.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList();

    return new ProyectoResultadoDTO
    {
        Id = proyecto.Id,
        Nombre = proyecto.Nombre ?? $"Proyecto #{proyecto.Id}",
        Descripcion = proyecto.Descripcion ?? string.Empty,
        Competidor = proyecto.competidor?.usuario?.Username ?? string.Empty,
        Participantes = participantesAdicionales,
        Media = mediaAjustada,
        MediaBruta = mediaBruta,
        MediaJurado = mediaJurado ?? 0,
        MediaPopular = mediaPopular ?? 0,
        NumVotos = votosProyecto.Count,
        NumVotosJurado = votosJurado.Count,
        NumVotosPopular = votosPopular.Count,
        PesoJuradoAplicado = votacion.PesoJurado,
        PesoPublicoAplicado = votacion.PesoPublico
    };
}

static ProyectoMonitorDTO BuildProjectMonitor(Proyecto proyecto, List<Voto> votosProyecto, Votacion votacion, int totalVotantesEsperados)
{
    var votosJurado = votosProyecto.Where(ResultadosVotacionCalculator.EsVotoExperto).ToList();
    var votosPopular = votosProyecto.Where(ResultadosVotacionCalculator.EsVotoPopular).ToList();
    double mediaBruta = ResultadosVotacionCalculator.CalcularMedia(votosProyecto);
    double? mediaJurado = votosJurado.Any() ? ResultadosVotacionCalculator.CalcularMedia(votosJurado) : null;
    double? mediaPopular = votosPopular.Any() ? ResultadosVotacionCalculator.CalcularMedia(votosPopular) : null;

    return new ProyectoMonitorDTO
    {
        Id = proyecto.Id,
        Nombre = proyecto.Nombre ?? $"Proyecto #{proyecto.Id}",
        Media = ResultadosVotacionCalculator.CalcularPuntuacionAjustada(
            mediaJurado,
            mediaPopular,
            votacion.PesoJurado,
            votacion.PesoPublico),
        MediaBruta = mediaBruta,
        MediaJurado = mediaJurado ?? 0,
        MediaPopular = mediaPopular ?? 0,
        NumVotos = votosProyecto.Count,
        NumVotosJurado = votosJurado.Count,
        NumVotosPopular = votosPopular.Count,
        TotalVotantesEsperados = totalVotantesEsperados
    };
}

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
record AsignarRolEventoRequest(string TipoRol);
record RolEventoResponse(int IdEvento, string? Rol);
record AiChatRequest(List<AiChatTurn> History, string Message);
record AiChatTurn(string Role, string Content);
record GuardarVotoRequest(int VotacionId, int ProyectoId, double Puntuacion, string? Comentario);
record CrearProyectoRequest(string Nombre, string? Descripcion, string UsernameCompetidor);
record ModificarProyectoRequest(string Nombre, string? Descripcion, List<string>? ParticipantesAdicionales);
