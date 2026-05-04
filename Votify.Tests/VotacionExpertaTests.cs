using System.Linq.Expressions;
using Moq;
using Votify.BusinessLogic.Service;
using Votify.Entities;
using Votify.Persistence;
using Xunit;

namespace Votify.Tests;

/// <summary>
/// UT-3934: Garantizar el anonimato y la integridad de las valoraciones expertas.
///
/// Anonimato: un jurado no puede acceder a los votos de otro votante,
/// y el sistema nunca expone la identidad del votante en la respuesta.
///
/// Integridad: cada voto es único por (votante, proyecto, votación),
/// solo los roles autorizados pueden votar, y el rango de puntuación es 0-10.
/// </summary>
public class VotacionExpertaTests
{
    // ── Mocks de repositorios ──────────────────────────────────────────────
    private readonly Mock<IDAL<Usuario>>           _usuarioRepo    = new();
    private readonly Mock<IDAL<Voto>>              _votoRepo       = new();
    private readonly Mock<IDAL<Votacion>>          _votacionRepo   = new();
    private readonly Mock<IDAL<Evento>>            _eventoRepo     = new();
    private readonly Mock<IDAL<Rol>>               _rolRepo        = new();
    private readonly Mock<IDAL<Proyecto>>          _proyectoRepo   = new();
    private readonly Mock<IDAL<Jurado>>            _juradoRepo     = new();
    private readonly Mock<IDAL<Publico>>           _publicoRepo    = new();
    private readonly Mock<IDAL<Competidor>>        _competidorRepo = new();
    private readonly Mock<IDAL<Organizador>>       _organizadorRepo = new();
    private readonly Mock<IDAL<EncargadoVotacion>> _encargadoRepo  = new();

    // ── Datos de prueba base ──────────────────────────────────────────────
    private readonly Usuario _usuarioJurado = new() { Id = 1, Username = "jurado1" };
    private readonly Evento  _evento        = new() { IdEvento = 10, PermiteCompetidoresVotar = false };

    private Votacion CrearVotacion() => new()
    {
        Id       = 5,
        EventoId = 10,
        evento   = _evento
    };

    private Proyecto CrearProyecto() => new() { Id = 3, EventoId = 10 };

    // ── Factory del servicio ──────────────────────────────────────────────
    private VotifyService CrearServicio() => new(
        _usuarioRepo.Object,
        _votoRepo.Object,
        _votacionRepo.Object,
        _eventoRepo.Object,
        _rolRepo.Object,
        _proyectoRepo.Object,
        _juradoRepo.Object,
        _publicoRepo.Object,
        _competidorRepo.Object,
        _organizadorRepo.Object,
        _encargadoRepo.Object
    );

    // ── Helpers de configuración ──────────────────────────────────────────

