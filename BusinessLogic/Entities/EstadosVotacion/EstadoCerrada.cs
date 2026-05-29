using System;

namespace Votify.Entities.EstadosVotacion
{
    public class EstadoCerrada : EstadoVotacion
    {
        public override void Pausar(Votacion votacion)
        {
            throw new InvalidOperationException("Una votación cerrada no puede ser pausada.");
        }

        public override void Reanudar(Votacion votacion)
        {
            throw new InvalidOperationException("Una votación cerrada está finalizada definitivamente y no puede ser reanudada.");
        }

        public override void Cerrar(Votacion votacion)
        {
            throw new InvalidOperationException("La votación ya se encuentra cerrada.");
        }

        public override bool PuedeVotar() => false;

        public override string ObtenerNombre() => "Cerrada";
    }
}
