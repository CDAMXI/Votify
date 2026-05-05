using System;

namespace Votify.Entities
{
    public partial class Reclamacion
    {
        public Reclamacion() { }

        public Reclamacion(int eventoId, int usuarioId, string descripcion)
        {
            EventoId = eventoId;
            UsuarioId = usuarioId;
            Descripcion = descripcion;
            FechaCreacion = DateTime.Now;
            Estado = EstadoPendiente;
        }
    }
}
