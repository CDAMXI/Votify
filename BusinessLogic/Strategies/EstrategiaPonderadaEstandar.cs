using System.Collections.Generic;
using System.Linq;
using Votify.Entities;
using Votify.BusinessLogic.Service; // Para ResultadosVotacionCalculator.CalcularMedia

namespace Votify.BusinessLogic.Strategies
{
    public class EstrategiaPonderadaEstandar : IEstrategiaCalculoResultados
    {
        public double CalcularPuntuacionFinal(IEnumerable<Voto> votosJurado, IEnumerable<Voto> votosPublico, int pesoJurado, int pesoPublico)
        {
            var juradoList = votosJurado?.ToList() ?? new List<Voto>();
            var publicoList = votosPublico?.ToList() ?? new List<Voto>();

            double? mediaJurado = juradoList.Any() ? ResultadosVotacionCalculator.CalcularMedia(juradoList) : null;
            double? mediaPopular = publicoList.Any() ? ResultadosVotacionCalculator.CalcularMedia(publicoList) : null;

            return ResultadosVotacionCalculator.CalcularPuntuacionAjustada(mediaJurado, mediaPopular, pesoJurado, pesoPublico);
        }
    }
}
