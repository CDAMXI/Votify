using System;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-3962: Dar valores por defecto a las votaciones.
    ///
    /// Criterios verificados:
    ///   · PesoJurado  = 70 por defecto
    ///   · PesoPublico = 30 por defecto
    ///   · PesoJurado + PesoPublico = 100
    ///   · Titulo      = "Votacion" por defecto
    ///   · Descripcion = ""         por defecto
    /// </summary>
    public static class ValoresPorDefectoTest
    {
        private const int PesoJuradoEsperado  = 70;
        private const int PesoPublicoEsperado = 30;
        private const string TituloEsperado   = "Votacion";

        private static Votacion CrearVotacionPorDefecto()
        {
            var encargado = new EncargadoVotacion(DateTime.Now, 0);
            return new Votacion(DateTime.Now, DateTime.Now.AddDays(7), true, encargado);
        }

        // ── Pruebas ────────────────────────────────────────────────────────

        public static (bool Success, string Message) PesoJuradoPorDefectoEs70()
        {
            int peso = CrearVotacionPorDefecto().PesoJurado;
            return peso == PesoJuradoEsperado
                ? (true, $"PesoJurado por defecto es {PesoJuradoEsperado}")
                : (false, $"Se esperaba PesoJurado={PesoJuradoEsperado}, se obtuvo {peso}");
        }

        public static (bool Success, string Message) PesoPublicoPorDefectoEs30()
        {
            int peso = CrearVotacionPorDefecto().PesoPublico;
            return peso == PesoPublicoEsperado
                ? (true, $"PesoPublico por defecto es {PesoPublicoEsperado}")
                : (false, $"Se esperaba PesoPublico={PesoPublicoEsperado}, se obtuvo {peso}");
        }

        public static (bool Success, string Message) PesosSuman100()
        {
            var votacion = CrearVotacionPorDefecto();
            int suma = votacion.PesoJurado + votacion.PesoPublico;
            return suma == 100
                ? (true, $"Los pesos por defecto suman 100 ({votacion.PesoJurado}+{votacion.PesoPublico})")
                : (false, $"Se esperaba suma=100, se obtuvo {suma}");
        }

        public static (bool Success, string Message) TituloPorDefectoEsVotacion()
        {
            string titulo = CrearVotacionPorDefecto().Titulo;
            return titulo == TituloEsperado
                ? (true, $"Título por defecto es '{TituloEsperado}'")
                : (false, $"Se esperaba Titulo='{TituloEsperado}', se obtuvo '{titulo}'");
        }

        public static (bool Success, string Message) DescripcionPorDefectoEsVacia()
        {
            string descripcion = CrearVotacionPorDefecto().Descripcion;
            return descripcion == string.Empty
                ? (true, "Descripción por defecto es cadena vacía")
                : (false, $"Se esperaba Descripcion='', se obtuvo '{descripcion}'");
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-3962 — Dar valores por defecto a las votaciones",
            ("PesoJurado=70",          PesoJuradoPorDefectoEs70),
            ("PesoPublico=30",         PesoPublicoPorDefectoEs30),
            ("Pesos suman 100",        PesosSuman100),
            ("Titulo por defecto",     TituloPorDefectoEsVotacion),
            ("Descripcion vacia",      DescripcionPorDefectoEsVacia));
    }
}
