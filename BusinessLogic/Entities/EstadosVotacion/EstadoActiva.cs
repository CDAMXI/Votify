using System;

namespace Votify.Entities.EstadosVotacion
{
    public class EstadoActiva : EstadoVotacion
    {
        public override void Pausar(Votacion votacion)
        {
            votacion.EstadoActual = new EstadoPausada();
        }

        public override void Reanudar(Votacion votacion)
        {
            throw new InvalidOperationException("La votación ya está activa y no puede ser reanudada.");
        }

        public override void Cerrar(Votacion votacion)
        {
            votacion.EstadoActual = new EstadoCerrada();
        }

        public override bool PuedeVotar() => true;

        public override string ObtenerNombre() => "Activa";
    }
}
