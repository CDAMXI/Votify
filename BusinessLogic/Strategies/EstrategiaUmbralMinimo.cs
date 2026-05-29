using System.Collections.Generic;
using System.Linq;
using Votify.Entities;
using Votify.BusinessLogic.Service;

namespace Votify.BusinessLogic.Strategies
{
    public class EstrategiaUmbralMinimo : IEstrategiaCalculoResultados
    {
        private const double UMBRAL_MINIMO_JURADO = 5.0;

        public double CalcularPuntuacionFinal(IEnumerable<Voto> votosJurado, IEnumerable<Voto> votosPublico, int pesoJurado, int pesoPublico)
        {
            var juradoList = votosJurado?.ToList() ?? new List<Voto>();
            var publicoList = votosPublico?.ToList() ?? new List<Voto>();

            double? mediaJurado = juradoList.Any() ? ResultadosVotacionCalculator.CalcularMedia(juradoList) : null;
            double? mediaPopular = publicoList.Any() ? ResultadosVotacionCalculator.CalcularMedia(publicoList) : null;

            // Si el jurado no ha votado, no podemos aplicar el umbral, así que calculamos normal (o podríamos devolver 0)
            if (!mediaJurado.HasValue)
            {
                return ResultadosVotacionCalculator.CalcularPuntuacionAjustada(null, mediaPopular, pesoJurado, pesoPublico);
            }

            // Si la nota del jurado es menor que el umbral (ej. 5.0), el proyecto suspende y el público no le puede salvar.
            if (mediaJurado.Value < UMBRAL_MINIMO_JURADO)
            {
                return mediaJurado.Value; // Se queda con la nota del jurado
            }

            // Si supera el umbral, se aplica la ponderación normal
            return ResultadosVotacionCalculator.CalcularPuntuacionAjustada(mediaJurado, mediaPopular, pesoJurado, pesoPublico);
        }
    }
}
