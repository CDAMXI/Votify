using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Votify.Entities;

namespace Votify.BusinessLogic.Service
{
    public sealed class ComentarioPopularEntrada
    {
        public int Id { get; set; }
        public string Autor { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string TipoRol { get; set; } = string.Empty;
    }

    public sealed class CategoriaComentarioPopular
    {
        public string Nombre { get; set; } = string.Empty;
    }

    public sealed class AsignacionCategoriaComentario
    {
        public int IdComentario { get; set; }
        public string Categoria { get; set; } = string.Empty;
    }

    public sealed class ResultadoClasificacionComentariosPopulares
    {
        public List<CategoriaComentarioPopular> Categorias { get; set; } = new();
        public List<AsignacionCategoriaComentario> Comentarios { get; set; } = new();
    }

    public sealed class ComentarioPopularClasificado
    {
        public int Id { get; set; }
        public string Autor { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string TipoRol { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
    }

    public sealed class ResultadoComentariosPopulares
    {
        public List<string> Categorias { get; set; } = new();
        public List<ComentarioPopularClasificado> Comentarios { get; set; } = new();
    }

    public interface IComentarioPopularClassifier
    {
        Task<ResultadoClasificacionComentariosPopulares> ClasificarAsync(
            IReadOnlyList<ComentarioPopularEntrada> comentarios,
            CancellationToken cancellationToken = default);
    }

    public sealed class ComentariosPopularesAgrupador
    {
        private const string CategoriaFallback = "Sin clasificar";
        private readonly IComentarioPopularClassifier _classifier;

        public ComentariosPopularesAgrupador(IComentarioPopularClassifier classifier)
        {
            _classifier = classifier;
        }

        public async Task<ResultadoComentariosPopulares> AgruparAsync(
            IEnumerable<Voto> votos,
            CancellationToken cancellationToken = default)
        {
            var entradas = ExtraerComentariosPopulares(votos);
            if (entradas.Count == 0)
                return new ResultadoComentariosPopulares();

            ResultadoClasificacionComentariosPopulares clasificacion;
            try
            {
                clasificacion = await _classifier.ClasificarAsync(entradas, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                clasificacion = new ResultadoClasificacionComentariosPopulares();
            }

            var asignaciones = clasificacion.Comentarios
                .Where(c => c.IdComentario > 0 && !string.IsNullOrWhiteSpace(c.Categoria))
                .GroupBy(c => c.IdComentario)
                .ToDictionary(g => g.Key, g => NormalizarCategoria(g.First().Categoria));

            var comentarios = entradas
                .Select(e => new ComentarioPopularClasificado
                {
                    Id = e.Id,
                    Autor = e.Autor,
                    Texto = e.Texto,
                    Fecha = e.Fecha,
                    TipoRol = e.TipoRol,
                    Categoria = asignaciones.TryGetValue(e.Id, out var categoria) ? categoria : CategoriaFallback
                })
                .ToList();

            var categoriasSugeridas = clasificacion.Categorias
                .Select(c => NormalizarCategoria(c.Nombre))
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var categoriasUsadas = comentarios
                .Select(c => c.Categoria)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var categorias = categoriasSugeridas
                .Where(c => categoriasUsadas.Contains(c, StringComparer.OrdinalIgnoreCase))
                .Concat(categoriasUsadas.Where(c => !categoriasSugeridas.Contains(c, StringComparer.OrdinalIgnoreCase)))
                .ToList();

            return new ResultadoComentariosPopulares
            {
                Categorias = categorias,
                Comentarios = comentarios
            };
        }

        public static IReadOnlyList<ComentarioPopularEntrada> ExtraerComentariosPopulares(IEnumerable<Voto> votos)
        {
            return votos
                .Where(v => ResultadosVotacionCalculator.EsVotoPopular(v))
                .Where(v => !string.IsNullOrWhiteSpace(v.Comentario))
                .OrderByDescending(v => v.Fecha)
                .Select(v => new ComentarioPopularEntrada
                {
                    Id = v.Id,
                    Autor = v.votante?.usuario?.Username ?? string.Empty,
                    Texto = v.Comentario.Trim(),
                    Fecha = v.Fecha,
                    TipoRol = v.votante?.TipoRol ?? string.Empty
                })
                .ToList();
        }

        private static string NormalizarCategoria(string categoria)
        {
            var valor = (categoria ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(valor) ? CategoriaFallback : valor;
        }
    }
}
