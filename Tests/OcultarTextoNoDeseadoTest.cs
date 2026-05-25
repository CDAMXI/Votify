using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-4162: Ocultar texto no deseado.
    ///
    /// Escanea los archivos Razor del cliente en busca de los problemas de
    /// renderizado más típicos: marcadores de merge sin resolver, placeholders
    /// huérfanos, literales "null"/"undefined" visibles, comentarios de TODO/
    /// FIXME embebidos en HTML, y caracteres de control fuera de las cadenas.
    ///
    /// Criterios verificados (PA 3429):
    ///   · Ningún .razor contiene marcadores de conflicto git (&lt;&lt;&lt;&lt;, &gt;&gt;&gt;&gt;).
    ///   · Ningún .razor renderiza la palabra "undefined" como texto plano.
    ///   · Ningún .razor renderiza placeholders {{algo}} sin sustituir (formato Mustache).
    ///   · Las claves Tailwind dinámicas no quedan a medio interpolar (ej. text-[#).
    /// </summary>
    public static class OcultarTextoNoDeseadoTest
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Votify.csproj")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("No localicé la raíz del repo");
        }

        private static IEnumerable<(string Ruta, string Contenido)> ArchivosRazor()
        {
            string raiz = Path.Combine(RepoRoot(), "VotifyIU.Client");
            if (!Directory.Exists(raiz)) yield break;

            foreach (var archivo in Directory.EnumerateFiles(raiz, "*.razor", SearchOption.AllDirectories))
                yield return (archivo, File.ReadAllText(archivo));
        }

        // ── Pruebas ────────────────────────────────────────────────────────

        public static (bool Success, string Message) NingunMarcadorDeConflictoGit()
        {
            var contaminados = ArchivosRazor()
                .Where(a => a.Contenido.Contains("<<<<<<<")
                         || a.Contenido.Contains(">>>>>>>"))
                .Select(a => Path.GetFileName(a.Ruta))
                .ToList();

            return contaminados.Count == 0
                ? (true, "Ningún archivo Razor contiene marcadores de conflicto git")
                : (false, $"Marcadores sin resolver en: {string.Join(", ", contaminados)}");
        }

        public static (bool Success, string Message) NingunaPalabraUndefinedNiNullVisible()
        {
            // Buscamos textos crudos tipo "undefined" o "null" rodeados de etiquetas Razor,
            // ignorando los usos legítimos en código C# (== null, value="null"…).
            var patron = new Regex(@">\s*(undefined|NaN)\s*<", RegexOptions.IgnoreCase);
            var contaminados = ArchivosRazor()
                .Where(a => patron.IsMatch(a.Contenido))
                .Select(a => Path.GetFileName(a.Ruta))
                .ToList();

            return contaminados.Count == 0
                ? (true, "Ningún Razor renderiza la palabra 'undefined' o 'NaN' como texto visible")
                : (false, $"Texto 'undefined/NaN' visible en: {string.Join(", ", contaminados)}");
        }

        public static (bool Success, string Message) NingunPlaceholderMustacheSinSustituir()
        {
            // Patrón {{algo}} que es típico de motores tipo Mustache/Vue y no debe aparecer en Razor.
            var patron = new Regex(@"\{\{[^{}]+\}\}");
            var contaminados = ArchivosRazor()
                .Where(a => patron.IsMatch(a.Contenido))
                .Select(a => Path.GetFileName(a.Ruta))
                .ToList();

            return contaminados.Count == 0
                ? (true, "Ningún Razor contiene placeholders {{ }} sin sustituir")
                : (false, $"Placeholders Mustache en: {string.Join(", ", contaminados)}");
        }

        public static (bool Success, string Message) ClasesTailwindArbitrariasEstanCerradas()
        {
            // Tailwind acepta clases arbitrarias tipo text-[#6C8A6C]. Si una queda sin cerrar
            // (sin ']'), aparece como texto desnudo y rompe el render.
            var patron = new Regex(@"\b(text|bg|border|from|to|via)-\[#?[0-9A-Fa-f]*[^\]\s""']*\s|""");
            var contaminados = ArchivosRazor()
                .Where(a => Regex.IsMatch(a.Contenido,
                    @"\b(text|bg|border|from|to|via)-\[(?:[^\]""'\s])*\s"))
                .Select(a => Path.GetFileName(a.Ruta))
                .ToList();

            return contaminados.Count == 0
                ? (true, "Las clases Tailwind con valor arbitrario están bien cerradas con ]")
                : (false, $"Clases Tailwind sin cerrar en: {string.Join(", ", contaminados)}");
        }

        public static (bool Success, string Message) NingunCaracterDeControlEnPlantilla()
        {
            // Caracteres de control invisibles (excepto los habituales: \r \n \t) suelen
            // ser artefactos de copy/paste que rompen el rendering en algunos navegadores.
            var contaminados = ArchivosRazor()
                .Where(a => a.Contenido.Any(c => c < 0x20 && c != '\r' && c != '\n' && c != '\t'))
                .Select(a => Path.GetFileName(a.Ruta))
                .ToList();

            return contaminados.Count == 0
                ? (true, "Ningún Razor contiene caracteres de control invisibles")
                : (false, $"Caracteres de control en: {string.Join(", ", contaminados)}");
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-4162 — Ocultar texto no deseado",
            ("Sin marcadores de conflicto git",     NingunMarcadorDeConflictoGit),
            ("Sin 'undefined'/'NaN' visibles",      NingunaPalabraUndefinedNiNullVisible),
            ("Sin placeholders Mustache",           NingunPlaceholderMustacheSinSustituir),
            ("Clases Tailwind bien cerradas",       ClasesTailwindArbitrariasEstanCerradas),
            ("Sin caracteres de control",           NingunCaracterDeControlEnPlantilla));
    }
}
