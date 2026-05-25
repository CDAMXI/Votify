using System;
using System.Globalization;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptación — UT-4124: Cambiar estilo del calendario.
    ///
    /// El picker visual (lunes-primero, nombres en español, popup con CSS de
    /// Flatpickr) lo renderiza JavaScript en el navegador y no es testable
    /// desde C#. Lo que sí podemos verificar de forma reproducible es el
    /// contrato de **intercambio de datos** entre el componente Blazor y el
    /// helper JS `votifyFlatpickr`, que es la base sobre la que se construye
    /// todo lo demás.
    ///
    /// Criterios verificados:
    ///   · Una fecha .NET se serializa a ISO 'yyyy-MM-dd' inequívoco para JS.
    ///   · El ISO local se parsea de vuelta a la fecha original sin desfase.
    ///   · El formato de visualización en pantalla es 'dd/MM/yyyy' (locale ES).
    ///   · MinDate considera "hoy" como límite inferior aceptable.
    ///   · El round-trip C# → ISO → C# preserva año, mes y día.
    /// </summary>
    public static class CambiarEstiloCalendarioTest
    {
        // ── Helpers que replican la lógica del componente FlatpickrInput ───

        private const string FormatoIso        = "yyyy-MM-dd";
        private const string FormatoVisualEs   = "dd/MM/yyyy";

        private static string ASerializarParaJs(DateTime fecha) =>
            fecha.ToString(FormatoIso, CultureInfo.InvariantCulture);

        private static string AVisualizarEnEspanol(DateTime fecha) =>
            fecha.ToString(FormatoVisualEs, CultureInfo.InvariantCulture);

        private static DateTime? AParsearDesdeJs(string iso) =>
            DateTime.TryParseExact(iso, FormatoIso, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fecha)
                ? fecha
                : null;

        private static bool EsAceptableConMinDateHoy(DateTime fecha) =>
            fecha.Date >= DateTime.Today;

        // ── Pruebas ────────────────────────────────────────────────────────

        public static (bool Success, string Message) FechaSeSerializaAIsoInequivoco()
        {
            var fecha = new DateTime(2026, 5, 24);
            string iso = ASerializarParaJs(fecha);

            return iso == "2026-05-24"
                ? (true, "DateTime → ISO 'yyyy-MM-dd' produce '2026-05-24'")
                : (false, $"Esperaba '2026-05-24', obtuvo '{iso}'");
        }

        public static (bool Success, string Message) FechaSeVisualizaEnFormatoEspanol()
        {
            var fecha = new DateTime(2026, 5, 24);
            string visual = AVisualizarEnEspanol(fecha);

            return visual == "24/05/2026"
                ? (true, "DateTime → display 'dd/MM/yyyy' produce '24/05/2026' (locale ES)")
                : (false, $"Esperaba '24/05/2026', obtuvo '{visual}'");
        }

        public static (bool Success, string Message) IsoSeParseaSinDesfase()
        {
            var fecha = AParsearDesdeJs("2026-05-24");

            return fecha.HasValue
                && fecha.Value.Year == 2026
                && fecha.Value.Month == 5
                && fecha.Value.Day == 24
                ? (true, "ISO '2026-05-24' → DateTime 2026-05-24 sin desfase de zona")
                : (false, $"Esperaba 2026-05-24, obtuvo {fecha?.ToString("o") ?? "<null>"}");
        }

        public static (bool Success, string Message) IsoMalformadoNoRompe()
        {
            var fechaInvalida   = AParsearDesdeJs("no-es-una-fecha");
            var fechaConBarras  = AParsearDesdeJs("24/05/2026");        // formato incorrecto para parser
            var fechaConHora    = AParsearDesdeJs("2026-05-24T00:00:00"); // demasiada precisión

            return fechaInvalida == null && fechaConBarras == null && fechaConHora == null
                ? (true, "El parser estricto rechaza cadenas que no encajan en 'yyyy-MM-dd'")
                : (false, $"Esperaba 3 nulls, obtuvo {(fechaInvalida == null)}, {(fechaConBarras == null)}, {(fechaConHora == null)}");
        }

        public static (bool Success, string Message) MinDateAceptaHoyYRechazaAyer()
        {
            var hoy   = DateTime.Today;
            var ayer  = hoy.AddDays(-1);
            var manana = hoy.AddDays(1);

            return EsAceptableConMinDateHoy(hoy)
                && EsAceptableConMinDateHoy(manana)
                && !EsAceptableConMinDateHoy(ayer)
                ? (true, "MinDate=hoy admite hoy y mañana, rechaza ayer")
                : (false, $"hoy={EsAceptableConMinDateHoy(hoy)}, manana={EsAceptableConMinDateHoy(manana)}, ayer={EsAceptableConMinDateHoy(ayer)}");
        }

        public static (bool Success, string Message) RoundTripCSharpJsCSharpPreservaFecha()
        {
            var original = new DateTime(2026, 12, 31);
            string iso = ASerializarParaJs(original);
            var devuelta = AParsearDesdeJs(iso);

            return devuelta.HasValue && devuelta.Value.Date == original.Date
                ? (true, $"Round-trip preserva la fecha: {original:yyyy-MM-dd} → '{iso}' → {devuelta:yyyy-MM-dd}")
                : (false, $"Round-trip alteró la fecha: {original:o} → '{iso}' → {devuelta?.ToString("o") ?? "<null>"}");
        }

        // ── Runner ─────────────────────────────────────────────────────────

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT-4124 — Cambiar estilo del calendario (intercambio C# ↔ JS)",
            ("Serialización ISO inequívoca",        FechaSeSerializaAIsoInequivoco),
            ("Visualización formato español",       FechaSeVisualizaEnFormatoEspanol),
            ("ISO se parsea sin desfase",           IsoSeParseaSinDesfase),
            ("ISO malformado no rompe",             IsoMalformadoNoRompe),
            ("MinDate=hoy",                         MinDateAceptaHoyYRechazaAyer),
            ("Round-trip C# → ISO → C#",            RoundTripCSharpJsCSharpPreservaFecha));
    }
}
