using System;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-3962: Dar valores por defecto a las votaciones.
    ///
    /// Criterios verificados:
    ///   · PesoJurado  = 70  por defecto
    ///   · PesoPublico = 30  por defecto
    ///   · PesoJurado + PesoPublico = 100
    ///   · Titulo      = "Votacion"   por defecto
    ///   · Descripcion = ""           por defecto
    /// </summary>
    public static class ValoresPorDefectoTest
    {
        // ── Helpers ────────────────────────────────────────────────────────

        private static Votacion CrearVotacion()
        {
            var encargado = new EncargadoVotacion(DateTime.Now, 0);
            return new Votacion(DateTime.Now, DateTime.Now.AddDays(7), true, encargado);
        }

        // ── Pruebas ────────────────────────────────────────────────────────

        /// <summary>
        /// El peso asignado al jurado por defecto debe ser 70.
        /// </summary>
        public static (bool Success, string Message) Test_PesoJuradoPorDefectoEs70()
        {
            try
            {
                var votacion = CrearVotacion();
                return votacion.PesoJurado == 70
                    ? (true, "PesoJurado por defecto es 70")
                    : (false, $"Se esperaba PesoJurado=70, se obtuvo {votacion.PesoJurado}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>
        /// El peso asignado al público por defecto debe ser 30.
        /// </summary>
        public static (bool Success, string Message) Test_PesoPublicoPorDefectoEs30()
        {
            try
            {
                var votacion = CrearVotacion();
                return votacion.PesoPublico == 30
                    ? (true, "PesoPublico por defecto es 30")
                    : (false, $"Se esperaba PesoPublico=30, se obtuvo {votacion.PesoPublico}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>
        /// Los pesos por defecto deben sumar exactamente 100.
        /// </summary>
        public static (bool Success, string Message) Test_PesosSuman100()
        {
            try
            {
                var votacion = CrearVotacion();
                int suma = votacion.PesoJurado + votacion.PesoPublico;
                return suma == 100
                    ? (true, $"Los pesos por defecto suman 100 ({votacion.PesoJurado}+{votacion.PesoPublico})")
                    : (false, $"Se esperaba suma=100, se obtuvo {suma}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>
        /// El título por defecto debe ser "Votacion".
        /// </summary>
        public static (bool Success, string Message) Test_TituloPorDefecto()
        {
            try
            {
                var votacion = CrearVotacion();
                return votacion.Titulo == "Votacion"
                    ? (true, "Título por defecto es 'Votacion'")
                    : (false, $"Se esperaba Titulo='Votacion', se obtuvo '{votacion.Titulo}'");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>
        /// La descripción por defecto debe ser cadena vacía.
        /// </summary>
        public static (bool Success, string Message) Test_DescripcionPorDefectoEsVacia()
        {
            try
            {
                var votacion = CrearVotacion();
                return votacion.Descripcion == string.Empty
                    ? (true, "Descripción por defecto es cadena vacía")
                    : (false, $"Se esperaba Descripcion='', se obtuvo '{votacion.Descripcion}'");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll()
        {
            var pruebas = new (string Nombre, Func<(bool, string)> Prueba)[]
            {
                ("PesoJurado=70",        Test_PesoJuradoPorDefectoEs70),
                ("PesoPublico=30",       Test_PesoPublicoPorDefectoEs30),
                ("Pesos suman 100",      Test_PesosSuman100),
                ("Titulo por defecto",   Test_TituloPorDefecto),
                ("Descripcion vacia",    Test_DescripcionPorDefectoEsVacia),
            };

            var lineas = new System.Text.StringBuilder();
            lineas.AppendLine("UT-3962 — Dar valores por defecto a las votaciones\n");
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
