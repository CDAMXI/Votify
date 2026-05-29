using System.Collections.Generic;
using Votify.Entities;

namespace Votify.BusinessLogic.Strategies
{
    public interface IEstrategiaCalculoResultados
    {
        double CalcularPuntuacionFinal(IEnumerable<Voto> votosJurado, IEnumerable<Voto> votosPublico, int pesoJurado, int pesoPublico);
    }
}
