using System;
using System.Collections.Generic;
using System.Linq;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-4009: Filtrar votaciones por categorías.
    ///
    /// Criterios verificados:
    ///   · Filtrar por una categoría devuelve solo las votaciones de esa categoría.
    ///   · Filtrar por "Todas" (null o vacío) devuelve todas las votaciones.
    ///   · Filtrar por una categoría inexistente devuelve lista vacía.
    ///   · El conteo por categoría coincide con el número real de votaciones en ella.
    /// </summary>
    public static class FiltrarVotacionesPorCategoriaTest
    {
        private const string CatSalud          = "Salud";
        private const string CatSostenibilidad = "Sostenibilidad";
        private const string CatEducacion      = "Educación";
        private const string CatGeneral        = "General";

        // ── Helper que replica el filtrado por categoría ───────────────────

        private static List<Votacion> FiltrarPorCategoria(
            IEnumerable<Votacion> votaciones,
            string? categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria))
                return votaciones.ToList();

            return votaciones
                .Where(v => string.Equals(v.categoria?.Nombre, categoria, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static Dictionary<string, int> ContarPorCategoria(IEnumerable<Votacion> votaciones)
            => votaciones
                .Where(v => !string.IsNullOrWhiteSpace(v.categoria?.Nombre))
                .GroupBy(v => v.categoria!.Nombre)
                .ToDictionary(g => g.Key, g => g.Count());

        // ── Construcción del escenario ─────────────────────────────────────

        private static List<Votacion> CrearVotacionesEscenario()
        {
            return new List<Votacion>
            {
                CrearVotacion(1,  "Mejor Proyecto de Salud",          CatSalud),
                CrearVotacion(2,  "Mejor Innovación en Salud",         CatSalud),
                CrearVotacion(3,  "Proyecto Más Innovador",            CatSostenibilidad),
                CrearVotacion(4,  "Mayor Impacto Social",              CatSostenibilidad),
                CrearVotacion(5,  "Mejor Implementación Educativa",    CatEducacion),
                CrearVotacion(6,  "Premio EdTech",                     CatEducacion),
                CrearVotacion(7,  "Premio del Público",                CatGeneral)
            };
        }

        private static Votacion CrearVotacion(int id, string titulo, string nombreCategoria)
            => new Votacion
            {
                Id = id,
                Titulo = titulo,
                Descripcion = string.Empty,
                Estado = true,
                FechaIni = DateTime.Now,
                FechaFin = DateTime.Now.AddDays(30),
                categoria = new Categoria(nombreCategoria, string.Empty)
            };

        // ── Pruebas ────────────────────────────────────────────────────────

        public static (bool Success, string Message) FiltrarPorSaludDevuelveSoloSalud()
        {
            var votaciones = CrearVotacionesEscenario();
            var filtradas = FiltrarPorCategoria(votaciones, CatSalud);

            return filtradas.Count == 2
                && filtradas.All(v => v.categoria!.Nombre == CatSalud)
                ? (true, $"Filtro '{CatSalud}' devuelve las 2 votaciones de esa categoría")
                : (false, $"Esperaba 2 votaciones de '{CatSalud}', obtuvo {filtradas.Count}");
        }

        public static (bool Success, string Message) FiltrarPorTodasDevuelveTodas()
        {
            var votaciones = CrearVotacionesEscenario();

            var sinFiltroNull   = FiltrarPorCategoria(votaciones, null);
            var sinFiltroVacio  = FiltrarPorCategoria(votaciones, "");
            var sinFiltroBlanco = FiltrarPorCategoria(votaciones, "   ");

            int esperado = votaciones.Count;
            bool ok = sinFiltroNull.Count == esperado
                   && sinFiltroVacio.Count == esperado
                   && sinFiltroBlanco.Count == esperado;

            return ok
                ? (true, $"Filtro nulo/vacío devuelve las {esperado} votaciones")
                : (false, $"Esperaba {esperado}, obtuvo null={sinFiltroNull.Count}, vacío={sinFiltroVacio.Count}, blanco={sinFiltroBlanco.Count}");
        }

        public static (bool Success, string Message) FiltrarPorCategoriaInexistenteDevuelveVacio()
        {
            var votaciones = CrearVotacionesEscenario();
            var filtradas = FiltrarPorCategoria(votaciones, "Categoría Que No Existe");

            return filtradas.Count == 0
                ? (true, "Una categoría inexistente devuelve lista vacía")
                : (false, $"Esperaba 0 votaciones, obtuvo {filtradas.Count}");
        }

        public static (bool Success, string Message) ConteoPorCategoriaEsCorrecto()
        {
            var votaciones = CrearVotacionesEscenario();
            var conteo = ContarPorCategoria(votaciones);

            bool ok = conteo[CatSalud]          == 2
                   && conteo[CatSostenibilidad] == 2
                   && conteo[CatEducacion]      == 2
                   && conteo[CatGeneral]        == 1;

            return ok
                ? (true, $"Conteo correcto: Salud=2, Sostenibilidad=2, Educación=2, General=1")
                : (false, $"Conteo erróneo: Salud={conteo.GetValueOrDefault(CatSalud)}, " +
                          $"Sostenibilidad={conteo.GetValueOrDefault(CatSostenibilidad)}, " +
                          $"Educación={conteo.GetValueOrDefault(CatEducacion)}, " +
                          $"General={conteo.GetValueOrDefault(CatGeneral)}");
        }

        public static (bool Success, string Message) FiltradoEsCaseInsensitive()
        {
            var votaciones = CrearVotacionesEscenario();
            var resultadoMinusculas = FiltrarPorCategoria(votaciones, "salud");
            var resultadoMayusculas = FiltrarPorCategoria(votaciones, "SALUD");

            return resultadoMinusculas.Count == 2 && resultadoMayusculas.Count == 2
                ? (true, "El filtrado por categoría es insensible a mayúsculas/minúsculas")
                : (false, $"Esperaba 2 en ambos, obtuvo {resultadoMinusculas.Count} y {resultadoMayusculas.Count}");
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-4009 — Filtrar votaciones por categorías",
            ("Filtro por categoría concreta",       FiltrarPorSaludDevuelveSoloSalud),
            ("Filtro 'Todas' devuelve todas",        FiltrarPorTodasDevuelveTodas),
            ("Categoría inexistente → vacío",       FiltrarPorCategoriaInexistenteDevuelveVacio),
            ("Conteo por categoría correcto",       ConteoPorCategoriaEsCorrecto),
            ("Insensible a mayúsculas",             FiltradoEsCaseInsensitive));
    }
}
