using System;

namespace Votify.Entities.EstadosVotacion
{
    public abstract class EstadoVotacion
    {
        public abstract void Pausar(Votacion votacion);
        public abstract void Reanudar(Votacion votacion);
        public abstract void Cerrar(Votacion votacion);
        public abstract bool PuedeVotar();
        public abstract string ObtenerNombre();
    }
}
