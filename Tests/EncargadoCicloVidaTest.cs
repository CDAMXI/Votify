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
        // ── Constantes de prueba ───────────────────────────────────────────

        private const int IdUsuarioEncargado = 1;
        private const int IdUsuarioOtro      = 2;
        private const int IdEvento           = 10;
        private const int IdVotacion         = 5;

        // ── Construcción del servicio con repositorios en memoria ──────────

        private static VotifyService CrearServicio(
            out InMemoryDAL<Usuario> usuarioRepo,
            out InMemoryDAL<Votacion> votacionRepo,
            out InMemoryDAL<EncargadoVotacion> encargadoRepo)
        {
            usuarioRepo   = new InMemoryDAL<Usuario>(u => u.Id);
            votacionRepo  = new InMemoryDAL<Votacion>(v => v.Id);
            encargadoRepo = new InMemoryDAL<EncargadoVotacion>(e => e.Id);

            return new VotifyService(
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
        }

        // ── Datos comunes ──────────────────────────────────────────────────

        private static void PrepararDatos(
            InMemoryDAL<Usuario> usuarioRepo,
            InMemoryDAL<Votacion> votacionRepo,
            InMemoryDAL<EncargadoVotacion> encargadoRepo)
        {
            // Usuario con rol de encargado
            usuarioRepo.Insert(new Usuario("encargado_test", "enc@test.com", "pass123", IdUsuarioEncargado)
            {
                roles = new List<Rol>()
            });

            // Usuario sin rol de encargado en el evento
            usuarioRepo.Insert(new Usuario("otro_test", "otro@test.com", "pass456", IdUsuarioOtro)
            {
                roles = new List<Rol>()
            });

            // Votación activa asociada al evento
            votacionRepo.Insert(new Votacion
            {
                Id          = IdVotacion,
                EventoId    = IdEvento,
                Estado      = true,
                FechaIni    = DateTime.Now.AddDays(-1),
                FechaFin    = DateTime.Now.AddDays(30),
                Titulo      = "Votacion de prueba",
                Descripcion = string.Empty
            });

            // Encargado del evento (vinculado al usuario 1)
            encargadoRepo.Insert(new EncargadoVotacion(DateTime.Now, 0)
            {
                Id        = 1,
                UsuarioId = IdUsuarioEncargado,
                EventoId  = IdEvento
            });
        }

        // ── Pruebas ────────────────────────────────────────────────────────

        /// <summary>
        /// Un encargado puede cambiar la fecha de fin y el estado de la votación.
        /// </summary>
        public static (bool Success, string Message) Test_Encargado_PuedeModificarVotacion()
        {
            try
            {
                var service = CrearServicio(out var usuarioRepo, out var votacionRepo, out var encargadoRepo);
                PrepararDatos(usuarioRepo, votacionRepo, encargadoRepo);

                service.LogIn("encargado_test", "pass123");
                service.ModificarVotacion(IdVotacion, DateTime.Now.AddDays(60), false);

                var votacion = votacionRepo.GetById(IdVotacion);
                return votacion.Estado == false
                    ? (true, "El encargado puede modificar la votación (estado actualizado correctamente)")
                    : (false, "El estado de la votación no cambió tras la modificación");
            }
            catch (Exception ex) { return (false, $"Error inesperado: {ex.Message}"); }
        }

        /// <summary>
        /// Un encargado puede cerrar una votación activa (Estado pasa a false).
        /// </summary>
        public static (bool Success, string Message) Test_Encargado_PuedeCerrarVotacion()
        {
            try
            {
                var service = CrearServicio(out var usuarioRepo, out var votacionRepo, out var encargadoRepo);
                PrepararDatos(usuarioRepo, votacionRepo, encargadoRepo);

                service.LogIn("encargado_test", "pass123");
                service.CerrarVotacion(IdVotacion);

                var votacion = votacionRepo.GetById(IdVotacion);
                return votacion.Estado == false
                    ? (true, "El encargado puede cerrar la votación (Estado=false)")
                    : (false, "La votación sigue activa tras llamar a CerrarVotacion");
            }
            catch (Exception ex) { return (false, $"Error inesperado: {ex.Message}"); }
        }

        /// <summary>
        /// Un usuario sin rol ENCARGADO no puede modificar la votación.
        /// Debe lanzar ServiceException con mensaje de permisos.
        /// </summary>
        public static (bool Success, string Message) Test_UsuarioSinRol_NoPuedeModificarVotacion()
        {
            try
            {
                var service = CrearServicio(out var usuarioRepo, out var votacionRepo, out var encargadoRepo);
                PrepararDatos(usuarioRepo, votacionRepo, encargadoRepo);

                service.LogIn("otro_test", "pass456");
                try
                {
                    service.ModificarVotacion(IdVotacion, DateTime.Now.AddDays(60), false);
                    return (false, "Se esperaba ServiceException por falta de permisos, pero no se lanzó");
                }
                catch (ServiceException ex) when (ex.Message.Contains("permisos"))
                {
                    return (true, "Acceso denegado correctamente a usuario sin rol de encargado");
                }
            }
            catch (Exception ex) { return (false, $"Error inesperado: {ex.Message}"); }
        }

        /// <summary>
        /// Un usuario sin rol ENCARGADO no puede cerrar la votación.
        /// Debe lanzar ServiceException con mensaje de permisos.
        /// </summary>
        public static (bool Success, string Message) Test_UsuarioSinRol_NoPuedeCerrarVotacion()
        {
            try
            {
                var service = CrearServicio(out var usuarioRepo, out var votacionRepo, out var encargadoRepo);
                PrepararDatos(usuarioRepo, votacionRepo, encargadoRepo);

                service.LogIn("otro_test", "pass456");
                try
                {
                    service.CerrarVotacion(IdVotacion);
                    return (false, "Se esperaba ServiceException por falta de permisos, pero no se lanzó");
                }
                catch (ServiceException ex) when (ex.Message.Contains("permisos"))
                {
                    return (true, "Acceso denegado correctamente a usuario sin rol de encargado");
                }
            }
            catch (Exception ex) { return (false, $"Error inesperado: {ex.Message}"); }
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll()
        {
            var pruebas = new (string Nombre, Func<(bool, string)> Prueba)[]
            {
                ("Encargado modifica votación",         Test_Encargado_PuedeModificarVotacion),
                ("Encargado cierra votación",           Test_Encargado_PuedeCerrarVotacion),
                ("Sin rol no puede modificar",          Test_UsuarioSinRol_NoPuedeModificarVotacion),
                ("Sin rol no puede cerrar",             Test_UsuarioSinRol_NoPuedeCerrarVotacion),
            };

            var lineas = new System.Text.StringBuilder();
            lineas.AppendLine("UT-3938 — Permitir al encargado intervenir en el ciclo de vida de una votación\n");
            bool globalOk = true;

            foreach (var (nombre, prueba) in pruebas)
            {
                var (ok, msg) = prueba();
                lineas.AppendLine($"  [{(ok ? "OK" : "FAIL")}] {nombre}: {msg}");
                if (!ok) globalOk = false;
            }

            return (globalOk, lineas.ToString());
        }
    }
}
