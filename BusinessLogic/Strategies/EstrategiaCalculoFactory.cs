using System;

namespace Votify.BusinessLogic.Strategies
{
    public static class EstrategiaCalculoFactory
    {
        public static IEstrategiaCalculoResultados CrearEstrategia(string tipoEstrategia)
        {
            if (string.IsNullOrWhiteSpace(tipoEstrategia))
            {
                return new EstrategiaPonderadaEstandar();
            }

            return tipoEstrategia.Trim().ToUpperInvariant() switch
            {
                "UMBRAL" => new EstrategiaUmbralMinimo(),
                "ESCALADA" => new EstrategiaEscaladaParticipacion(),
                "ESTANDAR" => new EstrategiaPonderadaEstandar(),
                _ => new EstrategiaPonderadaEstandar()
            };
        }
    }
}
