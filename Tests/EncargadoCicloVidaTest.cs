using System;
using System.Collections.Generic;
using Votify.BusinessLogic.Service;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-3938: Permitir al encargado intervenir en el ciclo de vida de una votación.
    ///
    /// Criterios verificados:
    ///   · Un ENCARGADO puede modificar una votación (fecha y estado).
    ///   · Un ENCARGADO puede cerrar una votación activa.
    ///   · Un usuario sin rol ENCARGADO no puede modificar la votación.
    ///   · Un usuario sin rol ENCARGADO no puede cerrar la votación.
    /// </summary>
    public static class EncargadoCicloVidaTest
    {
        // ── Identificadores fijos del escenario de prueba ──────────────────

        private const int IdUsuarioEncargado = 1;
        private const int IdUsuarioOtro = 2;
        private const int IdEvento = 10;
        private const int IdVotacion = 5;

        private const string UsernameEncargado = "encargado_test";
        private const string PasswordEncargado = "pass123";
        private const string UsernameOtro      = "otro_test";
        private const string PasswordOtro      = "pass456";

        // ── Contexto: agrupa el servicio y los repos accedidos por los tests ──

        private sealed record Contexto(
            VotifyService Service,
            InMemoryDAL<Votacion> VotacionRepo);

        // ── Construcción del servicio con repositorios en memoria ──────────

        private static Contexto CrearContextoConSesion(string username, string password)
        {
            var usuarioRepo   = new InMemoryDAL<Usuario>(u => u.Id);
            var votacionRepo  = new InMemoryDAL<Votacion>(v => v.Id);
            var encargadoRepo = new InMemoryDAL<EncargadoVotacion>(e => e.Id);

            PrepararDatos(usuarioRepo, votacionRepo, encargadoRepo);

            var repos = new VotifyRepositories(
                usuarioRepo,
                new InMemoryDAL<Voto>(v => v.Id),
                votacionRepo,
                new InMemoryDAL<Evento>(e => e.IdEvento),
                new InMemoryDAL<Rol>(),
                new InMemoryDAL<Proyecto>(p => p.Id),
                new InMemoryDAL<Jurado>(j => j.Id),
                new InMemoryDAL<Publico>(p => p.Id),
                new InMemoryDAL<Competidor>(c => c.Id),
                new InMemoryDAL<Organizador>(o => o.Id),
                encargadoRepo,
                new InMemoryDAL<Reclamacion>(r => r.Id));

            var service = new VotifyService(repos);
            service.LogIn(username, password);

            return new Contexto(service, votacionRepo);
        }

        private static void PrepararDatos(
            InMemoryDAL<Usuario> usuarioRepo,
            InMemoryDAL<Votacion> votacionRepo,
            InMemoryDAL<EncargadoVotacion> encargadoRepo)
        {
            usuarioRepo.Insert(new Usuario(UsernameEncargado, "enc@test.com", PasswordEncargado, IdUsuarioEncargado)
            {
                roles = new List<Rol>()
            });

            usuarioRepo.Insert(new Usuario(UsernameOtro, "otro@test.com", PasswordOtro, IdUsuarioOtro)
            {
                roles = new List<Rol>()
            });

            votacionRepo.Insert(new Votacion
            {
                Id = IdVotacion,
                EventoId = IdEvento,
                NombreEstado = "Activa",
                FechaIni = DateTime.Now.AddDays(-1),
                FechaFin = DateTime.Now.AddDays(30),
                Titulo = "Votacion de prueba",
                Descripcion = string.Empty
            });

            encargadoRepo.Insert(new EncargadoVotacion(DateTime.Now, 0)
            {
                Id = 1,
                UsuarioId = IdUsuarioEncargado,
                EventoId = IdEvento
            });
        }

        // ── Pruebas ────────────────────────────────────────────────────────

        public static (bool Success, string Message) EncargadoPuedeModificarVotacion()
        {
            var ctx = CrearContextoConSesion(UsernameEncargado, PasswordEncargado);

            ctx.Service.ModificarVotacion(IdVotacion, DateTime.Now.AddDays(60), estado: "Pausada");

            return ctx.VotacionRepo.GetById(IdVotacion).NombreEstado == "Pausada"
                ? (true, "El encargado puede modificar la votación (estado actualizado correctamente)")
                : (false, "El estado de la votación no cambió tras la modificación");
        }

        public static (bool Success, string Message) EncargadoPuedeCerrarVotacion()
        {
            var ctx = CrearContextoConSesion(UsernameEncargado, PasswordEncargado);

            ctx.Service.CerrarVotacion(IdVotacion);

            return ctx.VotacionRepo.GetById(IdVotacion).NombreEstado == "Cerrada"
                ? (true, "El encargado puede cerrar la votación (NombreEstado='Cerrada')")
                : (false, "La votación sigue activa tras llamar a CerrarVotacion");
        }

        public static (bool Success, string Message) UsuarioSinRolNoPuedeModificarVotacion()
        {
            var ctx = CrearContextoConSesion(UsernameOtro, PasswordOtro);

            return EsperarFallaDePermisos(
                () => ctx.Service.ModificarVotacion(IdVotacion, DateTime.Now.AddDays(60), estado: "Pausada"));
        }

        public static (bool Success, string Message) UsuarioSinRolNoPuedeCerrarVotacion()
        {
            var ctx = CrearContextoConSesion(UsernameOtro, PasswordOtro);

            return EsperarFallaDePermisos(() => ctx.Service.CerrarVotacion(IdVotacion));
        }

        // ── Helper de aserción ─────────────────────────────────────────────

        private static (bool Success, string Message) EsperarFallaDePermisos(Action accionProhibida)
        {
            try
            {
                accionProhibida();
                return (false, "Se esperaba ServiceException por falta de permisos, pero no se lanzó");
            }
            catch (ServiceException ex) when (ex.Message.Contains("permisos"))
            {
                return (true, "Acceso denegado correctamente a usuario sin rol de encargado");
            }
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-3938 — Permitir al encargado intervenir en el ciclo de vida de una votación",
            ("Encargado modifica votación",  EncargadoPuedeModificarVotacion),
            ("Encargado cierra votación",    EncargadoPuedeCerrarVotacion),
            ("Sin rol no puede modificar",   UsuarioSinRolNoPuedeModificarVotacion),
            ("Sin rol no puede cerrar",      UsuarioSinRolNoPuedeCerrarVotacion));
    }
}