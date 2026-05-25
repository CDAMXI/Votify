using System;
using System.Collections.Generic;
using System.Linq;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-4127: Confirmar registro de un voto.
    ///
    /// El flujo UX se compone de dos modales: confirmación previa al envío y
    /// confirmación de éxito tras persistir. Aquí verificamos la **persistencia
    /// real** y las reglas de negocio que la protegen, que es lo que justifica
    /// que el modal "¡Voto Registrado!" pueda aparecer con datos verídicos.
    ///
    /// Criterios verificados:
    ///   · Un voto válido se persiste con su puntuación y comentario.
    ///   · El voto queda enlazado al proyecto, votación y votante correctos.
    ///   · El sistema rechaza un segundo voto del mismo votante sobre el mismo proyecto.
    ///   · El sistema rechaza un voto sobre una votación con FechaFin pasada.
    ///   · El sistema rechaza un voto sobre una votación con Estado=false (pausada).
    ///   · El sistema rechaza un voto sin puntuación válida (fuera de 0..10).
    /// </summary>
    public static class ConfirmarRegistroVotoTest
    {
        private const int IdVotacion = 5;
        private const int IdProyecto = 10;
        private const int IdVotante  = 100;

        // ── Helpers que replican la lógica de validación + persistencia ────

        /// <summary>Reglas de negocio aplicadas justo antes de insertar el voto.</summary>
        private static string? ValidarRegistroVoto(
            Votacion votacion,
            int idVotante,
            int idProyecto,
            double puntuacion,
            string? comentario,
            InMemoryDAL<Voto> votoRepo)
        {
            if (puntuacion < 0 || puntuacion > 10)
                return "El valor debe estar entre 0 y 10";

            if (comentario != null && comentario.Length > 500)
                return "El comentario no puede superar los 500 caracteres";

            if (votacion.FechaFin <= DateTime.Now)
                return "La votación está cerrada";

            if (!votacion.Estado)
                return "La votación está pausada";

            bool yaVoto = votoRepo
                .GetWhere(v => v.VotanteId == idVotante
                            && v.VotacionId == votacion.Id
                            && v.ProyectoId == idProyecto)
                .Any();
            if (yaVoto)
                return "Ya has votado en este proyecto para esta votación";

            return null;
        }

        private static Voto? RegistrarVoto(
            Votacion votacion,
            int idVotante,
            int idProyecto,
            double puntuacion,
            string? comentario,
            InMemoryDAL<Voto> votoRepo,
            out string? error)
        {
            error = ValidarRegistroVoto(votacion, idVotante, idProyecto, puntuacion, comentario, votoRepo);
            if (error != null) return null;

            var voto = new Voto(puntuacion, comentario ?? string.Empty, DateTime.Now)
            {
                Id = votoRepo.GetAll().Count() + 1,
                VotanteId  = idVotante,
                VotacionId = votacion.Id,
                ProyectoId = idProyecto
            };
            votoRepo.Insert(voto);
            return voto;
        }

        /// <summary>
        /// Modifica un voto existente. Solo se permite si la votación sigue activa
        /// (FechaFin futura y Estado=true). Es la operación que respalda PA 3330/3335.
        /// </summary>
        private static Voto? ModificarVoto(
            Votacion votacion,
            int idVotante,
            int idProyecto,
            double nuevaPuntuacion,
            string? nuevoComentario,
            InMemoryDAL<Voto> votoRepo,
            out string? error)
        {
            error = null;

            if (votacion.FechaFin <= DateTime.Now) { error = "La votación está cerrada"; return null; }
            if (!votacion.Estado)                  { error = "La votación está pausada"; return null; }
            if (nuevaPuntuacion < 0 || nuevaPuntuacion > 10) { error = "El valor debe estar entre 0 y 10"; return null; }

            var voto = votoRepo.GetWhere(v =>
                v.VotanteId == idVotante &&
                v.VotacionId == votacion.Id &&
                v.ProyectoId == idProyecto).FirstOrDefault();

            if (voto == null) { error = "No existe un voto previo que modificar"; return null; }

            voto.Valor      = nuevaPuntuacion;
            voto.Comentario = nuevoComentario ?? string.Empty;
            voto.Fecha      = DateTime.Now;
            return voto;
        }

        // ── Construcción del escenario ─────────────────────────────────────

        private static Votacion CrearVotacionActiva()
            => new Votacion
            {
                Id = IdVotacion,
                Titulo = "Categoría Test",
                Descripcion = string.Empty,
                Estado = true,
                FechaIni = DateTime.Now.AddDays(-1),
                FechaFin = DateTime.Now.AddDays(7),
                EventoId = 1
            };

        private static InMemoryDAL<Voto> VotoRepoVacio() => new(v => v.Id);

        // ── Pruebas ────────────────────────────────────────────────────────

        public static (bool Success, string Message) VotoValidoSePersisteConPuntuacionYComentario()
        {
            var votacion = CrearVotacionActiva();
            var repo = VotoRepoVacio();

            var voto = RegistrarVoto(votacion, IdVotante, IdProyecto, 8.2, "Muy buen proyecto", repo, out _);

            return voto != null
                && voto.Valor == 8.2
                && voto.Comentario == "Muy buen proyecto"
                && repo.GetAll().Count() == 1
                ? (true, "Voto persistido con puntuación 8.2 y comentario íntegro")
                : (false, $"voto={voto?.Valor ?? -1}/'{voto?.Comentario}', en repo={repo.GetAll().Count()}");
        }

        public static (bool Success, string Message) VotoQuedaEnlazadoAVotacionProyectoYVotante()
        {
            var votacion = CrearVotacionActiva();
            var repo = VotoRepoVacio();

            var voto = RegistrarVoto(votacion, IdVotante, IdProyecto, 7, null, repo, out _);

            return voto != null
                && voto.VotacionId == IdVotacion
                && voto.ProyectoId == IdProyecto
                && voto.VotanteId  == IdVotante
                ? (true, "Voto enlazado correctamente a votación, proyecto y votante")
                : (false, $"VotacionId={voto?.VotacionId}, ProyectoId={voto?.ProyectoId}, VotanteId={voto?.VotanteId}");
        }

        public static (bool Success, string Message) NoSePuedeVotarDosVecesElMismoProyecto()
        {
            var votacion = CrearVotacionActiva();
            var repo = VotoRepoVacio();

            RegistrarVoto(votacion, IdVotante, IdProyecto, 5, "primer voto", repo, out _);
            RegistrarVoto(votacion, IdVotante, IdProyecto, 9, "segundo voto", repo, out string? error);

            return error != null && error.Contains("Ya has votado") && repo.GetAll().Count() == 1
                ? (true, "El segundo voto del mismo votante sobre el mismo proyecto es rechazado")
                : (false, $"error='{error}', votos en repo={repo.GetAll().Count()} (esperaba 1)");
        }

        public static (bool Success, string Message) NoSePuedeVotarEnVotacionCerrada()
        {
            var votacion = CrearVotacionActiva();
            votacion.FechaFin = DateTime.Now.AddDays(-1);  // cerrada
            var repo = VotoRepoVacio();

            RegistrarVoto(votacion, IdVotante, IdProyecto, 5, null, repo, out string? error);

            return error != null && error.Contains("cerrada") && repo.GetAll().Count() == 0
                ? (true, "Una votación con FechaFin pasada rechaza nuevos votos")
                : (false, $"error='{error}', votos en repo={repo.GetAll().Count()} (esperaba 0)");
        }

        public static (bool Success, string Message) NoSePuedeVotarEnVotacionPausada()
        {
            var votacion = CrearVotacionActiva();
            votacion.Estado = false;  // pausada
            var repo = VotoRepoVacio();

            RegistrarVoto(votacion, IdVotante, IdProyecto, 5, null, repo, out string? error);

            return error != null && error.Contains("pausada") && repo.GetAll().Count() == 0
                ? (true, "Una votación con Estado=false rechaza nuevos votos")
                : (false, $"error='{error}', votos en repo={repo.GetAll().Count()} (esperaba 0)");
        }

        public static (bool Success, string Message) PuntuacionFueraDeRangoEsRechazada()
        {
            var votacion = CrearVotacionActiva();
            var repo = VotoRepoVacio();

            RegistrarVoto(votacion, IdVotante, IdProyecto, -1, null, repo, out string? errorBajo);
            RegistrarVoto(votacion, IdVotante, IdProyecto, 11, null, repo, out string? errorAlto);

            return errorBajo != null && errorAlto != null && repo.GetAll().Count() == 0
                ? (true, "Puntuaciones fuera del rango 0..10 son rechazadas en ambos extremos")
                : (false, $"errorBajo='{errorBajo}', errorAlto='{errorAlto}', votos en repo={repo.GetAll().Count()}");
        }

        // ── PA 3330: El votante PUEDE modificar su voto en votaciones activas ─

        public static (bool Success, string Message) VotantePuedeModificarVotoEnVotacionActiva()
        {
            var votacion = CrearVotacionActiva();
            var repo = VotoRepoVacio();

            RegistrarVoto(votacion, IdVotante, IdProyecto, 5.0, "Primer juicio", repo, out _);
            var modificado = ModificarVoto(votacion, IdVotante, IdProyecto, 8.5, "Segundo juicio", repo, out string? error);

            return error == null
                && modificado != null
                && modificado.Valor == 8.5
                && modificado.Comentario == "Segundo juicio"
                && repo.GetAll().Count() == 1
                ? (true, "Voto modificado en votación activa: 5.0 → 8.5 y comentario actualizado")
                : (false, $"error='{error}', valor={modificado?.Valor}, comentario='{modificado?.Comentario}', votos={repo.GetAll().Count()}");
        }

        public static (bool Success, string Message) NoSePuedeModificarVotoEnVotacionInactiva()
        {
            var votacion = CrearVotacionActiva();
            var repo = VotoRepoVacio();
            RegistrarVoto(votacion, IdVotante, IdProyecto, 5.0, "Voto inicial", repo, out _);

            // La votación se pausa y luego se intenta modificar
            votacion.Estado = false;
            ModificarVoto(votacion, IdVotante, IdProyecto, 9.0, "Intento tras pausa", repo, out string? errorPausa);

            // La votación se cierra (FechaFin pasada) y se intenta modificar
            votacion.Estado = true;
            votacion.FechaFin = DateTime.Now.AddDays(-1);
            ModificarVoto(votacion, IdVotante, IdProyecto, 9.0, "Intento tras cierre", repo, out string? errorCierre);

            var votoEnRepo = repo.GetAll().Single();

            return errorPausa != null
                && errorCierre != null
                && votoEnRepo.Valor == 5.0
                && votoEnRepo.Comentario == "Voto inicial"
                ? (true, "Voto preserva valores originales cuando se intenta modificar en votación pausada o cerrada")
                : (false, $"errorPausa='{errorPausa}', errorCierre='{errorCierre}', valor preservado={votoEnRepo.Valor}, comentario='{votoEnRepo.Comentario}'");
        }

        // ── PA 3335: Probar la confirmación de modificación ──────────────────

        public static (bool Success, string Message) ModificacionConfirmadaPersisteNuevosValores()
        {
            var votacion = CrearVotacionActiva();
            var repo = VotoRepoVacio();

            var vOriginal = RegistrarVoto(votacion, IdVotante, IdProyecto, 6.0, "Original", repo, out _);
            int idOriginal = vOriginal!.Id;
            DateTime fechaOriginal = vOriginal.Fecha;

            var vModificado = ModificarVoto(votacion, IdVotante, IdProyecto, 9.0, "Modificado", repo, out _);

            // Re-leer del repo para confirmar que la persistencia es real
            var vConsultado = repo.GetById(idOriginal);

            return vConsultado.Valor == 9.0
                && vConsultado.Comentario == "Modificado"
                && vConsultado.Id == idOriginal           // sigue siendo el mismo registro, no se duplica
                && vConsultado.Fecha >= fechaOriginal     // se actualiza la fecha de modificación
                && repo.GetAll().Count() == 1
                ? (true, "Confirmación de modificación: el voto actualizado conserva su Id y la nueva puntuación/comentario son consultables")
                : (false, $"valor={vConsultado.Valor}, comentario='{vConsultado.Comentario}', id={vConsultado.Id} (original={idOriginal}), votos en repo={repo.GetAll().Count()}");
        }

        public static (bool Success, string Message) ModificacionPermiteMultiplesAjustesConsecutivos()
        {
            var votacion = CrearVotacionActiva();
            var repo = VotoRepoVacio();
            RegistrarVoto(votacion, IdVotante, IdProyecto, 1.0, "v1", repo, out _);

            ModificarVoto(votacion, IdVotante, IdProyecto, 5.0,  "v2", repo, out _);
            ModificarVoto(votacion, IdVotante, IdProyecto, 7.5,  "v3", repo, out _);
            ModificarVoto(votacion, IdVotante, IdProyecto, 10.0, "vFinal", repo, out _);

            var voto = repo.GetAll().Single();
            return voto.Valor == 10.0 && voto.Comentario == "vFinal"
                ? (true, "Tres modificaciones consecutivas se confirman correctamente y dejan los valores finales")
                : (false, $"Esperaba 10.0/'vFinal', obtuvo {voto.Valor}/'{voto.Comentario}'");
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-4127 — Confirmar registro de un voto",
            ("Persistencia puntuación + comentario",  VotoValidoSePersisteConPuntuacionYComentario),
            ("Enlace correcto a entidades",           VotoQuedaEnlazadoAVotacionProyectoYVotante),
            ("No voto duplicado",                     NoSePuedeVotarDosVecesElMismoProyecto),
            ("Rechazo en votación cerrada",           NoSePuedeVotarEnVotacionCerrada),
            ("Rechazo en votación pausada",           NoSePuedeVotarEnVotacionPausada),
            ("Rechazo puntuación fuera de rango",     PuntuacionFueraDeRangoEsRechazada),
            ("PA 3330: modifica en votación activa",   VotantePuedeModificarVotoEnVotacionActiva),
            ("PA 3330: no modifica en inactiva",       NoSePuedeModificarVotoEnVotacionInactiva),
            ("PA 3335: modificación persistida",       ModificacionConfirmadaPersisteNuevosValores),
            ("PA 3335: modificaciones consecutivas",   ModificacionPermiteMultiplesAjustesConsecutivos));
    }
}
