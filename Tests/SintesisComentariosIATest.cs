using System;
using System.Collections.Generic;
using System.Linq;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-4008: Sintetizar comentarios del jurado con IA.
    ///
    /// La síntesis la realiza el modelo Gemini, no es determinista. Lo que sí podemos
    /// verificar de forma reproducible es la **precondición**: que el sistema
    /// recopila correctamente los comentarios del jurado sobre los proyectos del
    /// competidor actual, que es lo que se inyecta en el system prompt del chat.
    ///
    /// Criterios verificados:
    ///   · Se devuelven todos los comentarios del jurado sobre los proyectos del
    ///     competidor logueado.
    ///   · No se devuelven comentarios vacíos.
    ///   · No se filtran comentarios sobre proyectos de otros competidores.
    ///   · Un usuario sin proyectos como competidor recibe lista vacía.
    /// </summary>
    public static class SintesisComentariosIATest
    {
        private const int IdCompetidorActual = 100;
        private const int IdCompetidorOtro = 200;
        private const int IdProyectoMio = 10;
        private const int IdProyectoAjeno = 20;
        private const int IdVotacion = 5;

        // ── Helper que replica la query del endpoint /api/ai/chat ──────────

        private static List<string> ObtenerComentariosDelJurado(
            int idUsuario,
            InMemoryDAL<Competidor> competidorRepo,
            InMemoryDAL<Proyecto> proyectoRepo,
            InMemoryDAL<Voto> votoRepo)
        {
            var competidorIds = competidorRepo
                .GetWhere(c => c.UsuarioId == idUsuario)
                .Select(c => c.Id)
                .ToHashSet();

            if (competidorIds.Count == 0) return new List<string>();

            var proyectoIds = proyectoRepo
                .GetWhere(p => competidorIds.Contains(p.CompetidorId))
                .Select(p => p.Id)
                .ToHashSet();

            return votoRepo
                .GetWhere(v => proyectoIds.Contains(v.ProyectoId)
                            && !string.IsNullOrWhiteSpace(v.Comentario))
                .OrderByDescending(v => v.Fecha)
                .Select(v => v.Comentario.Trim())
                .Distinct()
                .ToList();
        }

        // ── Construcción del escenario en memoria ──────────────────────────

        private sealed record Escenario(
            InMemoryDAL<Competidor> CompetidorRepo,
            InMemoryDAL<Proyecto> ProyectoRepo,
            InMemoryDAL<Voto> VotoRepo);

        private static Escenario CrearEscenario(int idUsuario)
        {
            var competidorRepo = new InMemoryDAL<Competidor>(c => c.Id);
            var proyectoRepo = new InMemoryDAL<Proyecto>(p => p.Id);
            var votoRepo = new InMemoryDAL<Voto>(v => v.Id);

            // Competidor actual con su proyecto
            competidorRepo.Insert(new Competidor(DateTime.Now, 0)
            {
                Id = IdCompetidorActual,
                UsuarioId = idUsuario
            });
            proyectoRepo.Insert(new Proyecto
            {
                Id = IdProyectoMio,
                Nombre = "Proyecto del usuario actual",
                CompetidorId = IdCompetidorActual
            });

            // Otro competidor con OTRO proyecto (no debería filtrarse)
            competidorRepo.Insert(new Competidor(DateTime.Now, 0)
            {
                Id = IdCompetidorOtro,
                UsuarioId = 999
            });
            proyectoRepo.Insert(new Proyecto
            {
                Id = IdProyectoAjeno,
                Nombre = "Proyecto ajeno",
                CompetidorId = IdCompetidorOtro
            });

            return new Escenario(competidorRepo, proyectoRepo, votoRepo);
        }

        private static Voto CrearVoto(int id, int idProyecto, string? comentario, DateTime fecha)
            => new Voto
            {
                Id = id,
                ProyectoId = idProyecto,
                VotacionId = IdVotacion,
                Comentario = comentario ?? string.Empty,
                Fecha = fecha,
                Valor = 8
            };

        // ── Pruebas ────────────────────────────────────────────────────────

        public static (bool Success, string Message) DevuelveTodosLosComentariosDelJurado()
        {
            const int idUsuario = 42;
            var ctx = CrearEscenario(idUsuario);
            ctx.VotoRepo.Insert(CrearVoto(1, IdProyectoMio, "Muy innovador",       DateTime.Now.AddMinutes(-3)));
            ctx.VotoRepo.Insert(CrearVoto(2, IdProyectoMio, "Falta documentación", DateTime.Now.AddMinutes(-2)));
            ctx.VotoRepo.Insert(CrearVoto(3, IdProyectoMio, "Buena presentación",  DateTime.Now.AddMinutes(-1)));

            var comentarios = ObtenerComentariosDelJurado(idUsuario,
                ctx.CompetidorRepo, ctx.ProyectoRepo, ctx.VotoRepo);

            return comentarios.Count == 3
                && comentarios.Contains("Muy innovador")
                && comentarios.Contains("Falta documentación")
                && comentarios.Contains("Buena presentación")
                ? (true, "Se recopilaron los 3 comentarios del jurado sobre el proyecto del competidor")
                : (false, $"Esperaba 3 comentarios, obtuvo {comentarios.Count}: [{string.Join(" | ", comentarios)}]");
        }

        public static (bool Success, string Message) IgnoraComentariosVaciosONulos()
        {
            const int idUsuario = 42;
            var ctx = CrearEscenario(idUsuario);
            ctx.VotoRepo.Insert(CrearVoto(1, IdProyectoMio, "Comentario válido", DateTime.Now));
            ctx.VotoRepo.Insert(CrearVoto(2, IdProyectoMio, "",                   DateTime.Now));
            ctx.VotoRepo.Insert(CrearVoto(3, IdProyectoMio, "   ",                DateTime.Now));
            ctx.VotoRepo.Insert(CrearVoto(4, IdProyectoMio, null,                 DateTime.Now));

            var comentarios = ObtenerComentariosDelJurado(idUsuario,
                ctx.CompetidorRepo, ctx.ProyectoRepo, ctx.VotoRepo);

            return comentarios.Count == 1 && comentarios[0] == "Comentario válido"
                ? (true, "Comentarios vacíos, en blanco y nulos quedan filtrados")
                : (false, $"Esperaba solo 'Comentario válido', obtuvo {comentarios.Count}: [{string.Join(" | ", comentarios)}]");
        }

        public static (bool Success, string Message) NoFiltraComentariosDeProyectosAjenos()
        {
            const int idUsuario = 42;
            var ctx = CrearEscenario(idUsuario);
            ctx.VotoRepo.Insert(CrearVoto(1, IdProyectoMio,    "Comentario sobre mi proyecto",       DateTime.Now));
            ctx.VotoRepo.Insert(CrearVoto(2, IdProyectoAjeno,  "Comentario sobre proyecto de otro",  DateTime.Now));

            var comentarios = ObtenerComentariosDelJurado(idUsuario,
                ctx.CompetidorRepo, ctx.ProyectoRepo, ctx.VotoRepo);

            return comentarios.Count == 1 && comentarios[0] == "Comentario sobre mi proyecto"
                ? (true, "Solo se devuelven comentarios sobre proyectos del competidor actual")
                : (false, $"Esperaba 1 comentario sobre mi proyecto, obtuvo {comentarios.Count}: [{string.Join(" | ", comentarios)}]");
        }

        public static (bool Success, string Message) UsuarioSinProyectosDevuelveListaVacia()
        {
            const int idUsuarioSinRol = 12345;  // no está en competidorRepo
            var ctx = CrearEscenario(idUsuario: 42);
            ctx.VotoRepo.Insert(CrearVoto(1, IdProyectoMio, "Comentario al proyecto de otro", DateTime.Now));

            var comentarios = ObtenerComentariosDelJurado(idUsuarioSinRol,
                ctx.CompetidorRepo, ctx.ProyectoRepo, ctx.VotoRepo);

            return comentarios.Count == 0
                ? (true, "Un usuario sin proyectos como competidor recibe lista vacía")
                : (false, $"Esperaba lista vacía, obtuvo {comentarios.Count} comentarios");
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-4008 — Sintetizar comentarios del jurado con IA (precondiciones de datos)",
            ("Recopila todos los comentarios",          DevuelveTodosLosComentariosDelJurado),
            ("Ignora comentarios vacíos",               IgnoraComentariosVaciosONulos),
            ("No filtra comentarios de otros",          NoFiltraComentariosDeProyectosAjenos),
            ("Sin proyectos → lista vacía",             UsuarioSinProyectosDevuelveListaVacia));
    }
}