    private void ConfigurarSesion(Usuario usuario)
    {
        _usuarioRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Usuario, bool>>>()))
                    .Returns(new List<Usuario> { usuario });
        // Commit() del servicio delega a _usuarioRepository.Commit()
        _usuarioRepo.Setup(r => r.Commit());
    }

    private Jurado ConfigurarRolJurado(Usuario usuario, int eventoId)
    {
        var jurado = new Jurado { Id = 100, UsuarioId = usuario.Id, EventoId = eventoId };
        _juradoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Jurado, bool>>>()))
                   .Returns(new List<Jurado> { jurado });
        _publicoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Publico, bool>>>()))
                    .Returns(new List<Publico>());
        _competidorRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Competidor, bool>>>()))
                       .Returns(new List<Competidor>());
        _organizadorRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Organizador, bool>>>()))
                        .Returns(new List<Organizador>());
        _encargadoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<EncargadoVotacion, bool>>>()))
                      .Returns(new List<EncargadoVotacion>());
        return jurado;
    }

    private void ConfigurarSinVotoExistente()
    {
        _votoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Voto, bool>>>()))
                 .Returns(new List<Voto>());
    }

    private void ConfigurarVotoExistente(Jurado jurado, Votacion votacion, Proyecto proyecto)
    {
        var votoExistente = new Voto { Id = 99, VotanteId = jurado.Id, VotacionId = votacion.Id, ProyectoId = proyecto.Id };
        _votoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Voto, bool>>>()))
                 .Returns(new List<Voto> { votoExistente });
    }

    // ══════════════════════════════════════════════════════════════════════
    // ANONIMATO
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GetMisVotos filtra por el VotanteId del rol del usuario autenticado.
    /// Un usuario nunca recibe los proyectos votados por otro usuario.
    /// </summary>
    [Fact]
    public void GetMisVotos_SoloDevuelveProyectosVotadosPorElUsuarioActual()
    {
        var votacion  = CrearVotacion();
        var jurado    = ConfigurarRolJurado(_usuarioJurado, _evento.IdEvento);
        ConfigurarSesion(_usuarioJurado);

        _votacionRepo.Setup(r => r.GetById(votacion.Id)).Returns(votacion);

        // Voto del jurado actual: proyecto 3
        // Voto de otro votante (id 999): proyecto 7 — no debe aparecer
        var votoPropio  = new Voto { Id = 1, VotanteId = jurado.Id,  VotacionId = votacion.Id, ProyectoId = 3 };
        var votoAjeno   = new Voto { Id = 2, VotanteId = 999,         VotacionId = votacion.Id, ProyectoId = 7 };

        // El repositorio devuelve ambos votos; el filtro debe aplicarlo el servicio
        _votoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Voto, bool>>>()))
                 .Returns((Expression<Func<Voto, bool>> pred) =>
                     new List<Voto> { votoPropio, votoAjeno }.Where(pred.Compile()).ToList());

        var servicio = CrearServicio();
        servicio.RestoreSession(_usuarioJurado.Username);

        var resultado = servicio.GetMisVotos(votacion.Id);

        Assert.Single(resultado);
        Assert.Equal(3, resultado[0]);
        Assert.DoesNotContain(7, resultado);
    }

    /// <summary>
    /// GuardarVoto no retorna ningún dato del votante: la operación es void.
    /// La identidad del jurado nunca queda expuesta en la respuesta.
    /// </summary>
    [Fact]
    public void GuardarVoto_NoExponeIdentidadDelVotante_OperacionEsVoid()
    {
        // Si GuardarVoto devolviera algo (datos del votante), este test no compilaría.
        // La verificación es estática: el tipo de retorno del método es void.
        var metodo = typeof(IVotifyService).GetMethod(nameof(IVotifyService.GuardarVoto));

        Assert.NotNull(metodo);
        Assert.Equal(typeof(void), metodo!.ReturnType);
    }

    // ══════════════════════════════════════════════════════════════════════
    // INTEGRIDAD — prevención de voto doble
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Un jurado que intenta votar dos veces al mismo proyecto en la misma votación
    /// recibe una ServiceException y el segundo voto no se persiste.
    /// </summary>
    [Fact]
    public void GuardarVoto_LanzaExcepcion_CuandoJuradoVotaDosVecesAlMismoProyecto()
    {
        var votacion = CrearVotacion();
        var proyecto = CrearProyecto();
        var jurado   = ConfigurarRolJurado(_usuarioJurado, _evento.IdEvento);
        ConfigurarSesion(_usuarioJurado);
        _votacionRepo.Setup(r => r.GetById(votacion.Id)).Returns(votacion);
        _proyectoRepo.Setup(r => r.GetById(proyecto.Id)).Returns(proyecto);
        ConfigurarVotoExistente(jurado, votacion, proyecto);

        var servicio = CrearServicio();
        servicio.RestoreSession(_usuarioJurado.Username);

        var ex = Assert.Throws<ServiceException>(() =>
            servicio.GuardarVoto(votacion.Id, proyecto.Id, 7.5, null));

        Assert.Contains("Ya has votado", ex.Message);
        _votoRepo.Verify(r => r.Insert(It.IsAny<Voto>()), Times.Never);
    }

    /// <summary>
    /// Cada voto del jurado queda registrado exactamente una vez en el repositorio.
    /// </summary>
    [Fact]
    public void GuardarVoto_InsertaVotoExactamenteUnaVez_CuandoJuradoVotaPrimeraVez()
    {
        var votacion = CrearVotacion();
        var proyecto = CrearProyecto();
        ConfigurarRolJurado(_usuarioJurado, _evento.IdEvento);
        ConfigurarSesion(_usuarioJurado);
        _votacionRepo.Setup(r => r.GetById(votacion.Id)).Returns(votacion);
        _proyectoRepo.Setup(r => r.GetById(proyecto.Id)).Returns(proyecto);
        ConfigurarSinVotoExistente();

        var servicio = CrearServicio();
        servicio.RestoreSession(_usuarioJurado.Username);
        servicio.GuardarVoto(votacion.Id, proyecto.Id, 8.0, "Buen proyecto");

        _votoRepo.Verify(r => r.Insert(It.IsAny<Voto>()), Times.Once);
    }

    // ══════════════════════════════════════════════════════════════════════
    // INTEGRIDAD — control de roles
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Un organizador no puede emitir valoraciones expertas.
    /// </summary>
    [Fact]
    public void GuardarVoto_LanzaExcepcion_CuandoOrganizadorIntentaVotar()
    {
        var votacion    = CrearVotacion();
        var proyecto    = CrearProyecto();
        var organizador = new Organizador { Id = 200, UsuarioId = _usuarioJurado.Id, EventoId = _evento.IdEvento };

        ConfigurarSesion(_usuarioJurado);
        _votacionRepo.Setup(r => r.GetById(votacion.Id)).Returns(votacion);
        _proyectoRepo.Setup(r => r.GetById(proyecto.Id)).Returns(proyecto);
        _juradoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Jurado, bool>>>()))
                   .Returns(new List<Jurado>());
        _publicoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Publico, bool>>>()))
                    .Returns(new List<Publico>());
        _competidorRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Competidor, bool>>>()))
                       .Returns(new List<Competidor>());
        _organizadorRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Organizador, bool>>>()))
                        .Returns(new List<Organizador> { organizador });
        _encargadoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<EncargadoVotacion, bool>>>()))
                      .Returns(new List<EncargadoVotacion>());

        var servicio = CrearServicio();
        servicio.RestoreSession(_usuarioJurado.Username);

        var ex = Assert.Throws<ServiceException>(() =>
            servicio.GuardarVoto(votacion.Id, proyecto.Id, 5.0, null));

        Assert.Contains("rol actual no puede votar", ex.Message);
        _votoRepo.Verify(r => r.Insert(It.IsAny<Voto>()), Times.Never);
    }

    /// <summary>
    /// Un encargado de votación no puede emitir valoraciones expertas.
    /// </summary>
    [Fact]
    public void GuardarVoto_LanzaExcepcion_CuandoEncargadoIntentaVotar()
    {
        var votacion   = CrearVotacion();
        var proyecto   = CrearProyecto();
        var encargado  = new EncargadoVotacion { Id = 300, UsuarioId = _usuarioJurado.Id, EventoId = _evento.IdEvento };

        ConfigurarSesion(_usuarioJurado);
        _votacionRepo.Setup(r => r.GetById(votacion.Id)).Returns(votacion);
        _proyectoRepo.Setup(r => r.GetById(proyecto.Id)).Returns(proyecto);
        _juradoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Jurado, bool>>>()))
                   .Returns(new List<Jurado>());
        _publicoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Publico, bool>>>()))
                    .Returns(new List<Publico>());
        _competidorRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Competidor, bool>>>()))
                       .Returns(new List<Competidor>());
        _organizadorRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Organizador, bool>>>()))
                        .Returns(new List<Organizador>());
        _encargadoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<EncargadoVotacion, bool>>>()))
                      .Returns(new List<EncargadoVotacion> { encargado });

        var servicio = CrearServicio();
        servicio.RestoreSession(_usuarioJurado.Username);

        var ex = Assert.Throws<ServiceException>(() =>
            servicio.GuardarVoto(votacion.Id, proyecto.Id, 5.0, null));

        Assert.Contains("rol actual no puede votar", ex.Message);
        _votoRepo.Verify(r => r.Insert(It.IsAny<Voto>()), Times.Never);
    }

    /// <summary>
    /// Un competidor no puede votar cuando el evento lo prohíbe.
    /// </summary>
    [Fact]
    public void GuardarVoto_LanzaExcepcion_CuandoCompetidorVotaYEventoLoProhibe()
    {
        var eventoSinVotoCompetidor = new Evento { IdEvento = 10, PermiteCompetidoresVotar = false };
        var votacion    = new Votacion { Id = 5, EventoId = 10, evento = eventoSinVotoCompetidor };
        var proyecto    = CrearProyecto();
        var competidor  = new Competidor { Id = 400, UsuarioId = _usuarioJurado.Id, EventoId = eventoSinVotoCompetidor.IdEvento };

        ConfigurarSesion(_usuarioJurado);
        _votacionRepo.Setup(r => r.GetById(votacion.Id)).Returns(votacion);
        _proyectoRepo.Setup(r => r.GetById(proyecto.Id)).Returns(proyecto);
        _juradoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Jurado, bool>>>()))
                   .Returns(new List<Jurado>());
        _publicoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Publico, bool>>>()))
                    .Returns(new List<Publico>());
        _competidorRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Competidor, bool>>>()))
                       .Returns(new List<Competidor> { competidor });

        var servicio = CrearServicio();
        servicio.RestoreSession(_usuarioJurado.Username);

        var ex = Assert.Throws<ServiceException>(() =>
            servicio.GuardarVoto(votacion.Id, proyecto.Id, 5.0, null));

        Assert.Contains("competidores no pueden votar", ex.Message);
        _votoRepo.Verify(r => r.Insert(It.IsAny<Voto>()), Times.Never);
    }

    /// <summary>
    /// Un usuario sin rol asignado en el evento no puede votar.
    /// </summary>
    [Fact]
    public void GuardarVoto_LanzaExcepcion_CuandoUsuarioNoTieneRolEnEvento()
    {
        var votacion = CrearVotacion();
        var proyecto = CrearProyecto();

        ConfigurarSesion(_usuarioJurado);
        _votacionRepo.Setup(r => r.GetById(votacion.Id)).Returns(votacion);
        _proyectoRepo.Setup(r => r.GetById(proyecto.Id)).Returns(proyecto);
        _juradoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Jurado, bool>>>()))
                   .Returns(new List<Jurado>());
        _publicoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Publico, bool>>>()))
                    .Returns(new List<Publico>());
        _competidorRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Competidor, bool>>>()))
                       .Returns(new List<Competidor>());
        _organizadorRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<Organizador, bool>>>()))
                        .Returns(new List<Organizador>());
        _encargadoRepo.Setup(r => r.GetWhere(It.IsAny<Expression<Func<EncargadoVotacion, bool>>>()))
                      .Returns(new List<EncargadoVotacion>());

        var servicio = CrearServicio();
        servicio.RestoreSession(_usuarioJurado.Username);

        var ex = Assert.Throws<ServiceException>(() =>
            servicio.GuardarVoto(votacion.Id, proyecto.Id, 5.0, null));

        Assert.Contains("rol asignado", ex.Message);
        _votoRepo.Verify(r => r.Insert(It.IsAny<Voto>()), Times.Never);
    }

    // ══════════════════════════════════════════════════════════════════════
    // INTEGRIDAD — rango de puntuación y longitud de comentario
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Una puntuación fuera del rango 0-10 es rechazada por la entidad Voto.
    /// La valoración no se persiste.
    /// </summary>
    [Theory]
    [InlineData(-1.0)]
    [InlineData(10.1)]
    [InlineData(100.0)]
    public void GuardarVoto_LanzaExcepcion_CuandoPuntuacionEstaFueraDeRango(double puntuacionInvalida)
    {
        var votacion = CrearVotacion();
        var proyecto = CrearProyecto();
        ConfigurarRolJurado(_usuarioJurado, _evento.IdEvento);
        ConfigurarSesion(_usuarioJurado);
        _votacionRepo.Setup(r => r.GetById(votacion.Id)).Returns(votacion);
        _proyectoRepo.Setup(r => r.GetById(proyecto.Id)).Returns(proyecto);
        ConfigurarSinVotoExistente();

        var servicio = CrearServicio();
        servicio.RestoreSession(_usuarioJurado.Username);

        Assert.ThrowsAny<Exception>(() =>
            servicio.GuardarVoto(votacion.Id, proyecto.Id, puntuacionInvalida, null));

        _votoRepo.Verify(r => r.Insert(It.IsAny<Voto>()), Times.Never);
    }

    /// <summary>
    /// Un comentario que supera los 500 caracteres es rechazado por el servicio
    /// antes de llegar al repositorio.
    /// </summary>
    [Fact]
    public void GuardarVoto_LanzaExcepcion_CuandoComentarioSuperaLongitudMaxima()
    {
        var votacion        = CrearVotacion();
        var proyecto        = CrearProyecto();
        var comentarioLargo = new string('x', 501);
        ConfigurarRolJurado(_usuarioJurado, _evento.IdEvento);
        ConfigurarSesion(_usuarioJurado);
        _votacionRepo.Setup(r => r.GetById(votacion.Id)).Returns(votacion);
        _proyectoRepo.Setup(r => r.GetById(proyecto.Id)).Returns(proyecto);

        var servicio = CrearServicio();
        servicio.RestoreSession(_usuarioJurado.Username);

        var ex = Assert.Throws<ServiceException>(() =>
            servicio.GuardarVoto(votacion.Id, proyecto.Id, 7.0, comentarioLargo));

        Assert.Contains("500", ex.Message);
        _votoRepo.Verify(r => r.Insert(It.IsAny<Voto>()), Times.Never);
    }
}
