using System;
using System.Collections.Generic;
using Votify.BusinessLogic.Service;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptacion: aplicar patron Proxy para proteger operaciones sensibles.
    ///
    /// Criterios verificados:
    ///   - El proxy implementa la misma interfaz que el servicio real.
    ///   - Sin sesion, el proxy bloquea antes de ejecutar la operacion real.
    ///   - Un usuario sin permisos no puede cerrar votaciones ni responder reclamaciones.
    ///   - Encargado u organizador pueden ejecutar operaciones permitidas.
    /// </summary>
    public static class PatronProxyTest
    {
        private const int IdUsuarioEncargado = 1;
        private const int IdUsuarioPublico = 2;
        private const int IdUsuarioOrganizador = 3;
        private const int IdEvento = 10;
        private const int IdVotacion = 20;
        private const int IdReclamacion = 30;
        private const int IdRolEncargado = 100;
        private const int IdRolOrganizador = 101;
        private const int IdRolPublico = 102;

        private sealed record Contexto(
            VotifyServiceProxy Proxy,
            InMemoryDAL<Votacion> Votaciones,
            InMemoryDAL<Reclamacion> Reclamaciones);

        public static (bool Success, string Message) ProxyImplementaMismaInterfaz()
        {
            var ctx = CrearContexto();
            bool ok = ctx.Proxy is IVotifyService;

            return ok
                ? (true, "El proxy puede sustituir a VotifyService mediante IVotifyService")
                : (false, "El proxy no implementa la interfaz del servicio");
        }

        public static (bool Success, string Message) SinSesionBloqueaAntesDeDelegar()
        {
            var ctx = CrearContexto();

            try
            {
                ctx.Proxy.CerrarVotacion(IdVotacion);
                return (false, "El proxy permitio cerrar votacion sin sesion");
            }
            catch (ServiceException)
            {
                bool sinCambios = ctx.Votaciones.GetById(IdVotacion).NombreEstado == "Activa";
                return sinCambios
                    ? (true, "El proxy bloqueo la operacion sin sesion y no delego al servicio real")
                    : (false, "La votacion cambio aunque el proxy debia bloquear la operacion");
            }
        }

        public static (bool Success, string Message) UsuarioSinPermisosNoCierraVotacion()
        {
            var ctx = CrearContexto("publico", "pass123");

            try
            {
                ctx.Proxy.CerrarVotacion(IdVotacion);
                return (false, "Un usuario publico pudo cerrar la votacion");
            }
            catch (ServiceException ex) when (ex.Message.Contains("permisos"))
            {
                bool sinCambios = ctx.Votaciones.GetById(IdVotacion).NombreEstado == "Activa";
                return sinCambios
                    ? (true, "El proxy denego cierre a usuario sin rol gestor")
                    : (false, "La votacion cambio aunque el proxy denego el acceso");
            }
        }

        public static (bool Success, string Message) EncargadoPuedeCerrarVotacion()
        {
            var ctx = CrearContexto("encargado", "pass123");

            ctx.Proxy.CerrarVotacion(IdVotacion);

            return ctx.Votaciones.GetById(IdVotacion).NombreEstado == "Cerrada"
                ? (true, "El proxy permitio cerrar votacion a un encargado")
                : (false, "El encargado fue autorizado pero la votacion no se cerro");
        }

        public static (bool Success, string Message) OrganizadorPuedeModificarVotacion()
        {
            var ctx = CrearContexto("organizador", "pass123");
            var nuevaFecha = DateTime.Now.AddDays(45);

            ctx.Proxy.ModificarVotacion(IdVotacion, nuevaFecha, estado: "Pausada");
            var votacion = ctx.Votaciones.GetById(IdVotacion);

            bool ok = votacion.NombreEstado == "Pausada" && votacion.FechaFin == nuevaFecha;
            return ok
                ? (true, "El proxy permitio modificar votacion al organizador")
                : (false, "La modificacion autorizada no se aplico correctamente");
        }

        public static (bool Success, string Message) SoloOrganizadorRespondeReclamacion()
        {
            var ctxPublico = CrearContexto("publico", "pass123");

            try
            {
                ctxPublico.Proxy.ResponderReclamacion(IdReclamacion, Reclamacion.EstadoResuelta, "Respuesta");
                return (false, "Un usuario publico pudo responder una reclamacion");
            }
            catch (ServiceException ex) when (ex.Message.Contains("permisos"))
            {
            }

            var ctxOrganizador = CrearContexto("organizador", "pass123");
            ctxOrganizador.Proxy.ResponderReclamacion(IdReclamacion, Reclamacion.EstadoResuelta, "Respuesta valida");
            var reclamacion = ctxOrganizador.Reclamaciones.GetById(IdReclamacion);

            bool ok = reclamacion.Estado == Reclamacion.EstadoResuelta
                && reclamacion.RespuestaOrganizador == "Respuesta valida";

            return ok
                ? (true, "El proxy bloqueo al publico y permitio responder al organizador")
                : (false, "La respuesta autorizada de reclamacion no se guardo");
        }

        private static Contexto CrearContexto(string? username = null, string? password = null)
        {
            var usuarioRepo = new InMemoryDAL<Usuario>(u => u.Id);
            var votoRepo = new InMemoryDAL<Voto>(v => v.Id);
            var votacionRepo = new InMemoryDAL<Votacion>(v => v.Id);
            var eventoRepo = new InMemoryDAL<Evento>(e => e.IdEvento);
            var rolRepo = new InMemoryDAL<Rol>(r => r.Id);
            var proyectoRepo = new InMemoryDAL<Proyecto>(p => p.Id);
            var juradoRepo = new InMemoryDAL<Jurado>(j => j.Id);
            var publicoRepo = new InMemoryDAL<Publico>(p => p.Id);
            var competidorRepo = new InMemoryDAL<Competidor>(c => c.Id);
            var organizadorRepo = new InMemoryDAL<Organizador>(o => o.Id);
            var encargadoRepo = new InMemoryDAL<EncargadoVotacion>(e => e.Id);
            var reclamacionRepo = new InMemoryDAL<Reclamacion>(r => r.Id);

            PrepararDatos(
                usuarioRepo,
                votacionRepo,
                eventoRepo,
                rolRepo,
                publicoRepo,
                organizadorRepo,
                encargadoRepo,
                reclamacionRepo);

            var repos = new VotifyRepositories(
                usuarioRepo,
                votoRepo,
                votacionRepo,
                eventoRepo,
                rolRepo,
                proyectoRepo,
                juradoRepo,
                publicoRepo,
                competidorRepo,
                organizadorRepo,
                encargadoRepo,
                reclamacionRepo);

            var realService = new VotifyService(repos);
            var proxy = new VotifyServiceProxy(realService, repos);

            if (!string.IsNullOrWhiteSpace(username) && password != null)
                proxy.LogIn(username, password);

            return new Contexto(proxy, votacionRepo, reclamacionRepo);
        }

        private static void PrepararDatos(
            InMemoryDAL<Usuario> usuarioRepo,
            InMemoryDAL<Votacion> votacionRepo,
            InMemoryDAL<Evento> eventoRepo,
            InMemoryDAL<Rol> rolRepo,
            InMemoryDAL<Publico> publicoRepo,
            InMemoryDAL<Organizador> organizadorRepo,
            InMemoryDAL<EncargadoVotacion> encargadoRepo,
            InMemoryDAL<Reclamacion> reclamacionRepo)
        {
            var encargado = new Usuario("encargado", "encargado@test.com", "pass123", IdUsuarioEncargado)
            {
                roles = new List<Rol>()
            };
            var publico = new Usuario("publico", "publico@test.com", "pass123", IdUsuarioPublico)
            {
                roles = new List<Rol>()
            };
            var organizador = new Usuario("organizador", "organizador@test.com", "pass123", IdUsuarioOrganizador)
            {
                roles = new List<Rol>()
            };

            usuarioRepo.Insert(encargado);
            usuarioRepo.Insert(publico);
            usuarioRepo.Insert(organizador);

            var evento = new Evento
            {
                IdEvento = IdEvento,
                OrganizadorId = IdUsuarioOrganizador,
                Nombre = "Evento Proxy",
                Descripcion = "Evento para probar el proxy",
                FechaIni = DateTime.Now.AddDays(-2),
                FechaFin = DateTime.Now.AddDays(30),
                codigoEncargado = "ENC",
                codigoJurado = "JUR"
            };
            eventoRepo.Insert(evento);

            var rolEncargado = new EncargadoVotacion(DateTime.Now, 0)
            {
                Id = IdRolEncargado,
                UsuarioId = IdUsuarioEncargado,
                EventoId = IdEvento,
                usuario = encargado,
                evento = evento
            };
            var rolOrganizador = new Organizador(DateTime.Now, 0)
            {
                Id = IdRolOrganizador,
                UsuarioId = IdUsuarioOrganizador,
                EventoId = IdEvento,
                usuario = organizador,
                evento = evento
            };
            var rolPublico = new Publico(DateTime.Now, 0)
            {
                Id = IdRolPublico,
                UsuarioId = IdUsuarioPublico,
                EventoId = IdEvento,
                usuario = publico,
                evento = evento
            };

            encargadoRepo.Insert(rolEncargado);
            organizadorRepo.Insert(rolOrganizador);
            publicoRepo.Insert(rolPublico);
            rolRepo.Insert(rolEncargado);
            rolRepo.Insert(rolOrganizador);
            rolRepo.Insert(rolPublico);

            votacionRepo.Insert(new Votacion
            {
                Id = IdVotacion,
                EventoId = IdEvento,
                EncargadoId = IdRolEncargado,
                NombreEstado = "Activa",
                FechaIni = DateTime.Now.AddDays(-1),
                FechaFin = DateTime.Now.AddDays(20),
                Titulo = "Votacion Proxy",
                Descripcion = string.Empty
            });

            reclamacionRepo.Insert(new Reclamacion
            {
                Id = IdReclamacion,
                EventoId = IdEvento,
                UsuarioId = IdUsuarioPublico,
                Descripcion = "Necesito revision",
                FechaCreacion = DateTime.Now,
                Estado = Reclamacion.EstadoPendiente,
                evento = evento,
                usuario = publico
            });
        }

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT - Patron Proxy para control de acceso",
            ("Proxy sustituye al servicio", ProxyImplementaMismaInterfaz),
            ("Bloqueo sin sesion", SinSesionBloqueaAntesDeDelegar),
            ("Bloqueo sin permisos", UsuarioSinPermisosNoCierraVotacion),
            ("Encargado autorizado", EncargadoPuedeCerrarVotacion),
            ("Organizador autorizado", OrganizadorPuedeModificarVotacion),
            ("Reclamacion protegida", SoloOrganizadorRespondeReclamacion));
    }
}
