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
builder.Services.AddScoped<IComentarioPopularClassifier>(sp =>
{
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var apiKey = sp.GetRequiredService<IConfiguration>()["GeminiApiKey"] ?? string.Empty;
    return new GeminiComentarioPopularClassifier(httpFactory.CreateClient(), apiKey);
});
builder.Services.AddScoped<ComentariosPopularesAgrupador>();

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

// Render (y la mayoría de PaaS) terminan SSL en su edge y pasan HTTP al contenedor.
// Sin esto, app.UseHttpsRedirection generaría un loop infinito 301→301.
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
    KnownNetworks = { },
    KnownProxies = { }
});

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

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
}).DisableAntiforgery();

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
    catch { return Results.Ok(new List<int>()); }
});

// ── Comentarios del competidor ─────────────────────────────────

app.MapGet("/api/proyectos/{idVotacion}/mis-comentarios", (
    int idVotacion,
    IVotifyService service,
    IDAL<Votacion> votacionRepo,
    IDAL<Proyecto> proyectoRepo,
    IDAL<Competidor> competidorRepo,
    IDAL<Voto> votoRepo,
    HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        var usuarioActual = service.GetUsuarioActual();
        if (usuarioActual == null) return Results.Unauthorized();

        var votacion = votacionRepo.GetById(idVotacion);
        if (votacion == null) return Results.NotFound("Votación no encontrada");

        var competidorIds = competidorRepo
            .GetWhere(c => c.UsuarioId == usuarioActual.Id)
            .Select(c => c.Id)
            .ToHashSet();

        if (competidorIds.Count == 0)
            return Results.Ok(new List<string>());

        var proyectosCompetidor = proyectoRepo
            .GetWhere(p => competidorIds.Contains(p.CompetidorId))
            .ToList();

        var proyectosFiltrados = proyectosCompetidor
            .Where(p => ProyectoPerteneceAVotacion(p, votacion))
            .ToList();

        if (proyectosFiltrados.Count == 0)
            proyectosFiltrados = proyectosCompetidor;

        var comentarios = proyectosFiltrados
            .SelectMany(p => (p.votos ?? Enumerable.Empty<Voto>())
                .Where(v => v.VotacionId == idVotacion && !string.IsNullOrWhiteSpace(v.Comentario)))
            .OrderByDescending(v => v.Fecha)
            .Select(v => v.Comentario.Trim())
            .ToList();

        // Fallback por si la colección votos no viene cargada por el ORM
        if (comentarios.Count == 0)
        {
            var proyectoIds = proyectosFiltrados.Select(p => p.Id).ToHashSet();
            comentarios = votoRepo.GetWhere(v =>
                    v.VotacionId == idVotacion &&
                    proyectoIds.Contains(v.ProyectoId) &&
                    !string.IsNullOrWhiteSpace(v.Comentario))
                .OrderByDescending(v => v.Fecha)
                .Select(v => v.Comentario.Trim())
                .ToList();
        }

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
        return Results.Ok(new
        {
            Username = user,
            Email = email,
            FotoPerfil = foto,
            NotificacionesNoLeidas = service.GetCantidadNotificacionesNoLeidas()
        });
    }
    catch (ServiceException) { return Results.Unauthorized(); }
});

