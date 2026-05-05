using System;
using System.Linq;
using System.Data.Common;
using Npgsql;
using Votify.Presentation;
using Votify.Tests;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

if (Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--dbtest", StringComparison.OrdinalIgnoreCase)))
{
    var result = DBTest.Run();
    MessageBox.Show(
        result.Message,
        result.Success ? "DBTest correcto" : "DBTest con error",
        MessageBoxButtons.OK,
        result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    return;
}

if (Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--checkdb", StringComparison.OrdinalIgnoreCase)))
{
    var result = DBTest.CheckData();
    MessageBox.Show(
        result.Message,
        result.Success ? "Comprobacion BD" : "Comprobacion BD con error",
        MessageBoxButtons.OK,
        result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    return;
}

// Aqui ira la inicializacion real del DAL y el servicio
// IVotifyService service = new VotifyService(new EntityFrameworkDAL(new VotifyDBContext()));

app.MapGet("/api/proyectos/{idVotacion}/mis-comentarios", (int idVotacion, IDAL<Votacion> votacionRepo, IDAL<Voto> votoRepo, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();

    try
    {
        var votacion = votacionRepo.GetById(idVotacion);
        if (votacion == null) return Results.NotFound("Votación no encontrada");

        var proyecto = (votacion.evento?.proyectos ?? Enumerable.Empty<Proyecto>())
            .FirstOrDefault(p => p.competidor?.usuario?.Username == username && ProyectoPerteneceAVotacion(p, votacion));

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

record GuardarVotoRequest(int VotacionId, int ProyectoId, double Puntuacion, string? Comentario);
record CrearProyectoRequest(string Nombre, string? Descripcion, string? UsernameCompetidor);
record ModificarProyectoRequest(string Nombre, string? Descripcion, List<string>? ParticipantesAdicionales);

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
            Competidor = proyecto.competidor?.usuario?.Username ?? username,
            Categoria = ObtenerCategoriaProyecto(proyecto),
            Participantes = ObtenerParticipantesProyecto(proyecto).ToList(),
            Media = 0,
            NumVotos = 0,
            Rank = 0
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

        var proyectos = (evento?.proyectos ?? Enumerable.Empty<Proyecto>())
            .Where(p => ProyectoPerteneceAVotacion(p, votacion))
            .ToList();
        var votos = votoRepo.GetWhere(v => v.VotacionId == idVotacion).ToList();
        var votosPorProyecto = votos.GroupBy(v => v.ProyectoId).ToDictionary(g => g.Key, g => g.ToList());
        int totalVotantesEsperados = todosVotantes.Count;

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

    var participantesAdicionales = ObtenerParticipantesProyecto(proyecto).ToList();

    return new ProyectoResultadoDTO
    {
        Id = proyecto.Id,
        Nombre = proyecto.Nombre ?? $"Proyecto #{proyecto.Id}",
        Descripcion = proyecto.Descripcion ?? string.Empty,
        Competidor = proyecto.competidor?.usuario?.Username ?? string.Empty,
        Categoria = ObtenerCategoriaProyecto(proyecto),
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

static string ObtenerCategoriaProyecto(Proyecto proyecto)
{
    const string prefijoCategoriaProyecto = "__CAT__:";
    if (string.IsNullOrWhiteSpace(proyecto.ParticipantesAdicionales))
        return string.Empty;

    return proyecto.ParticipantesAdicionales
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim())
        .FirstOrDefault(p => p.StartsWith(prefijoCategoriaProyecto, StringComparison.OrdinalIgnoreCase))?
        .Substring(prefijoCategoriaProyecto.Length)
        .Trim()
        ?? string.Empty;
}

static IEnumerable<string> ObtenerParticipantesProyecto(Proyecto proyecto)
{
    const string prefijoCategoriaProyecto = "__CAT__:";
    if (string.IsNullOrWhiteSpace(proyecto.ParticipantesAdicionales))
        return Enumerable.Empty<string>();

    return proyecto.ParticipantesAdicionales
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Trim())
        .Where(p => !string.IsNullOrWhiteSpace(p) && !p.StartsWith(prefijoCategoriaProyecto, StringComparison.OrdinalIgnoreCase));
}

static bool ProyectoPerteneceAVotacion(Proyecto proyecto, Votacion votacion)
{
    var categoriaProyecto = ObtenerCategoriaProyecto(proyecto);
    if (string.IsNullOrWhiteSpace(categoriaProyecto))
        return true;

    return string.Equals(categoriaProyecto, votacion.Titulo?.Trim(), StringComparison.OrdinalIgnoreCase);
}

static string ConstruirDefinicionCategoria(CategoriaBaremoDTO categoria)
{
    string nombre = categoria.Nombre?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(nombre))
        return string.Empty;

    var criterios = categoria.Criterios ?? new List<CriterioDTO>();
    if (!criterios.Any())
        return nombre;

    string json = System.Text.Json.JsonSerializer.Serialize(criterios);
    string encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    return $"{nombre}||__CRITERIOS__:{encoded}";
}

static string ObtenerDescripcionVisibleVotacion(string? descripcion)
{
    if (string.IsNullOrWhiteSpace(descripcion))
        return string.Empty;

    int idx = descripcion.IndexOf("\n__CRITERIOS__:", StringComparison.Ordinal);
    return (idx >= 0 ? descripcion[..idx] : descripcion).Trim();
}

static List<CriterioDTO> ObtenerCriteriosDeVotacion(string? descripcion)
{
    if (string.IsNullOrWhiteSpace(descripcion))
        return new();

    int idx = descripcion.IndexOf("\n__CRITERIOS__:", StringComparison.Ordinal);
    if (idx < 0)
        return new();

    string encoded = descripcion[(idx + "\n__CRITERIOS__:".Length)..].Trim();
    if (string.IsNullOrWhiteSpace(encoded))
        return new();

    try
    {
        string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        return System.Text.Json.JsonSerializer.Deserialize<List<CriterioDTO>>(json) ?? new();
    }
    catch
    {
        return new();
    }
}

static List<CategoriaBaremoDTO> ObtenerCategoriasDeVotacion(Votacion votacion)
{
    var criterios = ObtenerCriteriosDeVotacion(votacion.Descripcion);
    if (!criterios.Any())
        return new();

    return new List<CategoriaBaremoDTO>
    {
        new()
        {
            Nombre = votacion.Titulo ?? string.Empty,
            Criterios = criterios
        }
    };
}

static ProyectoMonitorDTO BuildProjectMonitor(Proyecto proyecto, List<Voto> votosProyecto, Votacion votacion, int totalVotantesEsperados)
{

}

app.MapPost("/api/votaciones", (VotacionDTO req, IVotifyService service, HttpContext http) =>
{
    string? username = ObtenerUsernameAutenticado(http);
    if (username == null) return Results.Unauthorized();
    try
    {
        service.RestoreSession(username);
        int idVotacion = service.CrearVotacion(
            req.Titulo,
            req.Descripcion,
            req.FechaFin,
            true,
            req.PermiteCompetidoresVotar,
            req.PesoJurado,
            req.PesoPublico,
            req.Categorias?.Select(ConstruirDefinicionCategoria).ToList());
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

        var votaciones = service.GetAllVotaciones().OrderBy(v => v.FechaFin).Select(v => new VotacionDTO
        {
            Id = v.Id,
            IdEvento = v.EventoId,
            NombreEvento = v.evento?.Nombre ?? string.Empty,
            Descripcion = ObtenerDescripcionVisibleVotacion(v.Descripcion),
            Titulo = string.IsNullOrEmpty(v.Titulo) ? $"Votación #{v.Id}" : v.Titulo,
            FechaIni = v.FechaIni,
            FechaFin = v.FechaFin,
            Estado = v.Estado,
            PesoJurado = v.PesoJurado,
            PesoPublico = v.PesoPublico,
            Categorias = ObtenerCategoriasDeVotacion(v),
            RolActual = service.GetTipoRolDeUsuario(usuarioActual.Id, v.EventoId)
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

        var votaciones = service.GetVotacionesByEvento(idEvento)
            .OrderBy(v => v.FechaFin)
            .Select(v => new VotacionDTO
            {
                Id = v.Id,
                IdEvento = v.EventoId,
                NombreEvento = v.evento?.Nombre ?? string.Empty,
                Titulo = string.IsNullOrEmpty(v.Titulo) ? $"Votación #{v.Id}" : v.Titulo,
                Descripcion = ObtenerDescripcionVisibleVotacion(v.Descripcion),
                FechaIni = v.FechaIni,
                FechaFin = v.FechaFin,
                Estado = v.Estado,
                PesoJurado = v.PesoJurado,
                PesoPublico = v.PesoPublico,
                Categorias = ObtenerCategoriasDeVotacion(v),
                RolActual = service.GetTipoRolDeUsuario(usuarioActual.Id, v.EventoId)
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
            Descripcion = ObtenerDescripcionVisibleVotacion(votacion.Descripcion),
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
