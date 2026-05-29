using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Votify.BusinessLogic.Service
{
    /// <summary>
    /// ConcreteStrategy adicional del patron Estrategia: clasifica comentarios
    /// populares mediante reglas de palabras clave, SIN IA ni red.
    ///
    /// Demuestra la extensibilidad que describe la memoria: se anade un nuevo
    /// motor implementando <see cref="IComentarioPopularClassifier"/> sin tocar
    /// ni el contexto (<see cref="ComentariosPopularesAgrupador"/>) ni el resto
    /// de estrategias (Open/Closed). Al no depender de claves API es totalmente
    /// determinista y ejecutable offline.
    /// </summary>
    public sealed class HeuristicoComentarioPopularClassifier : IComentarioPopularClassifier
    {
        private const string CategoriaPorDefecto = "General";

        // El orden define la prioridad ante empate de puntuacion.
        private static readonly IReadOnlyList<(string Categoria, string[] Palabras)> Reglas = new[]
        {
            ("Positivo", new[] { "excelente", "genial", "gran ", "buen", "increible", "me gusta", "encanta", "intuitiv", "claro", "facil", "fantastic", "perfecto", "recomiendo" }),
            ("Mejora", new[] { "mejor", "deberia", "falta", "necesita", "problema", "error", "fallo", "lento", "confus", "dificil", "pero ", "aunque", "inutil" }),
            ("Tecnico", new[] { "codigo", "api", "rendimiento", "bug", "tecnic", "backend", "base de datos", "arquitectura", "integracion", "despliegue", "servidor" }),
            ("Diseno", new[] { "diseno", "interfaz", "color", "estetic", "visual", "layout", "boton", "pantalla" }),
            ("Impacto", new[] { "impacto", "soluciona", "resuelve", "futuro", "escala", "social", "ayuda", "cambio" }),
            ("Presentacion", new[] { "presentacion", "demo", "explicacion", "expone", "slides", "charla", "comunica" }),
        };

        public Task<ResultadoClasificacionComentariosPopulares> ClasificarAsync(
            IReadOnlyList<ComentarioPopularEntrada> comentarios,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resultado = new ResultadoClasificacionComentariosPopulares();
            if (comentarios.Count == 0)
                return Task.FromResult(resultado);

            foreach (var comentario in comentarios)
            {
                resultado.Comentarios.Add(new AsignacionCategoriaComentario
                {
                    IdComentario = comentario.Id,
                    Categoria = DetectarCategoria(Normalizar(comentario.Texto))
                });
            }

            resultado.Categorias = resultado.Comentarios
                .Select(a => a.Categoria)
                .Distinct(StringComparer.Ordinal)
                .Select(nombre => new CategoriaComentarioPopular { Nombre = nombre })
                .ToList();

            return Task.FromResult(resultado);
        }

        private static string DetectarCategoria(string textoNormalizado)
        {
            var mejorCategoria = CategoriaPorDefecto;
            var mejorPuntuacion = 0;

            foreach (var (categoria, palabras) in Reglas)
            {
                var puntuacion = palabras.Count(p => textoNormalizado.Contains(p, StringComparison.Ordinal));
                if (puntuacion > mejorPuntuacion)
                {
                    mejorPuntuacion = puntuacion;
                    mejorCategoria = categoria;
                }
            }

            return mejorCategoria;
        }

        /// <summary>Pasa a minusculas y elimina acentos para comparar sin depender de tildes.</summary>
        private static string Normalizar(string texto)
        {
            var descompuesto = (texto ?? string.Empty).ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);

            foreach (var caracter in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                    sb.Append(caracter);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
