using System.Data.Common;
using Votify.Entities;
using Votify.shared;
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
builder.Services.AddHttpClient();

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

// Devuelve los IDs de proyectos que el usuario ya votó en esta votación
app.MapGet("/api/votos/misVotos/{idVotacion}", (int idVotacion, IDAL dal, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Ok(new List<int>());

    try
    {
        var usuario = dal.GetWhere<Usuario>(u => u.Username == username).FirstOrDefault();
        if (usuario == null) return Results.Ok(new List<int>());

        var votacion = dal.GetById<Votacion>(idVotacion);
        if (votacion?.evento == null) return Results.Ok(new List<int>());

        int idEvento = votacion.evento.IdEvento;
        int uid = usuario.Id;

        // Buscar el rol consultando cada subtype por separado (evita JOIN TPT)
        int rolId =
            dal.GetWhere<Jurado>(r => r.UsuarioId == uid && r.EventoId == idEvento).FirstOrDefault()?.Id ??
            dal.GetWhere<Publico>(r => r.UsuarioId == uid && r.EventoId == idEvento).FirstOrDefault()?.Id ??
            dal.GetWhere<Competidor>(r => r.UsuarioId == uid && r.EventoId == idEvento).FirstOrDefault()?.Id ??
            0;

        if (rolId == 0) return Results.Ok(new List<int>());

        var proyectosVotados = dal.GetWhere<Voto>(v =>
                v.VotanteId == rolId && v.VotacionId == idVotacion)
            .Select(v => v.ProyectoId)
            .ToList();

        return Results.Ok(proyectosVotados);
    }
    catch
    {
        return Results.Ok(new List<int>());
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
app.MapPost("/api/votaciones", (VotacionDTO req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        int idVotacion = service.CrearVotacion(req.Titulo, req.Descripcion, req.FechaFin, true);
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
        var votaciones = service.GetMisVotaciones().Select(v => new VotacionDTO
        {
            Id = v.Id,
            IdEvento = v.evento?.IdEvento ?? 0,
            Descripcion = v.Descripcion,
            Titulo = string.IsNullOrEmpty(v.Titulo) ? $"Votación #{v.Id}" : v.Titulo,
            FechaIni = v.FechaIni,
            FechaFin = v.FechaFin
        }).ToList();

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
        return Results.Ok(new VotacionDTO
        {
            Id = votacion.Id,
            IdEvento = votacion.evento.IdEvento,
            Titulo = votacion.Titulo,
            Descripcion = votacion.Descripcion,
            FechaIni = votacion.FechaIni,
            FechaFin = votacion.FechaFin
        });
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

app.MapPost("/api/proyectos/{idVotacion}", (int idVotacion, CrearProyectoRequest req, IDAL dal, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        // 1. Cargar votación y su evento
        var votacion = dal.GetById<Votacion>(idVotacion);
        if (votacion == null) return Results.NotFound("Votación no encontrada");

        var evento = votacion.evento;
        if (evento == null) return Results.NotFound("Evento no encontrado");

        // 2. Verificar que quien llama es organizador u encargado del evento
        var usuarioAuth = dal.GetWhere<Usuario>(u => u.Username == username).FirstOrDefault();
        if (usuarioAuth == null) return Results.Unauthorized();

        if (!EsOrganizadorDelEvento(dal, usuarioAuth.Id, evento.IdEvento))
            return Results.Forbid();

        // 3. Buscar el usuario competidor
        var competidorUser = dal.GetWhere<Usuario>(u => u.Username == req.UsernameCompetidor).FirstOrDefault();
        if (competidorUser == null)
            return Results.BadRequest($"No existe ningún usuario con el nombre '{req.UsernameCompetidor}'");

        // 4. Buscar o crear el rol Competidor para ese usuario en este evento
        var competidorRol = dal.GetWhere<Competidor>(c =>
            c.UsuarioId == competidorUser.Id && c.EventoId == evento.IdEvento).FirstOrDefault();

        if (competidorRol == null)
        {
            competidorRol = new Competidor(DateTime.Now, 0)
            {
                usuario   = competidorUser,
                evento    = evento,
                UsuarioId = competidorUser.Id,
                EventoId  = evento.IdEvento
            };
            dal.Insert<Competidor>(competidorRol);
            dal.Commit(); // commit para que EF asigne el Id
        }

        // 5. Crear el proyecto con FKs escalares explícitas (evita shadow FK bug de EF6)
        var proyecto = new Proyecto
        {
            Nombre                  = req.Nombre.Trim(),
            Descripcion             = req.Descripcion?.Trim() ?? "",
            competidor              = competidorRol,
            evento                  = evento,
            CompetidorId            = competidorRol.Id,
            EventoId                = evento.IdEvento,
            ParticipantesAdicionales = ""
        };
        dal.Insert<Proyecto>(proyecto);
        dal.Commit();

        return Results.Ok(new ProyectoResultadoDTO
        {
            Id          = proyecto.Id,
            Nombre      = proyecto.Nombre,
            Descripcion = proyecto.Descripcion,
            Competidor  = competidorUser.Username,
            Media       = 0,
            NumVotos    = 0,
            Rank        = 0
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPut("/api/proyectos/{idVotacion}/{idProyecto}", (int idVotacion, int idProyecto, ModificarProyectoRequest req, IDAL dal, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        var proyecto = dal.GetById<Proyecto>(idProyecto);
        if (proyecto == null) return Results.NotFound("Proyecto no encontrado");

        var usuarioAuth = dal.GetWhere<Usuario>(u => u.Username == username).FirstOrDefault();
        if (usuarioAuth == null) return Results.Unauthorized();

        var evento = proyecto.evento;
        if (evento == null) return Results.NotFound("Evento no encontrado");

        if (!EsOrganizadorDelEvento(dal, usuarioAuth.Id, evento.IdEvento)) return Results.Forbid();

        proyecto.Nombre = req.Nombre.Trim();
        proyecto.Descripcion = req.Descripcion?.Trim() ?? string.Empty;

        // Guardar participantes adicionales (excluyendo el competidor líder para evitar duplicados)
        if (req.ParticipantesAdicionales != null)
        {
            string leadUsername = proyecto.competidor?.usuario?.Username ?? "";
            var adicionales = req.ParticipantesAdicionales
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrEmpty(u) && u != leadUsername)
                .Distinct()
                .ToList();
            proyecto.ParticipantesAdicionales = string.Join(",", adicionales);
        }

        dal.Commit();

        return Results.Ok();
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});


app.MapDelete("/api/proyectos/{idVotacion}/{idProyecto}", (int idVotacion, int idProyecto, IDAL dal, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        var proyecto = dal.GetById<Proyecto>(idProyecto);
        if (proyecto == null) return Results.NotFound("Proyecto no encontrado");

        var usuarioAuth = dal.GetWhere<Usuario>(u => u.Username == username).FirstOrDefault();
        if (usuarioAuth == null) return Results.Unauthorized();

        var evento = proyecto.evento;
        if (evento == null) return Results.NotFound("Evento no encontrado");

        if (!EsOrganizadorDelEvento(dal, usuarioAuth.Id, evento.IdEvento)) return Results.Forbid();

        // Eliminar votos del proyecto (por si el cascade de BD no es suficiente con EF)
        var votos = dal.GetWhere<Voto>(v => v.ProyectoId == idProyecto).ToList();
        foreach (var voto in votos) dal.Delete<Voto>(voto);

        dal.Delete<Proyecto>(proyecto);
        dal.Commit();

        return Results.Ok();
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

// ── Endpoint de resultados reales ───────────────────────────────

app.MapGet("/api/resultados/{idVotacion}", (int idVotacion, IDAL dal, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        // Cargar votación y su evento (lazy loading de 2 tablas: seguro)
        var votacion = dal.GetById<Votacion>(idVotacion);
        if (votacion == null) return Results.NotFound("Votación no encontrada");

        var evento = votacion.evento;
        if (evento == null) return Results.NotFound("Evento no encontrado");

        // Cargar proyectos del evento (lazy loading desde evento)
        var proyectos = evento.proyectos?.ToList() ?? new List<Proyecto>();

        // Cargar votos de esta votación en memoria (consulta simple sin JOINs)
        var votos = dal.GetWhere<Voto>(v => v.VotacionId == idVotacion).ToList();
        var votosPorProyecto = votos.GroupBy(v => v.ProyectoId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Construir resultados
        var resultados = proyectos.Select(p =>
        {
            var votosProyecto = votosPorProyecto.TryGetValue(p.Id, out var vp) ? vp : new();
            var participantesAdicionales = string.IsNullOrWhiteSpace(p.ParticipantesAdicionales)
                ? new List<string>()
                : p.ParticipantesAdicionales.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(u => u.Trim()).Where(u => !string.IsNullOrEmpty(u)).ToList();
            return new ProyectoResultadoDTO
            {
                Id = p.Id,
                Nombre = p.Nombre ?? $"Proyecto #{p.Id}",
                Descripcion = p.Descripcion ?? "",
                Competidor = p.competidor?.usuario?.Username ?? "",
                Participantes = participantesAdicionales,
                Media = votosProyecto.Any() ? Math.Round(votosProyecto.Average(v => v.Valor), 2) : 0,
                NumVotos = votosProyecto.Count
            };
        })
        .OrderByDescending(r => r.Media)
        .Select((r, i) => { r.Rank = i + 1; return r; })
        .ToList();

        return Results.Ok(resultados);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ── Endpoint de monitoreo ───────────────────────────────────────

app.MapGet("/api/monitor/{idVotacion}", (int idVotacion, IDAL dal, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        // 1. Votos de esta votación (consulta simple por FK, sin JOINs problemáticos)
        var votos = dal.GetWhere<Voto>(v => v.VotacionId == idVotacion).ToList();

        // 2. IDs únicos de votantes y proyectos
        var votanteIds = votos.Select(v => v.VotanteId).Distinct().ToHashSet();

        // 3. Cargar Roles y Usuarios por separado en memoria para evitar JOIN de 3 tablas
        var rolesVotantes = dal.GetAll<Rol>().ToList()
            .Where(r => votanteIds.Contains(r.Id)).ToList();

        var userIds = rolesVotantes.Select(r => r.UsuarioId).Distinct().ToHashSet();
        var usuarios = dal.GetAll<Usuario>().ToList()
            .Where(u => userIds.Contains(u.Id)).ToList();

        var usernamePorRolId = rolesVotantes.ToDictionary(
            r => r.Id,
            r => usuarios.FirstOrDefault(u => u.Id == r.UsuarioId)?.Username ?? $"Votante #{r.Id}"
        );

        // 4. Cargar Jurados y Públicos de la votación para quién no ha votado
        var votacion = dal.GetById<Votacion>(idVotacion);

        var todosVotantes = new List<VotanteEstadoDTO>();
        try
        {
            var jurados = votacion?.jurados?.ToList() ?? new();
            var publicos = votacion?.publicos?.ToList() ?? new();

            // Cargar usernames de jurados/públicos
            var allRolIds = jurados.Select(j => j.Id).Concat(publicos.Select(p => p.Id)).ToHashSet();
            var allRoles = dal.GetAll<Rol>().ToList().Where(r => allRolIds.Contains(r.Id)).ToList();
            var allUserIds = allRoles.Select(r => r.UsuarioId).Distinct().ToHashSet();
            var allUsers = dal.GetAll<Usuario>().ToList().Where(u => allUserIds.Contains(u.Id)).ToList();
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
        catch { /* Si falla la carga relacional, continuamos sin la lista de votantes */ }

        // 5. Stats por proyecto (lazy loading proyecto.Nombre por lotes)
        var proyectoStats = votos
            .GroupBy(v => v.ProyectoId)
            .Select(g => new ProyectoMonitorDTO
            {
                Nombre = g.First().proyecto?.Nombre ?? $"Proyecto #{g.Key}",
                Media = Math.Round(g.Average(v => v.Valor), 2),
                NumVotos = g.Count()
            })
            .OrderByDescending(p => p.Media)
            .ToList();

        // 6. Historial cronológico
        var historial = votos
            .OrderByDescending(v => v.Fecha)
            .Select(v => new VotoHistorialDTO
            {
                Votante = usernamePorRolId.TryGetValue(v.VotanteId, out var name)
                    ? name : $"Votante #{v.VotanteId}",
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
            PromedioGeneral = votos.Any() ? Math.Round(votos.Average(v => v.Valor), 2) : 0,
            Proyectos = proyectoStats,
            Votantes = todosVotantes,
            Historial = historial
        };

        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ── Endpoint de IA ──────────────────────────────────────────────

app.MapPost("/api/ai/chat", async (AiChatRequest req, IConfiguration config, IHttpClientFactory httpFactory, IVotifyService service, HttpContext http) =>
{
    var apiKey = config["GeminiApiKey"] ?? "";
    if (string.IsNullOrEmpty(apiKey)) return Results.Problem("API key no configurada.");

    var geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent?key={apiKey}";

    // Contexto de eventos del usuario (si está autenticado)
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
        var errorBody = await response.Content.ReadAsStringAsync();
        return Results.Problem($"Error Gemini {(int)response.StatusCode}: {errorBody}");
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

app.Run();

// ── Helpers ─────────────────────────────────────────────────────

static string? ObtenerUsernameAutenticado(HttpContext http)
    => http.Session.GetString(SessionConfig.UsernameKey);

static bool EsOrganizadorDelEvento(IDAL dal, int usuarioId, int eventoId)
    => dal.GetWhere<Organizador>(r => r.UsuarioId == usuarioId && r.EventoId == eventoId).Any()
    || dal.GetWhere<EncargadoVotacion>(r => r.UsuarioId == usuarioId && r.EventoId == eventoId).Any();

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
record AiChatRequest(List<AiChatTurn> History, string Message);
record AiChatTurn(string Role, string Content);
record GuardarVotoRequest(int VotacionId, int ProyectoId, double Puntuacion, string? Comentario);
record CrearProyectoRequest(string Nombre, string? Descripcion, string UsernameCompetidor);
record ModificarProyectoRequest(string Nombre, string? Descripcion, List<string>? ParticipantesAdicionales);
