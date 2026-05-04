using System;
using System.Collections.Generic;
using System.Linq;
using Votify.Entities;

namespace Votify.BusinessLogic.Service
{
    public static class ResultadosVotacionCalculator
    {
        public static bool EsVotoExperto(Voto voto)
            => string.Equals(voto.votante?.TipoRol, "JURADO", StringComparison.OrdinalIgnoreCase);

        public static bool EsVotoPopular(Voto voto)
        {
            string? tipoRol = voto.votante?.TipoRol;
            return string.Equals(tipoRol, "PUBLICO", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tipoRol, "COMPETIDOR", StringComparison.OrdinalIgnoreCase);
        }

        public static double CalcularMedia(IEnumerable<Voto> votos)
        {
            List<Voto> lista = votos.ToList();
            if (!lista.Any())
                return 0;

            return Math.Round(lista.Average(v => v.Valor), 2);
        }

        public static double CalcularPuntuacionAjustada(
            double? mediaExperta,
            double? mediaPopular,
            int pesoExperto,
            int pesoPopular)
        {
            List<(double Media, int Peso)> buckets = new();

            if (pesoExperto > 0 && mediaExperta.HasValue)
                buckets.Add((mediaExperta.Value, pesoExperto));

            if (pesoPopular > 0 && mediaPopular.HasValue)
                buckets.Add((mediaPopular.Value, pesoPopular));

            if (!buckets.Any())
                return 0;

            double totalPeso = buckets.Sum(b => b.Peso);
            double totalPonderado = buckets.Sum(b => b.Media * b.Peso);
            return Math.Round(totalPonderado / totalPeso, 2);
        }
    }
}
