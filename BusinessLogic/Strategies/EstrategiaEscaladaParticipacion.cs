using System;
using System.Collections.Generic;
using System.Linq;
using Votify.Entities;
using Votify.BusinessLogic.Service;

namespace Votify.BusinessLogic.Strategies
{
    public class EstrategiaEscaladaParticipacion : IEstrategiaCalculoResultados
    {
        private const int VOTOS_MINIMOS = 5;
        private const int VOTOS_MAXIMOS = 20;

        public double CalcularPuntuacionFinal(IEnumerable<Voto> votosJurado, IEnumerable<Voto> votosPublico, int pesoJurado, int pesoPublico)
        {
            var juradoList = votosJurado?.ToList() ?? new List<Voto>();
            var publicoList = votosPublico?.ToList() ?? new List<Voto>();

            double? mediaJurado = juradoList.Any() ? ResultadosVotacionCalculator.CalcularMedia(juradoList) : null;
            double? mediaPopular = publicoList.Any() ? ResultadosVotacionCalculator.CalcularMedia(publicoList) : null;

            int cantidadVotosPublicos = publicoList.Count;
            int pesoPublicoCalculado = pesoPublico;

            if (cantidadVotosPublicos < VOTOS_MINIMOS)
            {
                // Menos de 5 votos: no cuentan los votos públicos.
                pesoPublicoCalculado = 0;
            }
            else if (cantidadVotosPublicos < VOTOS_MAXIMOS)
            {
                // Escalada lineal: entre 5 y 20 votos
                double factor = (double)(cantidadVotosPublicos - VOTOS_MINIMOS) / (VOTOS_MAXIMOS - VOTOS_MINIMOS);
                pesoPublicoCalculado = (int)Math.Round(pesoPublico * factor);
            }
            // Si es >= VOTOS_MAXIMOS, mantiene el pesoPublico original

            // Como el peso público puede haber bajado, ese porcentaje restante 
            // debería pasarse al jurado para que sume 100% (o al menos mantenga la proporción correcta)
            // Calculamos la diferencia
            int diferencia = pesoPublico - pesoPublicoCalculado;
            int pesoJuradoCalculado = pesoJurado + diferencia;

            return ResultadosVotacionCalculator.CalcularPuntuacionAjustada(mediaJurado, mediaPopular, pesoJuradoCalculado, pesoPublicoCalculado);
        }
    }
}
