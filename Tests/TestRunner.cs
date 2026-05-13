using System;
using System.Text;

namespace Votify.Tests
{
    /// <summary>
    /// Runner común para suites de pruebas de aceptación.
    /// Ejecuta cada caso, captura excepciones inesperadas y compone el reporte final.
    /// </summary>
    public static class TestRunner
    {
        public static (bool Success, string Message) Run(
            string suiteName,
            params (string Nombre, Func<(bool Success, string Message)> Prueba)[] casos)
        {
            var reporte = new StringBuilder();
            reporte.AppendLine(suiteName).AppendLine();

            bool todasOk = true;

            foreach (var (nombre, prueba) in casos)
            {
                var resultado = EjecutarSeguro(prueba);
                reporte.AppendLine($"  [{(resultado.Success ? "OK" : "FAIL")}] {nombre}: {resultado.Message}");
                if (!resultado.Success) todasOk = false;
            }

            return (todasOk, reporte.ToString());
        }

        private static (bool Success, string Message) EjecutarSeguro(Func<(bool, string)> prueba)
        {
            try
            {
                return prueba();
            }
            catch (Exception ex)
            {
                return (false, $"Excepción inesperada: {ex.Message}");
            }
        }
    }
}