app.MapPut("/api/perfil/email", (UpdateEmailRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try { service.RestoreSession(username); service.UpdateEmail(req.NuevoEmail); return Results.Ok(); }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPut("/api/perfil/password", (UpdatePasswordRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try { service.RestoreSession(username); service.UpdatePassword(req.PasswordActual, req.NuevaPassword); return Results.Ok(); }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPut("/api/perfil/foto", (UpdateFotoRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try { service.RestoreSession(username); service.UpdateFotoPerfil(req.Base64Foto); return Results.Ok(); }
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

// ── Endpoints de notificaciones ─────────────────────────────────

app.MapGet("/api/notificaciones/recibidas", (IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        var notificaciones = service.GetNotificacionesRecibidas()
            .Select(n => MapNotificacion(n))
            .ToList();
        return Results.Ok(notificaciones);
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

app.MapGet("/api/notificaciones/enviadas", (IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        var notificaciones = service.GetNotificacionesEnviadas()
            .Select(n => MapNotificacion(n))
            .ToList();
        return Results.Ok(notificaciones);
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

app.MapPut("/api/notificaciones/{id}/leida", (int id, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        service.MarcarNotificacionComoLeida(id);
        return Results.Ok();
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
        // Serializa los criterios de cada categoría en el token que pasa al servicio
        int idVotacion = service.CrearVotacion(new CrearVotacionRequest
        {
            Titulo = req.Titulo,
            Descripcion = req.Descripcion,
            FechaFin = req.FechaFin,
            Activa = true,
            PermiteCompetidoresVotar = req.PermiteCompetidoresVotar,
            PesoJurado = req.PesoJurado,
            PesoPublico = req.PesoPublico,
            CodigoEncargado = req.CodigoEncargado,
            CodigoJurado = req.CodigoJurado,
            CorreosEncargados = req.CorreosEncargados,
            CorreosJurados = req.CorreosJurados,
            Categorias = req.Categorias?
                .Where(c => !string.IsNullOrWhiteSpace(c.Nombre))
                .Select(ConstruirTokenCategoria)
                .ToList() ?? new List<string>()
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
        var rolesPorEvento = MapearRolesPorEvento(usuarioActual);

        var votaciones = service.GetAllVotaciones().OrderBy(v => v.FechaFin).Select(v => new VotacionDTO
        {
            Id = v.Id,
            IdEvento = v.EventoId,
            NombreEvento = v.evento?.Nombre ?? string.Empty,
            Titulo = string.IsNullOrEmpty(v.Titulo) ? $"Votación #{v.Id}" : v.Titulo,
            Descripcion = ObtenerDescripcionVisible(v.Descripcion),
            FechaIni = v.FechaIni,
            FechaFin = v.FechaFin,
            Estado = v.Estado,
            PesoJurado = v.PesoJurado,
            PesoPublico = v.PesoPublico,
            Categorias = ObtenerCategoriasDeVotacion(v),
            RolActual = rolesPorEvento.TryGetValue(v.EventoId, out var rol) ? rol : null
        }).ToList();
        return Results.Ok(votaciones);
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapGet("/api/dashboard/eventos-resumen", (IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);

        var resumen = service.GetAllVotaciones()
            .GroupBy(v => v.EventoId)
            .Select(g =>
            {
                var evento = g.Select(v => v.evento).FirstOrDefault(e => e != null);
                var proyectos = (evento?.proyectos ?? Enumerable.Empty<Proyecto>()).ToList();

                var competidoresUnicos = proyectos
                    .Select(p => p.competidor?.usuario?.Username)
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Select(u => u!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                return new EventoResumenDashboardDTO
                {
                    IdEvento = g.Key,
                    ProjectsCount = proyectos.Count,
                    ParticipantsCount = competidoresUnicos
                };
            })
            .ToList();

        return Results.Ok(resumen);
    }
    catch (Exception ex)
    {
        return Results.Problem(ObtenerMensajeErrorDetallado(ex));
    }
});

app.MapGet("/api/eventos/{idEvento}/votaciones", (int idEvento, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        var usuarioActual = service.GetUsuarioActual();
        var rolesPorEvento = MapearRolesPorEvento(usuarioActual);

        var votaciones = service.GetVotacionesByEvento(idEvento)
            .OrderBy(v => v.FechaFin)
            .Select(v => new VotacionDTO
            {
                Id = v.Id,
                IdEvento = v.EventoId,
                NombreEvento = v.evento?.Nombre ?? string.Empty,
                Titulo = string.IsNullOrEmpty(v.Titulo) ? $"Votación #{v.Id}" : v.Titulo,
                Descripcion = ObtenerDescripcionVisible(v.Descripcion),
                FechaIni = v.FechaIni,
                FechaFin = v.FechaFin,
                Estado = v.Estado,
                PesoJurado = v.PesoJurado,
                PesoPublico = v.PesoPublico,
                Categorias = ObtenerCategoriasDeVotacion(v),
                RolActual = rolesPorEvento.TryGetValue(v.EventoId, out var rol) ? rol : null
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
            Descripcion = ObtenerDescripcionVisible(votacion.Descripcion),
            FechaIni = votacion.FechaIni,
            FechaFin = votacion.FechaFin,
            Estado = votacion.Estado,
            PesoJurado = votacion.PesoJurado,
            PesoPublico = votacion.PesoPublico,
            Categorias = ObtenerCategoriasDeVotacion(votacion),
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
        service.AsignarRolEnEvento(req.TipoRol, idEvento, req.CodigoAcceso);
        return Results.Ok(new RolEventoResponse(idEvento, req.TipoRol.Trim().ToUpperInvariant()));
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
});

app.MapDelete("/api/votaciones/{id}", (int id, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try { service.RestoreSession(username); service.EliminarEvento(id); return Results.Ok(); }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapPut("/api/votaciones/{id}", (int id, VotacionDTO req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try { service.RestoreSession(username); service.ModificarVotacion(id, req.FechaFin, req.Estado); return Results.Ok(); }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapPost("/api/votaciones/{id}/cerrar", (int id, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try { service.RestoreSession(username); service.CerrarVotacion(id); return Results.Ok(); }
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
}).DisableAntiforgery();

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
            Competidor = proyecto.competidor?.usuario?.Username ?? req.UsernameCompetidor ?? string.Empty,
            Categoria = ObtenerCategoriaDeParticipantes(proyecto.ParticipantesAdicionales),
            Participantes = ObtenerParticipantesLimpios(proyecto.ParticipantesAdicionales),
            Media = 0,
            NumVotos = 0,
            Rank = 0
        });
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapPut("/api/proyectos/{idVotacion}/{idProyecto}/foto", (
    int idVotacion,
    int idProyecto,
    UpdateProyectoFotoRequest req,
    IVotifyService service,
    IDAL<Votacion> votacionRepo,
    IDAL<Proyecto> proyectoRepo,
    HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        service.RestoreSession(username);
        var usuario = service.GetUsuarioActual();
        if (usuario == null) return Results.Unauthorized();

        var votacion = votacionRepo.GetById(idVotacion);
        if (votacion == null) return Results.NotFound("Votación no encontrada");

        var proyecto = proyectoRepo.GetById(idProyecto);
        if (proyecto == null) return Results.NotFound("Proyecto no encontrado");

        if (proyecto.EventoId != votacion.EventoId)
            return Results.BadRequest("El proyecto no pertenece al evento de la votación");

        if (string.IsNullOrWhiteSpace(req.Base64Foto))
        {
            proyecto.FotoProyecto = null;
        }
        else
        {
            if (!EsDataUrlImagenValida(req.Base64Foto))
                return Results.BadRequest("Formato de imagen inválido");

            proyecto.FotoProyecto = req.Base64Foto.Trim();
        }

        proyectoRepo.Commit();
        return Results.Ok();
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
}).DisableAntiforgery();

app.MapGet("/api/proyectos/{idVotacion}/{idProyecto}/imagen", (
    int idVotacion,
    int idProyecto,
    IDAL<Votacion> votacionRepo,
    IDAL<Proyecto> proyectoRepo,
    HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    var votacion = votacionRepo.GetById(idVotacion);
    if (votacion == null) return Results.NotFound("Votación no encontrada");

    var proyecto = proyectoRepo.GetById(idProyecto);
    if (proyecto == null) return Results.NotFound("Proyecto no encontrado");

    if (proyecto.EventoId != votacion.EventoId)
        return Results.BadRequest("El proyecto no pertenece al evento de la votación");

    if (string.IsNullOrWhiteSpace(proyecto.FotoProyecto))
        return Results.NotFound("No hay contenido");

    if (!TryParseDataUrl(proyecto.FotoProyecto, out var contentType, out var bytes))
        return Results.Problem("La imagen guardada es inválida");

    return Results.File(bytes, contentType);
});

app.MapPut("/api/proyectos/{idVotacion}/{idProyecto}", (int idVotacion, int idProyecto, ModificarProyectoRequest req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try { service.RestoreSession(username); service.ModificarProyecto(idProyecto, req.Nombre, req.Descripcion, req.ParticipantesAdicionales); return Results.Ok(); }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapDelete("/api/proyectos/{idVotacion}/{idProyecto}", (int idVotacion, int idProyecto, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try { service.RestoreSession(username); service.EliminarProyecto(idProyecto); return Results.Ok(); }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

// ── Endpoint de resultados reales ───────────────────────────────

app.MapGet("/api/votaciones/{id}/configuracion-resultados", (
    int id, IVotifyService service, IDAL<Votacion> votacionRepo,
    IDAL<Organizador> organizadorRepo, IDAL<EncargadoVotacion> encargadoRepo, HttpContext http) =>
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
        return Results.Ok(new ConfiguracionResultadosDTO { PesoJurado = votacion.PesoJurado, PesoPublico = votacion.PesoPublico });
    }
    catch (ServiceException ex) { return Results.BadRequest(ex.Message); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapPut("/api/votaciones/{id}/configuracion-resultados", (
    int id, ConfiguracionResultadosDTO req, IVotifyService service, IDAL<Votacion> votacionRepo,
    IDAL<Organizador> organizadorRepo, IDAL<EncargadoVotacion> encargadoRepo, HttpContext http) =>
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
        if (errorPesos != null) return Results.BadRequest(errorPesos);
        votacion.PesoJurado = req.PesoJurado;
        votacion.PesoPublico = req.PesoPublico;
        votacionRepo.Commit();
        return Results.Ok(new ConfiguracionResultadosDTO { PesoJurado = votacion.PesoJurado, PesoPublico = votacion.PesoPublico });
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

        // Solo proyectos que pertenecen a ESTA categoría/votación
        var proyectos = (evento.proyectos ?? Enumerable.Empty<Proyecto>())
            .Where(p => ProyectoPerteneceAVotacion(p, votacion))
            .ToList();

        var votos = votoRepo.GetWhere(v => v.VotacionId == idVotacion).ToList();
        var votosPorProyecto = votos.GroupBy(v => v.ProyectoId).ToDictionary(g => g.Key, g => g.ToList());

        var resultados = proyectos
            .Select(p => BuildProjectResult(p, votosPorProyecto.TryGetValue(p.Id, out var vp) ? vp : new List<Voto>(), votacion))
            .OrderByDescending(r => r.Media)
            .Select((r, i) => { r.Rank = i + 1; return r; })
            .ToList();

        return Results.Ok(resultados);
    }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

// ── Endpoint de comentarios populares ───────────────────────────

app.MapGet("/api/resultados/{idVotacion}/proyectos/{idProyecto}/comentarios-populares", async (
    int idVotacion,
    int idProyecto,
    IDAL<Votacion> votacionRepo,
    IDAL<Proyecto> proyectoRepo,
    IDAL<Voto> votoRepo,
    ComentariosPopularesAgrupador agrupador,
    HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        var votacion = votacionRepo.GetById(idVotacion);
        if (votacion == null) return Results.NotFound("VotaciÃ³n no encontrada");

        var proyecto = proyectoRepo.GetById(idProyecto);
        if (proyecto == null) return Results.NotFound("Proyecto no encontrado");

        if (proyecto.EventoId != votacion.EventoId || !ProyectoPerteneceAVotacion(proyecto, votacion))
            return Results.BadRequest("El proyecto no pertenece a esta votaciÃ³n");

        var votos = votoRepo.GetWhere(v => v.VotacionId == idVotacion && v.ProyectoId == idProyecto).ToList();
        var resultado = await agrupador.AgruparAsync(votos, http.RequestAborted);
        return Results.Ok(MapComentariosPopulares(resultado));
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

        var votes = votoRepo.GetWhere(v => v.VotacionId == idVotacion).ToList();
        var voterIds = votes.Select(v => v.VotanteId).Distinct().ToHashSet();
        var evento = votacion.evento;

        var rolesVotantes = juradoRepo.GetWhere(r => voterIds.Contains(r.Id)).Cast<Rol>()
            .Concat(publicoRepo.GetWhere(r => voterIds.Contains(r.Id)).Cast<Rol>())
            .Concat(competidorRepo.GetWhere(r => voterIds.Contains(r.Id)).Cast<Rol>())
            .GroupBy(r => r.Id).Select(g => g.First()).ToList();
        var userIds = rolesVotantes.Select(r => r.UsuarioId).Distinct().ToHashSet();
        var usuarios = usuarioRepo.GetAll().ToList().Where(u => userIds.Contains(u.Id)).ToList();
        var usernamePorRolId = rolesVotantes.ToDictionary(r => r.Id, r => usuarios.FirstOrDefault(u => u.Id == r.UsuarioId)?.Username ?? $"Votante #{r.Id}");

        var todosVotantes = new List<VotanteEstadoDTO>();
        try
        {
            if (votacion.EventoId > 0)
            {
                var tiposPermitidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "JURADO", "PUBLICO" };
                if (evento?.PermiteCompetidoresVotar == true) tiposPermitidos.Add("COMPETIDOR");

                var rolesEsperados = juradoRepo.GetWhere(r => r.EventoId == votacion.EventoId).Cast<Rol>()
                    .Concat(publicoRepo.GetWhere(r => r.EventoId == votacion.EventoId).Cast<Rol>())
                    .Concat(evento?.PermiteCompetidoresVotar == true
                        ? competidorRepo.GetWhere(r => r.EventoId == votacion.EventoId).Cast<Rol>()
                        : Enumerable.Empty<Rol>())
                    .Where(r => tiposPermitidos.Contains(r.TipoRol ?? string.Empty))
                    .GroupBy(r => r.Id).Select(g => g.First())
                    .OrderBy(r => r.TipoRol).ThenBy(r => r.Id).ToList();

                var expectedUserIds = rolesEsperados.Select(r => r.UsuarioId).Distinct().ToHashSet();
                var usuariosEsperados = usuarioRepo.GetAll().ToList()
                    .Where(u => expectedUserIds.Contains(u.Id)).GroupBy(u => u.Id).Select(g => g.First())
                    .ToDictionary(u => u.Id, u => u.Username ?? $"Usuario #{u.Id}");

                foreach (var rol in rolesEsperados)
                    todosVotantes.Add(new VotanteEstadoDTO
                    {
                        Nombre = usuariosEsperados.TryGetValue(rol.UsuarioId, out var nombre) ? nombre : $"Usuario #{rol.UsuarioId}",
                        Tipo = ObtenerEtiquetaRolMonitor(rol.TipoRol),
                        HaVotado = voterIds.Contains(rol.Id)
                    });
            }
            else
            {
                var jurados = votacion.jurados?.ToList() ?? new();
                var publicos = votacion.publicos?.ToList() ?? new();
                var competidores = evento?.PermiteCompetidoresVotar == true
                    ? evento.roles?.OfType<Competidor>().ToList() ?? new List<Competidor>()
                    : new List<Competidor>();
                var allRolIds = jurados.Select(j => j.Id).Concat(publicos.Select(p => p.Id)).Concat(competidores.Select(c => c.Id)).ToHashSet();
                var allRoles = juradoRepo.GetWhere(r => allRolIds.Contains(r.Id)).Cast<Rol>()
                    .Concat(publicoRepo.GetWhere(r => allRolIds.Contains(r.Id)).Cast<Rol>())
                    .Concat(competidorRepo.GetWhere(r => allRolIds.Contains(r.Id)).Cast<Rol>())
                    .GroupBy(r => r.Id).Select(g => g.First()).ToList();
                var allUserIds = allRoles.Select(r => r.UsuarioId).Distinct().ToHashSet();
                var allUsers = usuarioRepo.GetAll().ToList().Where(u => allUserIds.Contains(u.Id)).ToList();
                var nameMap = allRoles.ToDictionary(r => r.Id, r => allUsers.FirstOrDefault(u => u.Id == r.UsuarioId)?.Username ?? $"#{r.Id}");
                foreach (var j in jurados)
                    todosVotantes.Add(new VotanteEstadoDTO { Nombre = nameMap.TryGetValue(j.Id, out var n) ? n : $"Jurado #{j.Id}", Tipo = "Jurado", HaVotado = voterIds.Contains(j.Id) });
                foreach (var p in publicos)
                    todosVotantes.Add(new VotanteEstadoDTO { Nombre = nameMap.TryGetValue(p.Id, out var n) ? n : $"Público #{p.Id}", Tipo = "Público", HaVotado = voterIds.Contains(p.Id) });
            }
        }
        catch { /* Ignorado por seguridad de la relación */ }

        var proyectos = (evento?.proyectos ?? Enumerable.Empty<Proyecto>())
            .Where(p => ProyectoPerteneceAVotacion(p, votacion))
            .ToList();
        var votosPorProyecto = votes.GroupBy(v => v.ProyectoId).ToDictionary(g => g.Key, g => g.ToList());
        int totalVotantesEsperados = todosVotantes.Count;

        var proyectoStats = proyectos
            .Select(p => BuildProjectMonitor(p, votosPorProyecto.TryGetValue(p.Id, out var vp) ? vp : new List<Voto>(), votacion, totalVotantesEsperados))
            .OrderByDescending(p => p.Media).ToList();

        var historial = votes.OrderByDescending(v => v.Fecha).Select(v => new VotoHistorialDTO
        {
            Votante = usernamePorRolId.TryGetValue(v.VotanteId, out var name) ? name : $"Votante #{v.VotanteId}",
            Proyecto = v.proyecto?.Nombre ?? $"Proyecto #{v.ProyectoId}",
            Valor = v.Valor,
            Comentario = string.IsNullOrWhiteSpace(v.Comentario) ? null : v.Comentario,
            Fecha = v.Fecha
        }).ToList();

        return Results.Ok(new MonitorVotacionDTO
        {
            VotosEmitidos = votes.Count,
            TotalVotantes = todosVotantes.Count,
            PromedioGeneral = proyectoStats.Any(p => p.NumVotos > 0) ? Math.Round(proyectoStats.Where(p => p.NumVotos > 0).Average(p => p.Media), 2) : 0,
            PesoJurado = votacion.PesoJurado,
            PesoPublico = votacion.PesoPublico,
            Proyectos = proyectoStats,
            Votantes = todosVotantes,
            Historial = historial
        });
    }
    catch (Exception ex) { return Results.Problem(ObtenerMensajeErrorDetallado(ex)); }
});

// ── Endpoint de IA ──────────────────────────────────────────────

app.MapPost("/api/ai/chat", async (AiChatRequest req, IConfiguration config, IHttpClientFactory httpFactory, IVotifyService service, IDAL<Votacion> votacionRepo, IDAL<Proyecto> proyectoRepo, IDAL<Competidor> competidorRepo, IDAL<Voto> votoRepo, HttpContext http) =>
{
    var apiKey = config["GeminiApiKey"] ?? "";
    if (string.IsNullOrEmpty(apiKey)) return Results.Problem("API key no configurada.");

    var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent?key={apiKey}";

    var eventosContexto = "";
    var comentariosContexto = "";
    var username = ObtenerUsernameAutenticado(http);
    if (username != null)
    {
        try
        {
            service.RestoreSession(username);
            var usuarioActual = service.GetUsuarioActual();

            // ── Contexto de eventos ──
            var votaciones = service.GetMisVotaciones().ToList();
            if (votaciones.Any())
            {
                var lineas = votaciones.Select(v => $"- \"{v.Titulo ?? $"Votación #{v.Id}"}\" " +
                    $"(ID: {v.Id}, " +
                    $"del {v.FechaIni:dd/MM/yyyy} al {v.FechaFin:dd/MM/yyyy}, " +
                    $"estado: {(v.FechaFin >= DateTime.Today ? "activo" : "finalizado")})");
                eventosContexto = $"\n\nEventos actuales del usuario:\n{string.Join("\n", lineas)}";
            }
            else
            {
                eventosContexto = "\n\nEl usuario no tiene eventos creados actualmente.";
            }

            // ── Contexto de comentarios del jurado (si es competidor) ──
            if (usuarioActual != null)
            {
                var competidorIds = competidorRepo
                    .GetWhere(c => c.UsuarioId == usuarioActual.Id)
                    .Select(c => c.Id)
                    .ToHashSet();

                if (competidorIds.Count > 0)
                {
                    var proyectosCompetidor = proyectoRepo
                        .GetWhere(p => competidorIds.Contains(p.CompetidorId))
                        .ToList();

                    var comentarios = proyectosCompetidor
                        .SelectMany(p => (p.votos ?? Enumerable.Empty<Voto>())
                            .Where(v => !string.IsNullOrWhiteSpace(v.Comentario)))
                        .OrderByDescending(v => v.Fecha)
                        .Select(v => v.Comentario.Trim())
                        .Distinct()
                        .ToList();

                    if (comentarios.Count == 0)
                    {
                        var proyectoIds = proyectosCompetidor.Select(p => p.Id).ToHashSet();
                        comentarios = votoRepo.GetWhere(v =>
                                proyectoIds.Contains(v.ProyectoId) &&
                                !string.IsNullOrWhiteSpace(v.Comentario))
                            .OrderByDescending(v => v.Fecha)
                            .Select(v => v.Comentario.Trim())
                            .Distinct()
                            .ToList();
                    }

                    if (comentarios.Any())
                    {
                        var comentariosStr = string.Join("\n- ", comentarios);
                        comentariosContexto = $"\n\nComentarios del jurado sobre los proyectos del usuario:\n- {comentariosStr}\n\n" +
                            $"Si el usuario te pide sintetizar estos comentarios, genera UN único mensaje constructivo dirigido al competidor, " +
                            $"hablando del 'jurado' en plural sin mencionar quién dijo qué específicamente. Sé conciso y útil.";
                    }
                    else
                    {
                        comentariosContexto = "\n\nEl usuario es competidor pero aún no tiene comentarios del jurado sobre sus proyectos. " +
                            "Si te pide sintetizarlos, dile claramente que todavía no ha recibido ningún comentario, en lugar de decir que no tienes acceso.";
                    }
                }
            }
        }
        catch { }
    }

    var systemPrompt =
        "Eres el asistente de Votify, una plataforma para gestionar votaciones en hackathones, ferias de innovación y concursos. " +
        "Ayuda a los usuarios con dudas sobre cómo votar, crear eventos, gestionar proyectos y usar el sistema. " +
        "Responde siempre en español, de forma concisa y útil. Si no sabes algo, dilo claramente." +
        eventosContexto +
        comentariosContexto;

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

app.MapGet("/api/tests/ut-comentarios-populares-ia", () =>
{
    var (ok, msg) = Votify.Tests.ComentariosPopularesIATest.RunAll();
    return ok ? Results.Ok(msg) : Results.BadRequest(msg);
});

app.Run();

// ── Helpers ────────────────────────────────────────────────────

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

// Devuelve un diccionario eventoId → tipoRol cargando todos los roles del usuario en
// un único acceso (lazy loading de EF), evitando 5 queries por votación al construir DTOs.
static Dictionary<int, string?> MapearRolesPorEvento(Usuario usuarioActual)
{
    return usuarioActual.roles?
        .GroupBy(r => r.EventoId)
        .ToDictionary(g => g.Key, g => g.First().TipoRol)
        ?? new Dictionary<int, string?>();
}

static bool UsuarioPuedeGestionarResultados(Votacion votacion, Usuario usuarioActual, IDAL<Organizador> organizadorRepo, IDAL<EncargadoVotacion> encargadoRepo)
{
    bool esEncargado = encargadoRepo.GetWhere(r => r.Id == votacion.EncargadoId && r.UsuarioId == usuarioActual.Id).Any();
    bool esOrganizador = organizadorRepo.GetWhere(r => r.UsuarioId == usuarioActual.Id && r.EventoId == votacion.EventoId).Any();
    return esEncargado || esOrganizador;
}

static string ObtenerEtiquetaRolMonitor(string? tipoRol) =>
    (tipoRol ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "JURADO" => "Jurado",
        "PUBLICO" => "Publico",
        "COMPETIDOR" => "Competidor",
        _ => "Votante"
    };

static string ObtenerMensajeErrorDetallado(Exception ex) =>
    ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;

const string MarcadorCriterios = "\n__CRITERIOS__:";

/// Convierte una CategoriaBaremoDTO en el token que recibe el servicio (nombre + criterios codificados)
static string ConstruirTokenCategoria(CategoriaBaremoDTO categoria)
{
    string nombre = categoria.Nombre?.Trim() ?? string.Empty;
    if (!categoria.Criterios.Any()) return nombre;
    string json = System.Text.Json.JsonSerializer.Serialize(categoria.Criterios);
    string encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    return $"{nombre}||__CRITERIOS__:{encoded}";
}

/// La descripción visible: todo lo que está antes del marcador de criterios
static string ObtenerDescripcionVisible(string? descripcion)
{
    if (string.IsNullOrWhiteSpace(descripcion)) return string.Empty;
    int idx = descripcion.IndexOf(MarcadorCriterios, StringComparison.Ordinal);
    return (idx >= 0 ? descripcion[..idx] : descripcion).Trim();
}

/// Extrae la lista de CriterioDTO de la descripción almacenada
static List<CriterioDTO> ObtenerCriteriosDeDescripcion(string? descripcion)
{
    if (string.IsNullOrWhiteSpace(descripcion)) return new();

    int idx = descripcion.IndexOf(MarcadorCriterios, StringComparison.Ordinal);
    string markerUsado = MarcadorCriterios;

    if (idx < 0)
    {
        const string marcadorLegacy = "\n||CRITERIOS||:";
        idx = descripcion.IndexOf(marcadorLegacy, StringComparison.Ordinal);
        markerUsado = marcadorLegacy;
    }

    if (idx < 0) return new();

    string encoded = descripcion[(idx + markerUsado.Length)..].Trim();
    if (string.IsNullOrWhiteSpace(encoded)) return new();

    try
    {
        string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        return System.Text.Json.JsonSerializer.Deserialize<List<CriterioDTO>>(json) ?? new();
    }
    catch { return new(); }
}

/// Construye la lista de categorías con criterios que va en el VotacionDTO
static List<CategoriaBaremoDTO> ObtenerCategoriasDeVotacion(Votacion votacion)
{
    var criterios = ObtenerCriteriosDeDescripcion(votacion.Descripcion);
    if (!criterios.Any()) return new();
    return new List<CategoriaBaremoDTO>
    {
        new() { Nombre = votacion.Titulo?.Trim() ?? string.Empty, Criterios = criterios }
    };
}

// Prefijo que el servicio escribe en ParticipantesAdicionales para marcar la categoría del proyecto
const string PrefijoCatProyecto = "__CAT__:";

static string ObtenerCategoriaDeParticipantes(string? participantesAdicionales)
{
    if (string.IsNullOrWhiteSpace(participantesAdicionales)) return string.Empty;
    return participantesAdicionales.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim())
        .FirstOrDefault(p => p.StartsWith(PrefijoCatProyecto, StringComparison.OrdinalIgnoreCase))
        ?[PrefijoCatProyecto.Length..].Trim()
        ?? string.Empty;
}

static List<string> ObtenerParticipantesLimpios(string? participantesAdicionales)
{
    if (string.IsNullOrWhiteSpace(participantesAdicionales)) return new();
    return participantesAdicionales.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim())
        .Where(p => !string.IsNullOrEmpty(p) && !p.StartsWith(PrefijoCatProyecto, StringComparison.OrdinalIgnoreCase))
        .ToList();
}

/// Un proyecto pertenece a esta votación si su marcador de categoría coincide con el título de la votación
static bool ProyectoPerteneceAVotacion(Proyecto proyecto, Votacion votacion)
{
    var catProyecto = ObtenerCategoriaDeParticipantes(proyecto.ParticipantesAdicionales);
    if (string.IsNullOrWhiteSpace(catProyecto)) return true; // sin marcador → pertenece a todas
    return string.Equals(catProyecto, votacion.Titulo?.Trim(), StringComparison.OrdinalIgnoreCase);
}

static NotificacionDTO MapNotificacion(Notificacion notificacion)
{
    return new NotificacionDTO
    {
        Id = notificacion.Id,
        Asunto = notificacion.Asunto,
        Mensaje = notificacion.Mensaje,
        FechaCreacion = notificacion.FechaCreacion,
        Leida = notificacion.Leida,
        RemitenteUsername = notificacion.remitente?.Username ?? string.Empty,
        DestinatarioUsername = notificacion.destinatario?.Username ?? string.Empty
    };
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

static ComentariosPopularesDTO MapComentariosPopulares(ResultadoComentariosPopulares resultado)
{
    return new ComentariosPopularesDTO
    {
        Categorias = resultado.Categorias,
        Comentarios = resultado.Comentarios.Select(c => new ComentarioPopularDTO
        {
            Id = c.Id,
            Autor = c.Autor,
            Texto = c.Texto,
            Fecha = c.Fecha,
            TipoRol = c.TipoRol,
            Categoria = c.Categoria
        }).ToList()
    };
}

static ProyectoResultadoDTO BuildProjectResult(Proyecto proyecto, List<Voto> votosProyecto, Votacion votacion)
{
    var votosJurado = votosProyecto.Where(ResultadosVotacionCalculator.EsVotoExperto).ToList();
    var votosPopular = votosProyecto.Where(ResultadosVotacionCalculator.EsVotoPopular).ToList();
    double mediaBruta = ResultadosVotacionCalculator.CalcularMedia(votosProyecto);
    double? mediaJurado = votosJurado.Any() ? ResultadosVotacionCalculator.CalcularMedia(votosJurado) : null;
    double? mediaPopular = votosPopular.Any() ? ResultadosVotacionCalculator.CalcularMedia(votosPopular) : null;
    double mediaAjustada = ResultadosVotacionCalculator.CalcularPuntuacionAjustada(mediaJurado, mediaPopular, votacion.PesoJurado, votacion.PesoPublico);

    var comentarios = votosProyecto
        .Where(v => !string.IsNullOrWhiteSpace(v.Comentario))
        .Select(v => v.Comentario.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(6)
        .ToList();

    return new ProyectoResultadoDTO
    {
        Id = proyecto.Id,
        Nombre = proyecto.Nombre ?? $"Proyecto #{proyecto.Id}",
        Descripcion = proyecto.Descripcion ?? string.Empty,
        Competidor = proyecto.competidor?.usuario?.Username ?? string.Empty,
        Categoria = ObtenerCategoriaDeParticipantes(proyecto.ParticipantesAdicionales),
        Participantes = ObtenerParticipantesLimpios(proyecto.ParticipantesAdicionales),
        Media = mediaAjustada,
        MediaBruta = mediaBruta,
        MediaJurado = mediaJurado ?? 0,
        MediaPopular = mediaPopular ?? 0,
        NumVotos = votosProyecto.Count,
        NumVotosJurado = votosJurado.Count,
        NumVotosPopular = votosPopular.Count,
        PesoJuradoAplicado = votacion.PesoJurado,
        PesoPublicoAplicado = votacion.PesoPublico,
        TieneImagen = !string.IsNullOrWhiteSpace(proyecto.FotoProyecto),
        Comentarios = comentarios
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
        Media = ResultadosVotacionCalculator.CalcularPuntuacionAjustada(mediaJurado, mediaPopular, votacion.PesoJurado, votacion.PesoPublico),
        MediaBruta = mediaBruta,
        MediaJurado = mediaJurado ?? 0,
        MediaPopular = mediaPopular ?? 0,
        NumVotos = votosProyecto.Count,
        NumVotosJurado = votosJurado.Count,
        NumVotosPopular = votosPopular.Count,
        TotalVotantesEsperados = totalVotantesEsperados
    };
}

static bool EsDataUrlImagenValida(string dataUrl)
    => TryParseDataUrl(dataUrl, out _, out _);

static bool TryParseDataUrl(string dataUrl, out string contentType, out byte[] bytes)
{
    contentType = "application/octet-stream";
    bytes = Array.Empty<byte>();

    if (string.IsNullOrWhiteSpace(dataUrl))
        return false;

    var trimmed = dataUrl.Trim();
    if (!trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        return false;

    int commaIndex = trimmed.IndexOf(',');
    if (commaIndex <= 0)
        return false;

    var metadata = trimmed.Substring(5, commaIndex - 5);
    var payload = trimmed[(commaIndex + 1)..];

    if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        return false;

    var mediaType = metadata.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
    if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        return false;

    try
    {
        bytes = Convert.FromBase64String(payload);
        contentType = mediaType;
        return bytes.Length > 0;
    }
    catch
    {
        return false;
    }
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
record AsignarRolEventoRequest(string TipoRol, string? CodigoAcceso);
record RolEventoResponse(int IdEvento, string? Rol);
record AiChatRequest(List<AiChatTurn> History, string Message);
record AiChatTurn(string Role, string Content);
record GuardarVotoRequest(int VotacionId, int ProyectoId, double Puntuacion, string? Comentario);
record CrearProyectoRequest(string Nombre, string? Descripcion, string? UsernameCompetidor);
record ModificarProyectoRequest(string Nombre, string? Descripcion, List<string>? ParticipantesAdicionales);
record UpdateProyectoFotoRequest(string Base64Foto);
