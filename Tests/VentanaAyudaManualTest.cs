using System;
using System.IO;
using System.Linq;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-4164: Documentación de la página + botón "help".
    ///
    /// El test inspecciona el contenido textual de los archivos Razor (que viajan
    /// con el binario como recursos de proyecto) para verificar el contrato de
    /// la ventana de ayuda: secciones presentes, rutas correctas y reglas de
    /// visibilidad del botón flotante.
    ///
    /// Criterios verificados (PA 3433):
    ///   · La página /manual existe y declara la ruta correcta.
    ///   · El manual cubre las 9 secciones canónicas de la guía de usuario.
    ///   · El botón "?" navega a /manual.
    ///   · El botón "?" se oculta en puntos de entrada (login, registro,
    ///     dashboard, manual mismo).
    /// </summary>
    public static class VentanaAyudaManualTest
    {
        // ── Localización de los archivos en el repo ─────────────────────────
        // El binario de Tests está en bin/Debug/net10.0; el repo raíz está 4 niveles arriba.

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Votify.csproj")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("No localicé la raíz del repo");
        }

        private static string LeerArchivo(string rutaRelativa)
        {
            string ruta = Path.Combine(RepoRoot(), rutaRelativa);
            return File.Exists(ruta) ? File.ReadAllText(ruta) : string.Empty;
        }

        // ── Pruebas ────────────────────────────────────────────────────────

        public static (bool Success, string Message) PaginaManualDeclaraRutaYLayout()
        {
            string contenido = LeerArchivo(Path.Combine("VotifyIU.Client", "Pages", "Manual.razor"));

            bool ok = contenido.Contains("@page \"/manual\"")
                   && contenido.Contains("@layout EmptyLayout");

            return ok
                ? (true, "Manual.razor declara ruta /manual y layout EmptyLayout")
                : (false, "Manual.razor no declara correctamente @page o @layout");
        }

        public static (bool Success, string Message) ManualContieneLasNueveSecciones()
        {
            string contenido = LeerArchivo(Path.Combine("VotifyIU.Client", "Pages", "Manual.razor"));

            string[] secciones =
            {
                "Manual de Usuario",
                "¿Qué es Votify?",
                "Primeros Pasos",
                "Roles en un evento",
                "Cómo votar un proyecto",
                "Crear un evento",
                "Mi Perfil",
                "Asistente IA",
                "Navegación"
            };

            var faltantes = secciones.Where(s => !contenido.Contains(s)).ToList();

            return faltantes.Count == 0
                ? (true, $"El manual cubre las {secciones.Length} secciones canónicas")
                : (false, $"Faltan secciones: {string.Join(", ", faltantes)}");
        }

        public static (bool Success, string Message) BotonHelpNavegaAlManual()
        {
            string contenido = LeerArchivo(Path.Combine("VotifyIU.Client", "Components", "HelpButton.razor"));

            bool ok = contenido.Contains("Nav.NavigateTo(\"/manual\")")
                   || contenido.Contains("NavigateTo(\"/manual\")");

            return ok
                ? (true, "HelpButton invoca NavigateTo(\"/manual\")")
                : (false, "HelpButton no navega explícitamente a /manual");
        }

        public static (bool Success, string Message) BotonHelpSeOcultaEnPuntosDeEntrada()
        {
            string contenido = LeerArchivo(Path.Combine("VotifyIU.Client", "Components", "HelpButton.razor"));

            string[] rutasOcultas = { "\"/\"", "\"/login\"", "\"/registro\"", "\"/manual\"" };
            var faltantes = rutasOcultas.Where(r => !contenido.Contains(r)).ToList();

            return faltantes.Count == 0
                ? (true, "HelpButton declara como ocultas /, /login, /registro y /manual")
                : (false, $"Faltan rutas en la lista de ocultas: {string.Join(", ", faltantes)}");
        }

        public static (bool Success, string Message) HelpButtonEstaRegistradoEnLayoutGlobal()
        {
            string contenido = LeerArchivo(Path.Combine("VotifyIU.Client", "Layout", "EmptyLayout.razor"));

            return contenido.Contains("<HelpButton")
                ? (true, "EmptyLayout incluye <HelpButton /> a nivel global")
                : (false, "EmptyLayout no referencia HelpButton — no se renderiza en ninguna página");
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-4164 — Ventana de ayuda y botón help",
            ("Manual declara ruta y layout",      PaginaManualDeclaraRutaYLayout),
            ("Manual cubre 9 secciones",          ManualContieneLasNueveSecciones),
            ("Botón ? navega a /manual",          BotonHelpNavegaAlManual),
            ("Botón ? oculto en entradas",        BotonHelpSeOcultaEnPuntosDeEntrada),
            ("HelpButton registrado en layout",   HelpButtonEstaRegistradoEnLayoutGlobal));
    }
}
