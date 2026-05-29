using System;
using System.Collections.Generic;
using System.Linq;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-3979: Visualizar proyectos de votaciones concretadas.
    ///
    /// Una "votación concretada/finalizada" es aquella cuya FechaFin ya pasó.
    /// En la vista "Eventos Finalizados" del Dashboard el usuario debe poder
    /// consultar los proyectos que participaron y, en concreto, el ganador.
    ///
    /// Criterios verificados:
    ///   · El listado de finalizadas excluye votaciones activas y futuras.
    ///   · Una votación concretada expone todos los proyectos que participaron.
    ///   · El ganador es el proyecto con mayor puntuación media.
    ///   · El conteo de votos totales de una votación finalizada es correcto.
    ///   · Una votación finalizada sin proyectos sigue apareciendo en el historial.
    /// </summary>
    public static class VisualizarProyectosVotacionesFinalizadasTest
    {
        // ── Helpers que replican la lógica de visualización ────────────────

        private static bool EsFinalizada(Votacion v) =>
            v.FechaFin <= DateTime.Now;

        private static List<Votacion> FiltrarFinalizadas(IEnumerable<Votacion> votaciones) =>
            votaciones.Where(EsFinalizada).ToList();

        private static List<Proyecto> ProyectosDeVotacion(
            Votacion votacion,
            InMemoryDAL<Proyecto> proyectoRepo,
            InMemoryDAL<Voto> votoRepo)
        {
            var proyectoIds = votoRepo
                .GetWhere(v => v.VotacionId == votacion.Id)
                .Select(v => v.ProyectoId)
                .Distinct()
                .ToHashSet();

            return proyectoRepo.GetAll()
                .Where(p => proyectoIds.Contains(p.Id))
                .ToList();
        }

        private static Proyecto? GanadorDeVotacion(
            Votacion votacion,
            InMemoryDAL<Proyecto> proyectoRepo,
            InMemoryDAL<Voto> votoRepo)
        {
            return votoRepo
                .GetWhere(v => v.VotacionId == votacion.Id)
                .GroupBy(v => v.ProyectoId)
                .Select(g => new
                {
                    ProyectoId = g.Key,
                    Media = g.Average(v => v.Valor)
                })
                .OrderByDescending(x => x.Media)
                .Take(1)
                .Select(x => proyectoRepo.GetById(x.ProyectoId))
                .FirstOrDefault();
        }

        // ── Construcción del escenario ─────────────────────────────────────

        private const int IdVotacionFinalizada = 1;
        private const int IdVotacionActiva    = 2;
        private const int IdVotacionFutura    = 3;
        private const int IdVotacionSinProy   = 4;

        private const int IdProyectoEcoTrack = 10;
        private const int IdProyectoHealthAI = 11;
        private const int IdProyectoEduSmart = 12;

        private sealed record Escenario(
            InMemoryDAL<Votacion> VotacionRepo,
            InMemoryDAL<Proyecto> ProyectoRepo,
            InMemoryDAL<Voto> VotoRepo);

        private static Escenario CrearEscenario()
        {
            var votacionRepo = new InMemoryDAL<Votacion>(v => v.Id);
            var proyectoRepo = new InMemoryDAL<Proyecto>(p => p.Id);
            var votoRepo     = new InMemoryDAL<Voto>(v => v.Id);

            // 3 votaciones en distintos estados temporales
            votacionRepo.Insert(CrearVotacion(IdVotacionFinalizada, "DevFest 2026",
                fechaIni: DateTime.Now.AddDays(-30),
                fechaFin: DateTime.Now.AddDays(-5)));
            votacionRepo.Insert(CrearVotacion(IdVotacionActiva,    "HackUPC 2026",
                fechaIni: DateTime.Now.AddDays(-1),
                fechaFin: DateTime.Now.AddDays(7)));
            votacionRepo.Insert(CrearVotacion(IdVotacionFutura,    "StartupWeekend",
                fechaIni: DateTime.Now.AddDays(10),
                fechaFin: DateTime.Now.AddDays(20)));
            votacionRepo.Insert(CrearVotacion(IdVotacionSinProy,   "EventoVacío",
                fechaIni: DateTime.Now.AddDays(-15),
                fechaFin: DateTime.Now.AddDays(-1)));

            // Proyectos
            proyectoRepo.Insert(new Proyecto { Id = IdProyectoEcoTrack, Nombre = "EcoTrack",  CompetidorId = 1 });
            proyectoRepo.Insert(new Proyecto { Id = IdProyectoHealthAI, Nombre = "HealthAI",  CompetidorId = 2 });
            proyectoRepo.Insert(new Proyecto { Id = IdProyectoEduSmart, Nombre = "EduSmart",  CompetidorId = 3 });

            // Votos en la votación finalizada: HealthAI gana con media 9.0
            votoRepo.Insert(CrearVoto(1,  IdVotacionFinalizada, IdProyectoEcoTrack, 6.0));
            votoRepo.Insert(CrearVoto(2,  IdVotacionFinalizada, IdProyectoEcoTrack, 7.0));
            votoRepo.Insert(CrearVoto(3,  IdVotacionFinalizada, IdProyectoHealthAI, 9.0));
            votoRepo.Insert(CrearVoto(4,  IdVotacionFinalizada, IdProyectoHealthAI, 9.0));
            votoRepo.Insert(CrearVoto(5,  IdVotacionFinalizada, IdProyectoEduSmart, 8.0));
            votoRepo.Insert(CrearVoto(6,  IdVotacionFinalizada, IdProyectoEduSmart, 8.5));

            return new Escenario(votacionRepo, proyectoRepo, votoRepo);
        }

        private static Votacion CrearVotacion(int id, string titulo, DateTime fechaIni, DateTime fechaFin)
            => new Votacion
            {
                Id = id,
                Titulo = titulo,
                Descripcion = string.Empty,
                FechaIni = fechaIni,
                FechaFin = fechaFin,
                NombreEstado = fechaFin > DateTime.Now ? "Activa" : "Cerrada",
                EventoId = id
            };

        private static Voto CrearVoto(int id, int idVotacion, int idProyecto, double valor)
            => new Voto
            {
                Id = id,
                VotacionId = idVotacion,
                ProyectoId = idProyecto,
                Valor = valor,
                Comentario = string.Empty,
                Fecha = DateTime.Now.AddDays(-7)
            };

        // ── Pruebas ────────────────────────────────────────────────────────

        public static (bool Success, string Message) HistorialSoloMuestraVotacionesFinalizadas()
        {
            var ctx = CrearEscenario();
            var finalizadas = FiltrarFinalizadas(ctx.VotacionRepo.GetAll());

            var ids = finalizadas.Select(v => v.Id).OrderBy(id => id).ToList();
            var esperado = new[] { IdVotacionFinalizada, IdVotacionSinProy };

            return ids.SequenceEqual(esperado)
                ? (true, "El historial incluye solo las votaciones cuya FechaFin ya pasó")
                : (false, $"Esperaba IDs {string.Join(",", esperado)}, obtuvo {string.Join(",", ids)}");
        }

        public static (bool Success, string Message) VotacionFinalizadaExponeTodosSusProyectos()
        {
            var ctx = CrearEscenario();
            var votacion = ctx.VotacionRepo.GetById(IdVotacionFinalizada);

            var proyectos = ProyectosDeVotacion(votacion, ctx.ProyectoRepo, ctx.VotoRepo);
            var nombres = proyectos.Select(p => p.Nombre).OrderBy(n => n).ToList();
            var esperado = new[] { "EcoTrack", "EduSmart", "HealthAI" };

            return nombres.SequenceEqual(esperado)
                ? (true, $"La votación finalizada expone los 3 proyectos: {string.Join(", ", esperado)}")
                : (false, $"Esperaba {string.Join(", ", esperado)}, obtuvo {string.Join(", ", nombres)}");
        }

        public static (bool Success, string Message) ElGanadorEsElProyectoConMayorMedia()
        {
            var ctx = CrearEscenario();
            var votacion = ctx.VotacionRepo.GetById(IdVotacionFinalizada);

            var ganador = GanadorDeVotacion(votacion, ctx.ProyectoRepo, ctx.VotoRepo);

            return ganador != null && ganador.Id == IdProyectoHealthAI
                ? (true, "Ganador correcto: HealthAI con media 9.0 (vs EduSmart 8.25 y EcoTrack 6.5)")
                : (false, $"Esperaba HealthAI (id={IdProyectoHealthAI}), obtuvo {ganador?.Nombre ?? "<null>"}");
        }

        public static (bool Success, string Message) ConteoDeVotosTotalesEsCorrecto()
        {
            var ctx = CrearEscenario();
            int totalVotos = ctx.VotoRepo.GetWhere(v => v.VotacionId == IdVotacionFinalizada).Count();

            return totalVotos == 6
                ? (true, "La votación finalizada tiene 6 votos totales (2 por cada proyecto)")
                : (false, $"Esperaba 6 votos, obtuvo {totalVotos}");
        }

        public static (bool Success, string Message) VotacionFinalizadaSinProyectosSigueEnElHistorial()
        {
            var ctx = CrearEscenario();
            var finalizadas = FiltrarFinalizadas(ctx.VotacionRepo.GetAll());

            bool incluyeVacia = finalizadas.Any(v => v.Id == IdVotacionSinProy);
            var proyectos = ProyectosDeVotacion(
                ctx.VotacionRepo.GetById(IdVotacionSinProy),
                ctx.ProyectoRepo, ctx.VotoRepo);

            return incluyeVacia && proyectos.Count == 0
                ? (true, "Una votación finalizada sin proyectos sigue listada (con 0 proyectos)")
                : (false, $"incluyeVacia={incluyeVacia}, proyectos={proyectos.Count} (esperaba true, 0)");
        }

        public static (bool Success, string Message) VotacionActivaNoApareceEnElHistorial()
        {
            var ctx = CrearEscenario();
            var finalizadas = FiltrarFinalizadas(ctx.VotacionRepo.GetAll());

            return finalizadas.All(v => v.Id != IdVotacionActiva)
                ? (true, "La votación activa (HackUPC 2026) queda fuera del historial")
                : (false, "La votación activa apareció erróneamente en el historial");
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-3979 — Visualizar proyectos de votaciones concretadas",
            ("Historial solo finalizadas",              HistorialSoloMuestraVotacionesFinalizadas),
            ("Proyectos de la votación finalizada",     VotacionFinalizadaExponeTodosSusProyectos),
            ("Ganador por mayor media",                 ElGanadorEsElProyectoConMayorMedia),
            ("Conteo de votos totales",                 ConteoDeVotosTotalesEsCorrecto),
            ("Finalizada sin proyectos sigue listada",  VotacionFinalizadaSinProyectosSigueEnElHistorial),
            ("Activa no aparece en historial",          VotacionActivaNoApareceEnElHistorial));
    }
}
