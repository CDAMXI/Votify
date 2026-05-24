using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Votify.BusinessLogic.Service;
using Votify.Entities;

namespace Votify.Tests
{
    /// <summary>
    /// Pruebas de aceptacion: categorizar comentarios populares mediante IA.
    ///
    /// Criterios verificados:
    ///   - Solo se envian a IA comentarios de PUBLICO y COMPETIDOR.
    ///   - Los comentarios sin texto y los de JURADO/ORGANIZADOR quedan fuera.
    ///   - Las categorias devueltas por IA se asignan a cada comentario.
    ///   - Si IA omite comentarios o falla, se clasifican como "Sin clasificar".
    /// </summary>
    public static class ComentariosPopularesIATest
    {
        private sealed class FakeClassifier : IComentarioPopularClassifier
        {
            public IReadOnlyList<ComentarioPopularEntrada> Recibidos { get; private set; } = new List<ComentarioPopularEntrada>();
            public ResultadoClasificacionComentariosPopulares Resultado { get; set; } = new();
            public bool DebeFallar { get; set; }

            public Task<ResultadoClasificacionComentariosPopulares> ClasificarAsync(
                IReadOnlyList<ComentarioPopularEntrada> comentarios,
                CancellationToken cancellationToken = default)
            {
                Recibidos = comentarios.ToList();

                if (DebeFallar)
                    throw new ServiceException("Fallo simulado de IA");

                return Task.FromResult(Resultado);
            }
        }

        public static (bool Success, string Message) SoloClasificaComentariosPopulares()
        {
            var fake = new FakeClassifier();
            var agrupador = new ComentariosPopularesAgrupador(fake);

            agrupador.AgruparAsync(CrearVotosEscenario()).GetAwaiter().GetResult();

            var ids = fake.Recibidos.Select(c => c.Id).OrderBy(id => id).ToList();
            bool ok = ids.SequenceEqual(new[] { 1, 2 });

            return ok
                ? (true, "Solo se enviaron a IA comentarios de publico y competidor con texto")
                : (false, $"Se esperaban IDs 1,2 y se recibieron {string.Join(",", ids)}");
        }

        public static (bool Success, string Message) AsignaCategoriasDevueltasPorIA()
        {
            var fake = new FakeClassifier
            {
                Resultado = new ResultadoClasificacionComentariosPopulares
                {
                    Categorias = new List<CategoriaComentarioPopular>
                    {
                        new() { Nombre = "Positivo" },
                        new() { Nombre = "Mejora" }
                    },
                    Comentarios = new List<AsignacionCategoriaComentario>
                    {
                        new() { IdComentario = 1, Categoria = "Positivo" },
                        new() { IdComentario = 2, Categoria = "Mejora" }
                    }
                }
            };

            var resultado = new ComentariosPopularesAgrupador(fake)
                .AgruparAsync(CrearVotosEscenario())
                .GetAwaiter()
                .GetResult();

            bool ok =
                resultado.Comentarios.Single(c => c.Id == 1).Categoria == "Positivo" &&
                resultado.Comentarios.Single(c => c.Id == 2).Categoria == "Mejora" &&
                resultado.Categorias.SequenceEqual(new[] { "Positivo", "Mejora" });

            return ok
                ? (true, "Las categorias de IA se asignaron a los comentarios populares")
                : (false, "Las categorias resultantes no coinciden con las asignaciones de IA");
        }

        public static (bool Success, string Message) ComentarioOmitidoPorIAQuedaSinClasificar()
        {
            var fake = new FakeClassifier
            {
                Resultado = new ResultadoClasificacionComentariosPopulares
                {
                    Categorias = new List<CategoriaComentarioPopular>
                    {
                        new() { Nombre = "Positivo" },
                        new() { Nombre = "Categoria vacia" }
                    },
                    Comentarios = new List<AsignacionCategoriaComentario>
                    {
                        new() { IdComentario = 1, Categoria = "Positivo" }
                    }
                }
            };

            var resultado = new ComentariosPopularesAgrupador(fake)
                .AgruparAsync(CrearVotosEscenario())
                .GetAwaiter()
                .GetResult();

            bool ok =
                resultado.Comentarios.Single(c => c.Id == 2).Categoria == "Sin clasificar" &&
                resultado.Categorias.Contains("Sin clasificar") &&
                !resultado.Categorias.Contains("Categoria vacia");

            return ok
                ? (true, "Los comentarios omitidos por IA quedan marcados como Sin clasificar y no aparecen categorias vacias")
                : (false, "No se aplico correctamente el fallback o aparecieron categorias sin comentarios");
        }

        public static (bool Success, string Message) FalloDeIAClasificaTodoComoFallback()
        {
            var fake = new FakeClassifier { DebeFallar = true };

            var resultado = new ComentariosPopularesAgrupador(fake)
                .AgruparAsync(CrearVotosEscenario())
                .GetAwaiter()
                .GetResult();

            bool ok =
                resultado.Comentarios.Count == 2 &&
                resultado.Comentarios.All(c => c.Categoria == "Sin clasificar");

            return ok
                ? (true, "Si IA falla, los comentarios populares se devuelven sin clasificar")
                : (false, "El fallback ante fallo de IA no preservo todos los comentarios populares");
        }

        private static List<Voto> CrearVotosEscenario()
        {
            return new List<Voto>
            {
                CrearVoto(1, new Publico(DateTime.Now, 0), "maria_tech", "Excelente aplicacion e interfaz intuitiva"),
                CrearVoto(2, new Competidor(DateTime.Now, 0), "carlos_dev", "Gran impacto, pero necesita mejor documentacion"),
                CrearVoto(3, new Jurado(DateTime.Now, 0), "ana_jurado", "Comentario experto que no debe categorizarse"),
                CrearVoto(4, new Organizador(DateTime.Now, 0), "orga", "Comentario organizador que no debe categorizarse"),
                CrearVoto(5, new Publico(DateTime.Now, 0), "sin_texto", "   ")
            };
        }

        private static Voto CrearVoto(int id, Rol rol, string username, string comentario)
        {
            rol.Id = id + 100;
            rol.UsuarioId = id + 200;
            rol.usuario = new Usuario(username, $"{username}@test.com", "pass", rol.UsuarioId);

            return new Voto(8, comentario, DateTime.Now.AddMinutes(id))
            {
                Id = id,
                votante = rol,
                VotanteId = rol.Id,
                VotacionId = 10,
                ProyectoId = 20
            };
        }

        public static (bool Success, string Message) RunAll() => TestRunner.Run(
            "UT - Categorizar comentarios populares mediante IA",
            ("Solo comentarios populares", SoloClasificaComentariosPopulares),
            ("Asignacion de categorias IA", AsignaCategoriasDevueltasPorIA),
            ("Fallback para omitidos", ComentarioOmitidoPorIAQuedaSinClasificar),
            ("Fallback ante fallo IA", FalloDeIAClasificaTodoComoFallback));
    }
}
