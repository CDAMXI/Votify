using System;

namespace Votify.Entities.EstadosVotacion
{
    public static class EstadoFactory
    {
        public static EstadoVotacion Crear(string nombreEstado)
        {
            if (string.IsNullOrWhiteSpace(nombreEstado))
                return new EstadoActiva();

            return nombreEstado.Trim().ToUpperInvariant() switch
            {
                "ACTIVA" => new EstadoActiva(),
                "PAUSADA" => new EstadoPausada(),
                "CERRADA" => new EstadoCerrada(),
                _ => new EstadoActiva() // Default
            };
        }
    }
}
