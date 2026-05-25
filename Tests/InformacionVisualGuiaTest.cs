using System;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-4129: Añadir información visual relevante
    /// para guiar al usuario.
    ///
    /// Verificamos las **reglas de mapeo** que asignan un icono a cada
    /// entidad/estado de la UI. Estos mapas viven en los Razor pages como
    /// helpers privados (IconoParaCriterio, IconoParaRol, GetStatusIcon).
    /// El test replica el contrato esperado: dado un input semántico, qué
    /// icono se debe renderizar.
    ///
    /// Criterios verificados:
    ///   · Cada criterio de evaluación recibe un icono temático según su nombre.
    ///   · Cada rol de evento recibe un icono distintivo.
    ///   · Cada estado de votación recibe un icono que refuerza el color/texto.
    ///   · Casos sin coincidencia caen al fallback ("star" / "user").
    ///   · El mapeo es case-insensitive (los criterios pueden venir en cualquier capitalización).
    /// </summary>
    public static class InformacionVisualGuiaTest
    {
        // ── Reglas de mapeo (réplica del contrato de los Razor pages) ──────

        private static string IconoParaCriterio(string? nombre)
        {
            var n = (nombre ?? string.Empty).ToLowerInvariant();
            if (n.Contains("innov") || n.Contains("crea") || n.Contains("origin")) return "lightbulb";
            if (n.Contains("viab") || n.Contains("técnic") || n.Contains("tecnic")
                || n.Contains("factib") || n.Contains("implement")) return "cog";
            if (n.Contains("impact") || n.Contains("social")) return "heart";
            if (n.Contains("present") || n.Contains("expos")
                || n.Contains("comuni") || n.Contains("calidad")) return "microphone";
            return "star";
        }

        private static string IconoParaRol(string tipo) => tipo switch
        {
            "ENCARGADO"  => "shield",
            "JURADO"     => "award",
            "PUBLICO"    => "users",
            "COMPETIDOR" => "trophy",
            _            => "user"
        };

        private static string IconoParaEstado(string status) => status switch
        {
            "active"    => "play",
            "upcoming"  => "calendar",
            "paused"    => "cog",
            "finished"  => "check-circle",
            _           => "star"
        };

        // ── Pruebas: criterios ─────────────────────────────────────────────

        public static (bool Success, string Message) InnovacionMapeaALightbulb()
        {
            bool ok = IconoParaCriterio("Innovación") == "lightbulb"
                   && IconoParaCriterio("Creatividad") == "lightbulb"
                   && IconoParaCriterio("Originalidad") == "lightbulb";

            return ok
                ? (true, "'Innovación', 'Creatividad' y 'Originalidad' → 'lightbulb'")
                : (false, "Algún sinónimo de innovación no mapea a lightbulb");
        }

        public static (bool Success, string Message) ViabilidadMapeaACog()
        {
            bool ok = IconoParaCriterio("Viabilidad") == "cog"
                   && IconoParaCriterio("Viabilidad Técnica") == "cog"
                   && IconoParaCriterio("Factibilidad") == "cog"
                   && IconoParaCriterio("Implementación") == "cog";

            return ok
                ? (true, "Variantes de viabilidad técnica → 'cog'")
                : (false, "Alguna variante de viabilidad no mapea a cog");
        }

        public static (bool Success, string Message) ImpactoSocialMapeaAHeart()
        {
            bool ok = IconoParaCriterio("Impacto Social") == "heart"
                   && IconoParaCriterio("Impacto") == "heart"
                   && IconoParaCriterio("Beneficio Social") == "heart";

            return ok
                ? (true, "Variantes de impacto social → 'heart'")
                : (false, "Alguna variante de impacto no mapea a heart");
        }

        public static (bool Success, string Message) PresentacionMapeaAMicrophone()
        {
            bool ok = IconoParaCriterio("Presentación") == "microphone"
                   && IconoParaCriterio("Exposición") == "microphone"
                   && IconoParaCriterio("Calidad de la exposición") == "microphone";

            return ok
                ? (true, "Variantes de presentación → 'microphone'")
                : (false, "Alguna variante de presentación no mapea a microphone");
        }

        public static (bool Success, string Message) CriterioDesconocidoCaeAStar()
        {
            bool ok = IconoParaCriterio("Algo raro") == "star"
                   && IconoParaCriterio("") == "star"
                   && IconoParaCriterio(null) == "star";

            return ok
                ? (true, "Criterios sin coincidencia, vacíos o nulos → 'star' (fallback)")
                : (false, "El fallback de criterios no es 'star' en todos los casos");
        }

        public static (bool Success, string Message) MapeoDeCriteriosEsCaseInsensitive()
        {
            bool ok = IconoParaCriterio("INNOVACIÓN") == "lightbulb"
                   && IconoParaCriterio("innovación") == "lightbulb"
                   && IconoParaCriterio("InNoVaCiÓn") == "lightbulb";

            return ok
                ? (true, "El mapeo de criterios ignora mayúsculas/minúsculas")
                : (false, "El mapeo no es case-insensitive para 'Innovación'");
        }

        // ── Pruebas: roles ─────────────────────────────────────────────────

        public static (bool Success, string Message) RolesTienenIconoDistintivo()
        {
            bool ok = IconoParaRol("ENCARGADO")  == "shield"
                   && IconoParaRol("JURADO")     == "award"
                   && IconoParaRol("PUBLICO")    == "users"
                   && IconoParaRol("COMPETIDOR") == "trophy";

            return ok
                ? (true, "Los 4 roles tienen su icono: shield/award/users/trophy")
                : (false, "Algún rol no mapea al icono esperado");
        }

        public static (bool Success, string Message) RolDesconocidoCaeAUser()
        {
            return IconoParaRol("ROL_INVENTADO") == "user"
                ? (true, "Un rol no contemplado cae al fallback 'user'")
                : (false, "El fallback de rol no es 'user'");
        }

        // ── Pruebas: estados de votación ───────────────────────────────────

        public static (bool Success, string Message) EstadosDeVotacionTienenIcono()
        {
            bool ok = IconoParaEstado("active")    == "play"
                   && IconoParaEstado("upcoming")  == "calendar"
                   && IconoParaEstado("paused")    == "cog"
                   && IconoParaEstado("finished")  == "check-circle";

            return ok
                ? (true, "Los 4 estados (active/upcoming/paused/finished) tienen su icono")
                : (false, "Algún estado no mapea al icono esperado");
        }

        public static (bool Success, string Message) EstadoDesconocidoCaeAStar()
        {
            return IconoParaEstado("???") == "star"
                ? (true, "Un estado no contemplado cae al fallback 'star'")
                : (false, "El fallback de estado no es 'star'");
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-4129 — Información visual relevante (mapeo de iconos)",
            ("Innovación → lightbulb",          InnovacionMapeaALightbulb),
            ("Viabilidad → cog",                ViabilidadMapeaACog),
            ("Impacto Social → heart",          ImpactoSocialMapeaAHeart),
            ("Presentación → microphone",       PresentacionMapeaAMicrophone),
            ("Criterio desconocido → star",     CriterioDesconocidoCaeAStar),
            ("Case-insensitive en criterios",   MapeoDeCriteriosEsCaseInsensitive),
            ("Roles distintivos",               RolesTienenIconoDistintivo),
            ("Rol desconocido → user",          RolDesconocidoCaeAUser),
            ("Estados de votación",             EstadosDeVotacionTienenIcono),
            ("Estado desconocido → star",       EstadoDesconocidoCaeAStar));
    }
}
