using System;

namespace Votify.Entities.EstadosVotacion
{
    public class EstadoPausada : EstadoVotacion
    {
        public override void Pausar(Votacion votacion)
        {
            throw new InvalidOperationException("La votación ya se encuentra pausada.");
        }

        public override void Reanudar(Votacion votacion)
        {
            votacion.EstadoActual = new EstadoActiva();
        }

        public override void Cerrar(Votacion votacion)
        {
            votacion.EstadoActual = new EstadoCerrada();
        }

        public override bool PuedeVotar() => false;

        public override string ObtenerNombre() => "Pausada";
    }
}
