using System;
using System.Collections.Generic;
using System.Text;

namespace Votify.Entities
{
    public partial class Rol
    {
        //Atributos
        public DateTime FechaAsignacion { get; set; }

        //Relaciones
        public Evento evento; //Verificar
        public Usuario usuario; //Verificar
    }
}
